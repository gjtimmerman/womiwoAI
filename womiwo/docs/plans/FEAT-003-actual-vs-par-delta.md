# Implementation Plan: Actual-vs-Par Delta Calculation

**Spec:** `docs/specs/FEAT-003-actual-vs-par-delta.md`
**ADR:** `docs/architecture/adr/001-technology-stack.md`
**Created:** 2026-04-08
**Status:** Draft

## Summary

Implement bridge contract scoring and IMP delta calculation as a pure-arithmetic pipeline in the Shared and Server projects. A static `BridgeScorer` class in `BridgeGameCalculator.Shared` handles all WBF duplicate scoring rules and IMP table lookups. A `DeltaCalculationService` in `BridgeGameCalculator.Server` orchestrates the scoring and delta computation per board. The analysis endpoint is extended to return `BoardDelta` alongside `ParResult` for each board.

## Key Design Decisions

1. **`BridgeScorer` is a static class, not a DI service.** The scoring logic is pure math with zero dependencies -- no I/O, no state, no configuration. A static class keeps it simple, directly testable, and avoids unnecessary DI ceremony. It lives in `BridgeGameCalculator.Shared` so both Server and Client can use it (the Client will need it for single-hand entry in FEAT-006).

2. **`DeltaCalculationService` is a registered service in Server.** It coordinates Board + ParResult into a BoardDelta. Even though it is lightweight today, making it a service allows the analysis endpoint to depend on it via DI and keeps the endpoint handler thin. Registered as a singleton (it is stateless).

3. **IMP table is a `ReadOnlySpan<int>` of upper bounds.** The WBF IMP scale has 21 rows. Encoding it as a sorted array of upper bounds and using a linear scan (21 iterations max) is simpler and faster than a dictionary or binary search for this tiny dataset. The lookup method is `ImpFromDifference(int absoluteDifference)`.

4. **All scores expressed from NS perspective as the canonical direction.** When the declarer is East or West, the raw score is negated before returning. This convention is established in the FEAT-002 domain model and carried forward here.

5. **`BoardDelta` is a record in Shared.** It is a data-transfer object that flows from Server to Client. Using a C# `record` gives value equality, immutability, and clean serialization for free.

6. **Vulnerability is resolved per-declarer, not per-board.** The `Vulnerability` enum on a `Board` says which *sides* are vulnerable (None, NS, EW, Both). The scorer must check whether the *declaring side* is vulnerable, not just read the enum directly. This is a common source of bugs in bridge software.

7. **Tests use `[Theory]` with `[InlineData]` for scoring tables.** The WBF scoring rules have many combinations (level, strain, vulnerability, doubled, overtricks, undertricks). Parameterized xUnit theories are the right pattern -- they produce one test case per row, making failures easy to diagnose.

## Implementation Steps

### Phase 1: Shared Domain Models

Create the solution structure (if not already present) and add the shared domain types that FEAT-003 depends on. Since this is a greenfield project, the domain models from FEAT-001 and FEAT-002 must also exist for FEAT-003 to compile. This phase establishes exactly what FEAT-003 needs.

**Step 1.1: Create the solution and project structure**

Create the three-project solution as defined in ADR-001:

```
BridgeGameCalculator.sln
src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj
src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj
src/BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj
tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj
```

- `Shared` is a `netstandard2.1` or `net8.0` class library.
- `Server` is an ASP.NET Core 8 web project (references Shared).
- `Client` is a Blazor WASM project (references Shared).
- `Tests` is an xUnit test project (references Shared and Server).

The existing `BridgeDDCalculator/` console project is a placeholder and is not part of the target architecture. Leave it in place but do not reference it.

**Step 1.2: Create domain enums in Shared**

File: `src/BridgeGameCalculator.Shared/Domain/Seat.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public enum Seat { North, East, South, West }
```

File: `src/BridgeGameCalculator.Shared/Domain/Vulnerability.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public enum Vulnerability { None, NS, EW, Both }
```

File: `src/BridgeGameCalculator.Shared/Domain/Strain.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public enum Strain { Clubs, Diamonds, Hearts, Spades, NoTrump }
```

File: `src/BridgeGameCalculator.Shared/Domain/DoubledState.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public enum DoubledState { Undoubled, Doubled, Redoubled }
```

**Step 1.3: Create the `Contract` value object**

File: `src/BridgeGameCalculator.Shared/Domain/Contract.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public sealed record Contract(int Level, Strain Strain, DoubledState Doubled)
{
    // Level must be 1-7
}
```

**Step 1.4: Create the `Board` entity (minimal, FEAT-001 fields only)**

File: `src/BridgeGameCalculator.Shared/Domain/Board.cs`

Include only the fields needed for FEAT-003 scoring: `BoardNumber`, `Dealer`, `Vulnerability`, `Contract`, `Declarer`, `Result`. The `Hands` property is needed for FEAT-001/002 but is not consumed by FEAT-003 scoring. Include it as a placeholder (`string?` for now) so the domain model is complete.

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public sealed record Board(
    int BoardNumber,
    Seat Dealer,
    Vulnerability Vulnerability,
    string? DealPbn,          // PBN deal string; placeholder until FEAT-001 parser is built
    Contract? Contract,       // null if passed out
    Seat? Declarer,           // null if passed out
    int? Result               // tricks taken by declarer, 0-13; null if passed out or no result
);
```

**Step 1.5: Create the `ParResult` entity (from FEAT-002)**

File: `src/BridgeGameCalculator.Shared/Domain/ParResult.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public sealed record ParContract(int Level, Strain Strain, Seat Declarer, DoubledState Doubled);

public sealed record ParResult(int BoardNumber, int ParScore, IReadOnlyList<ParContract> ParContracts);
```

**Step 1.6: Create the `BoardDelta` entity**

File: `src/BridgeGameCalculator.Shared/Domain/BoardDelta.cs`

```csharp
namespace BridgeGameCalculator.Shared.Domain;

public sealed record BoardDelta(
    int BoardNumber,
    int? ActualScore,    // NS perspective; null if no result
    int ParScore,        // NS perspective
    int? ImpDelta        // positive = NS beats par; null if no result
);
```

### Phase 2: BridgeScorer (Shared)

This is the core arithmetic engine. All methods are static and pure.

**Step 2.1: Create `BridgeScorer` with `CalculateScore`**

File: `src/BridgeGameCalculator.Shared/Scoring/BridgeScorer.cs`

Namespace: `BridgeGameCalculator.Shared.Scoring`

Public API:

```csharp
public static class BridgeScorer
{
    /// <summary>
    /// Calculate the duplicate bridge score for a contract result.
    /// Returns points from NS perspective (positive = NS gains, negative = EW gains).
    /// </summary>
    public static int CalculateScore(
        Contract contract,
        Seat declarer,
        Vulnerability vulnerability,
        int tricksMade)

    /// <summary>
    /// Calculate the IMP delta between an actual score and the par score.
    /// Returns null if actualScore is null (board not played / no result).
    /// Positive = NS outperformed par; negative = NS underperformed.
    /// </summary>
    public static int? CalculateImpDelta(int? actualScore, int parScore)

    /// <summary>
    /// Look up the WBF IMP value for an absolute point difference.
    /// </summary>
    public static int ImpFromDifference(int absoluteDifference)
}
```

Implementation details for `CalculateScore`:

- Determine if the declaring side is vulnerable by checking `Vulnerability` against `Declarer`:
  - North/South are vulnerable when `Vulnerability` is `NS` or `Both`.
  - East/West are vulnerable when `Vulnerability` is `EW` or `Both`.
- Compute `tricksOverContract = tricksMade - (6 + contract.Level)`.
- If `tricksOverContract >= 0` (contract made):
  - Compute trick score (per-trick value depends on strain and doubled state).
  - Clubs/Diamonds: 20 per trick. Hearts/Spades: 30 per trick. NT: 40 first trick, 30 subsequent.
  - Doubled: trick values x2. Redoubled: trick values x4.
  - Determine game bonus: trick score >= 100 is game (300 NV / 500 V). Below 100 is partial (50).
  - Slam bonuses: level 6 = small slam (500 NV / 750 V). Level 7 = grand slam (1000 NV / 1500 V).
  - Doubled/redoubled making bonus: 50 per doubled, 100 per redoubled (the "insult" bonus).
  - Overtrick value: undoubled = per-trick value. Doubled = 100 NV / 200 V per overtrick. Redoubled = 200 NV / 400 V per overtrick.
  - Total = trick score + game/slam bonus + doubled making bonus + overtrick score.
- If `tricksOverContract < 0` (contract defeated):
  - Undertricks = absolute value of tricksOverContract.
  - Undoubled: 50 NV / 100 V per undertrick.
  - Doubled NV: 1st = 100, 2nd-3rd = 200 each, 4th+ = 300 each.
  - Doubled V: 1st = 200, 2nd-3rd = 300 each, 4th+ = 300 each.
  - Redoubled: double the doubled penalties.
  - Total is negative (penalty to declarer).
- Convert to NS perspective: if declarer is East or West, negate the score.

**Step 2.2: Implement the WBF IMP lookup table**

Inside `BridgeScorer.cs`. Encode as a private static readonly array of upper-bound thresholds:

```csharp
// Upper bounds for each IMP level. Index = IMP value.
// ImpFromDifference scans until absoluteDifference <= UpperBounds[i].
private static readonly int[] ImpUpperBounds = new[]
{
    10,   // 0 IMPs: 0-10
    40,   // 1 IMP:  20-40
    80,   // 2 IMPs: 50-80
    120,  // 3 IMPs: 90-120
    160,  // 4 IMPs: 130-160
    210,  // 5 IMPs: 170-210
    260,  // 6 IMPs: 220-260
    310,  // 7 IMPs: 270-310
    360,  // 8 IMPs: 320-360
    420,  // 9 IMPs: 370-420
    490,  // 10 IMPs: 430-490
    590,  // 11 IMPs: 500-590
    740,  // 12 IMPs: 600-740
    890,  // 13 IMPs: 750-890
    1090, // 14 IMPs: 900-1090
    1290, // 15 IMPs: 1100-1290
    1490, // 16 IMPs: 1300-1490
    1740, // 17 IMPs: 1500-1740
    1990, // 18 IMPs: 1750-1990
    2240, // 19 IMPs: 2000-2240
    2490, // 20 IMPs: 2250-2490
};
// 2500+ = 24 IMPs (handled as a special case after the loop)
```

`ImpFromDifference` implementation:
- Take `Math.Abs(absoluteDifference)`.
- Linear scan through `ImpUpperBounds`. Return the index if `absoluteDifference <= ImpUpperBounds[i]`.
- If no match (> 2490), return 24.

`CalculateImpDelta` implementation:
- If `actualScore` is null, return null.
- `int diff = actualScore.Value - parScore`.
- `int imps = ImpFromDifference(Math.Abs(diff))`.
- Return `diff >= 0 ? imps : -imps`. (diff == 0 gives 0 IMPs, which is correct.)

### Phase 3: DeltaCalculationService (Server)

**Step 3.1: Create `DeltaCalculationService`**

File: `src/BridgeGameCalculator.Server/Services/DeltaCalculationService.cs`

Namespace: `BridgeGameCalculator.Server.Services`

```csharp
public class DeltaCalculationService
{
    /// <summary>
    /// Compute the BoardDelta for a single board given its par result.
    /// </summary>
    public BoardDelta CalculateDelta(Board board, ParResult parResult)
    {
        // 1. If board is passed out (contract is null): actualScore = 0, impDelta = ImpFromDifference(|0 - parScore|)
        //    Note: for a truly passed-out board, parScore should also be 0, so delta = 0.
        // 2. If board has a contract but no result (result is null): actualScore = null, impDelta = null.
        // 3. Otherwise: compute actualScore via BridgeScorer.CalculateScore, then impDelta via BridgeScorer.CalculateImpDelta.
    }

    /// <summary>
    /// Compute deltas for all boards in a session.
    /// </summary>
    public IReadOnlyList<BoardDelta> CalculateDeltas(
        IReadOnlyList<Board> boards,
        IReadOnlyList<ParResult> parResults)
    {
        // Match boards to parResults by BoardNumber.
        // Call CalculateDelta for each pair.
        // Return list of BoardDelta.
    }
}
```

Register in DI as singleton in `Program.cs`:

```csharp
builder.Services.AddSingleton<DeltaCalculationService>();
```

Key logic for `CalculateDelta`:

```
if (board.Contract is null)
    // Passed out: actualScore = 0
    return new BoardDelta(board.BoardNumber, 0, parResult.ParScore, 
        BridgeScorer.CalculateImpDelta(0, parResult.ParScore));

if (board.Result is null)
    // Contract exists but no result recorded
    return new BoardDelta(board.BoardNumber, null, parResult.ParScore, null);

int actualScore = BridgeScorer.CalculateScore(
    board.Contract, board.Declarer!.Value, board.Vulnerability, board.Result.Value);
int? impDelta = BridgeScorer.CalculateImpDelta(actualScore, parResult.ParScore);
return new BoardDelta(board.BoardNumber, actualScore, parResult.ParScore, impDelta);
```

### Phase 4: API Integration

**Step 4.1: Create or extend the analysis response DTO**

File: `src/BridgeGameCalculator.Shared/Api/AnalysisResponse.cs`

```csharp
namespace BridgeGameCalculator.Shared.Api;

public sealed record BoardAnalysisResult(
    int BoardNumber,
    ParResult ParResult,
    BoardDelta Delta
);

public sealed record SessionAnalysisResponse(
    IReadOnlyList<BoardAnalysisResult> Boards
);
```

**Step 4.2: Wire into the analysis endpoint**

File: `src/BridgeGameCalculator.Server/Program.cs` (or a dedicated endpoint file if the project uses a route-grouping pattern)

The `/api/sessions/{id}/analyze` endpoint (or equivalent; the exact URL may be defined by FEAT-001/002 plans) should:

1. Accept the session ID (in-memory session reference).
2. Retrieve the parsed boards (from FEAT-001).
3. Run DD analysis to get `ParResult` per board (from FEAT-002).
4. Call `DeltaCalculationService.CalculateDeltas(boards, parResults)`.
5. Combine into `SessionAnalysisResponse` and return.

Since FEAT-001 and FEAT-002 are not yet implemented, define the endpoint shape now and use TODO comments for the upstream dependencies. The delta calculation portion is fully implementable.

```csharp
app.MapPost("/api/sessions/{id}/analyze", async (
    string id,
    DeltaCalculationService deltaService,
    /* DdsAnalysisService ddsService -- from FEAT-002 */
    /* SessionStore sessionStore -- from FEAT-001 */) =>
{
    // var session = sessionStore.Get(id);  // FEAT-001
    // var parResults = await ddsService.AnalyzeSession(session.Boards);  // FEAT-002
    // var deltas = deltaService.CalculateDeltas(session.Boards, parResults);
    // var response = new SessionAnalysisResponse(
    //     session.Boards.Zip(parResults, deltas)
    //         .Select(t => new BoardAnalysisResult(t.First.BoardNumber, t.Second, t.Third))
    //         .ToList());
    // return Results.Ok(response);
});
```

### Phase 5: Tests

**Step 5.1: Create `BridgeScorerTests` -- Contract Scoring**

File: `tests/BridgeGameCalculator.Tests/Scoring/BridgeScorerCalculateScoreTests.cs`

Use `[Theory]` with `[InlineData]` for each test category. Group tests into logical regions:

**Making contracts (undoubled, NV):**

| Level | Strain | Tricks | Expected Score | Why |
|-------|--------|--------|----------------|-----|
| 1 | Clubs | 7 | 70 | 20x1 trick + 50 partial |
| 2 | Hearts | 8 | 110 | 30x2 + 50 partial (trick score 60 < 100) |
| 3 | NoTrump | 9 | 400 | 40+30+30=100 trick score (game!) + 300 NV game |
| 4 | Spades | 10 | 420 | 30x4=120 + 300 NV game |
| 5 | Diamonds | 11 | 400 | 20x5=100 + 300 NV game |
| 6 | Hearts | 12 | 980 | 30x6=180 + 300 game + 500 small slam |
| 7 | NoTrump | 13 | 1520 | 40+6x30=220 + 300 game + 1000 grand slam |

**Making contracts (undoubled, vulnerable):**

| Level | Strain | Tricks | Expected Score | Why |
|-------|--------|--------|----------------|-----|
| 3 | NoTrump | 9 | 600 | 100 trick + 500 V game |
| 4 | Spades | 10 | 620 | 120 trick + 500 V game |
| 6 | Hearts | 12 | 1430 | 180 trick + 500 game + 750 V small slam |
| 7 | NoTrump | 13 | 2220 | 220 trick + 500 game + 1500 V grand slam |

**Overtricks (undoubled):**

| Level | Strain | Tricks | Vuln | Expected Score | Why |
|-------|--------|--------|------|----------------|-----|
| 3 | NoTrump | 10 | NV | 430 | 400 + 30 overtrick |
| 4 | Spades | 11 | NV | 450 | 420 + 30 overtrick |

**Going down (undoubled):**

| Undertricks | Vuln | Expected Score | Why |
|-------------|------|----------------|-----|
| Down 1 | NV | -50 | |
| Down 1 | V | -100 | |
| Down 3 | NV | -150 | 3x50 |
| Down 3 | V | -300 | 3x100 |

**Doubled contracts making:**

| Level | Strain | Tricks | Vuln | Expected Score | Why |
|-------|--------|--------|------|----------------|-----|
| 2 | Spades | 8 | NV | 470 | 2x30x2=120 trick + 300 game + 50 insult |
| 4 | Hearts | 10 | V | 790 | 2x30x4=240 trick + 500 game + 50 insult |
| 4 | Hearts | 11 | NV | 590 | 240 trick + 300 game + 50 insult + 100 NV doubled overtrick |
| 4 | Hearts | 11 | V | 990 | 240 trick + 500 game + 50 insult + 200 V doubled overtrick |

**Doubled contracts going down:**

| Undertricks | Vuln | Expected Score | Why |
|-------------|------|----------------|-----|
| Down 1 | NV | -100 | |
| Down 1 | V | -200 | |
| Down 2 | NV | -300 | 100 + 200 |
| Down 3 | NV | -500 | 100 + 200 + 200 |
| Down 4 | NV | -800 | 100 + 200 + 200 + 300 |
| Down 2 | V | -500 | 200 + 300 |
| Down 3 | V | -800 | 200 + 300 + 300 |

**Redoubled contracts:**

| Scenario | Expected Score | Why |
|----------|----------------|-----|
| 2S XX making exactly, NV | 1040 | 4x30x2=240 trick + 300 game + 100 insult + 200x0 overtrick (wait -- check: redoubled trick score: level*suit_value*4. 2S = 2*30*4 = 240. 240 >= 100 = game. 300 + 240 + 100 = 640.) |

Correction -- let me be precise. Redoubled 2S NV making exactly:
- Trick score: 2 * 30 * 4 = 240 (redoubled). Game (>= 100). NV game bonus = 300.
- Insult bonus: 100 (redoubled).
- Total: 240 + 300 + 100 = 640.

Include a test for this: `(2, Spades, Redoubled, 8, false) => 640`.

**EW declaring (NS perspective):**

Test that when declarer is East or West, the raw score is negated:
- East declares 4S making 10 tricks, EW NV: raw = +420, NS perspective = -420.
- West declares 3NT down 1, EW V: raw = -100, NS perspective = +100.

**Step 5.2: Create `BridgeScorerTests` -- IMP Table**

File: `tests/BridgeGameCalculator.Tests/Scoring/BridgeScorerImpTests.cs`

Test `ImpFromDifference` at every boundary defined in the WBF table:

```
[Theory]
[InlineData(0, 0)]
[InlineData(10, 0)]
[InlineData(20, 1)]
[InlineData(40, 1)]
[InlineData(50, 2)]
[InlineData(80, 2)]
[InlineData(90, 3)]
[InlineData(120, 3)]
[InlineData(130, 4)]
[InlineData(490, 10)]
[InlineData(500, 11)]
[InlineData(590, 11)]
[InlineData(600, 12)]
[InlineData(740, 12)]
[InlineData(750, 13)]
[InlineData(2490, 20)]
[InlineData(2500, 24)]
[InlineData(3000, 24)]
[InlineData(5000, 24)]
```

Test `CalculateImpDelta`:

```
[Theory]
[InlineData(450, 420, 1)]      // +30 diff = 1 IMP (NS beats par)
[InlineData(-100, 600, -12)]   // -700 diff = -12 IMPs (NS loses to par)
[InlineData(-420, -420, 0)]    // 0 diff = 0 IMPs (matches par)
[InlineData(null, 420, null)]  // no result = null
[InlineData(0, 0, 0)]          // passed out = 0 IMPs
```

**Step 5.3: Create `DeltaCalculationServiceTests`**

File: `tests/BridgeGameCalculator.Tests/Services/DeltaCalculationServiceTests.cs`

Test cases mapping directly to the acceptance scenarios in the spec:

1. **SC-001 (NS overtrick above par):** Board 1, NS NV, 4S by N making 11 (actual +450), par +420. Expected: delta = +1 IMP.
2. **SC-002 (NS goes down):** Board 2, NS V, 3NT by N making 8 (actual -100), par +600. Expected: delta = -12 IMPs.
3. **SC-003 (EW declaring, matches par):** Board 3, EW NV, 4H by E making 10 (EW +420 = NS -420), par -420. Expected: delta = 0 IMPs.
4. **SC-004 (Passed out):** Board 4, contract null. Expected: actualScore = 0, parScore = 0, delta = 0.
5. **SC-005 (No result):** Board 5, contract = 3NT but result = null. Expected: actualScore = null, delta = null.
6. **Batch calculation:** Test `CalculateDeltas` with a list of 3 boards, verify correct matching by board number.

**Step 5.4: Create `BoardDeltaModelTests`**

File: `tests/BridgeGameCalculator.Tests/Domain/BoardDeltaModelTests.cs`

Verify record equality and serialization round-trip:
- Two `BoardDelta` instances with same values are equal.
- JSON serialization/deserialization produces an equivalent instance (important for the API layer).

### Phase 6: Documentation Updates

**Step 6.1: Update architecture building block view**

File: `docs/architecture/05-building-block-view.md`

Add the Delta Calculator as a building block under Level 2, describing `BridgeScorer` (Shared) and `DeltaCalculationService` (Server).

**Step 6.2: Update crosscutting concepts**

File: `docs/architecture/08-cross-cutting-concepts.md`

Add a note under "Domain Model" about the NS-perspective convention for all scores and deltas.

## File Inventory

### New Files

- `BridgeGameCalculator.sln` -- Solution file linking all projects
- `src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj` -- Shared class library
- `src/BridgeGameCalculator.Shared/Domain/Seat.cs` -- Seat enum (N/E/S/W)
- `src/BridgeGameCalculator.Shared/Domain/Vulnerability.cs` -- Vulnerability enum
- `src/BridgeGameCalculator.Shared/Domain/Strain.cs` -- Strain enum (C/D/H/S/NT)
- `src/BridgeGameCalculator.Shared/Domain/DoubledState.cs` -- Doubled/Redoubled enum
- `src/BridgeGameCalculator.Shared/Domain/Contract.cs` -- Contract value object record
- `src/BridgeGameCalculator.Shared/Domain/Board.cs` -- Board entity record
- `src/BridgeGameCalculator.Shared/Domain/ParResult.cs` -- ParResult + ParContract records (FEAT-002 dependency)
- `src/BridgeGameCalculator.Shared/Domain/BoardDelta.cs` -- BoardDelta record (FEAT-003 output)
- `src/BridgeGameCalculator.Shared/Scoring/BridgeScorer.cs` -- Static scoring + IMP calculation class
- `src/BridgeGameCalculator.Shared/Api/AnalysisResponse.cs` -- API response DTOs
- `src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj` -- ASP.NET Core host project
- `src/BridgeGameCalculator.Server/Program.cs` -- Server entry point with DI registration and endpoint mapping
- `src/BridgeGameCalculator.Server/Services/DeltaCalculationService.cs` -- Delta orchestration service
- `src/BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj` -- Blazor WASM project (placeholder for now)
- `tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj` -- xUnit test project
- `tests/BridgeGameCalculator.Tests/Scoring/BridgeScorerCalculateScoreTests.cs` -- Contract scoring tests
- `tests/BridgeGameCalculator.Tests/Scoring/BridgeScorerImpTests.cs` -- IMP table boundary tests
- `tests/BridgeGameCalculator.Tests/Services/DeltaCalculationServiceTests.cs` -- Integration tests for delta service
- `tests/BridgeGameCalculator.Tests/Domain/BoardDeltaModelTests.cs` -- Record equality and serialization tests

### Modified Files

- `docs/architecture/05-building-block-view.md` -- Add Delta Calculator building block
- `docs/architecture/08-cross-cutting-concepts.md` -- Document NS-perspective scoring convention

## Testing Strategy

### Unit Tests (BridgeScorer)

The `BridgeScorer` tests are the most important tests in this feature. They validate the correctness of WBF duplicate scoring rules, which are the foundation for everything else.

- **Pattern:** `[Theory]` with `[InlineData]` for exhaustive parameterized coverage.
- **Coverage targets:**
  - Every strain (C/D/H/S/NT) at levels that produce different trick-score thresholds.
  - Vulnerable vs non-vulnerable for game bonus, slam bonus, and penalty differences.
  - Undoubled, doubled, and redoubled for both making and going down.
  - Overtricks in all three doubled states, both vulnerabilities.
  - Undertricks 1 through 4+ in all three doubled states, both vulnerabilities (the penalty schedule has breakpoints at undertricks 1, 2-3, and 4+).
  - EW declaring (verifies NS-perspective negation).
- **Edge cases:** Exactly making the contract (0 overtricks), 7NT making 13 (grand slam), 1C doubled making with 6 overtricks (maximum overtrick count).

### Unit Tests (IMP Table)

- Test every boundary value in the WBF IMP table (both lower and upper bound of each row).
- Test values between rows to confirm correct bucketing.
- Test 0 difference = 0 IMPs.
- Test values > 2500 = 24 IMPs.
- Test `CalculateImpDelta` with positive, negative, zero, and null inputs.

### Integration Tests (DeltaCalculationService)

- Construct real `Board` and `ParResult` objects and verify the output `BoardDelta`.
- Map each acceptance scenario from the spec (SC-001 through SC-005) to a named test method.
- Test the batch `CalculateDeltas` method with a mixed list (some boards making, some down, some passed out, some missing results).

### Serialization Tests

- Verify `BoardDelta` survives JSON round-trip via `System.Text.Json`, confirming the API contract.

## Migration Notes

### No database migration needed

The application is stateless (per CLAUDE.md and the impact map). All data lives in memory for the duration of a session. There is no database to migrate.

### Backwards compatibility

This is a greenfield project with no existing users or API consumers. No backwards compatibility concerns.

### Dependencies on unimplemented features

FEAT-003 depends on FEAT-001 (Board parsing) and FEAT-002 (ParResult computation). The plan handles this by:
- Defining the domain models (Board, ParResult) as part of Phase 1 so that FEAT-003 code compiles independently.
- Using TODO comments in the API endpoint where FEAT-001/002 services would be injected.
- All FEAT-003 logic (BridgeScorer, DeltaCalculationService) is fully testable with hand-constructed Board and ParResult objects, without needing the PBN parser or DDS wrapper.

### Build order across features

Features can be implemented in any order because the Shared domain models are defined first. The recommended order is FEAT-001 -> FEAT-002 -> FEAT-003 (matching the data flow), but FEAT-003 scoring logic can be built and tested in parallel with FEAT-001/002 since it has no I/O dependencies.
