# Implementation Plan: DD Analysis Engine (DDS Wrapper)

**Spec:** `docs/specs/FEAT-002-dd-analysis-engine.md`
**ADR:** `docs/architecture/adr/001-technology-stack.md`
**Created:** 2026-04-08
**Status:** Draft

## Summary

Wrap Bo Haglund's DDS C/C++ library via P/Invoke to compute double-dummy trick tables and par scores for bridge boards. The implementation adds a `DdsInterop` static class for native marshaling, a `DdsAnalysisService` behind an `IDdsAnalysisService` interface, domain models in the Shared project, and a POST endpoint at `/api/sessions/{id}/analyze`. Per-board error isolation ensures one bad board never aborts the session.

## Key Design Decisions

1. **Use `CalcAllTablesPBN` + `DealerPar` (not `Par` or `CalcParPBN`).** `CalcAllTablesPBN` is the batch API that computes DD trick tables for up to 200 boards in one call, maximizing DDS's internal multi-threading. `DealerPar` is preferred over `Par` because it takes the dealer seat as input and returns results from both sides' perspectives, which maps cleanly to the spec's requirement for NS-perspective par scores. `CalcParPBN` bundles both steps but does not support batch operation, so it would be slower for sessions.

2. **Domain models live in `BridgeGameCalculator.Shared`.** `DdTable`, `DdResult`, `ParResult`, and `ParContract` are pure domain types needed by both server (to produce) and client (to display). P/Invoke structs live in the Server project only — they are marshaling concerns, not domain concerns.

3. **P/Invoke structs are separate from domain models.** The DDS C structs have fixed-size arrays and specific layouts that must not leak into the domain. `DdsInterop` owns the C-compatible structs; `DdsAnalysisService` maps them to domain objects.

4. **`IDdsAnalysisService` interface for testability.** The endpoint and all consumers depend on the interface, not the concrete class. Unit tests use a mock/fake; integration tests hit the real DDS library.

5. **Per-board error isolation via `BoardAnalysisResult`.** The service returns a `BoardAnalysisResult` per board that is either a success (with `DdTable` + `ParResult`) or a failure (with error message). The endpoint aggregates these into a `SessionAnalysisResponse`. A DDS error on board 5 does not prevent board 6 from being analyzed.

6. **Single-board analysis reuses the same service.** For FEAT-006 (single hand entry), the same `IDdsAnalysisService.AnalyzeBoardAsync` method is called. No separate code path needed.

7. **Native library loaded at startup with health check.** The `DdsInterop.SetMaxThreads(0)` call is made during application startup (in `Program.cs`) to verify the native library is loadable. Failure produces a clear fatal error per EC-1 in the spec.

8. **DDS library ships in `src/BridgeGameCalculator.Server/native/`.** The `.dll` (Windows) and `.so` (Linux) are placed here and copied to the output directory via a build target in the `.csproj`. The `DllImport` uses just the library name (`"dds"`) and relies on the runtime's native library resolution.

## Implementation Steps

### Phase 1: Domain Models (Shared Project)

#### Step 1.1: Add enums to Shared project

**File:** `src/BridgeGameCalculator.Shared/Models/Enums.cs`

If FEAT-001 has already defined `Seat`, `Strain`, and `Vulnerability` enums, skip this step. If not, create them here so both features use the same definitions.

```csharp
namespace BridgeGameCalculator.Shared.Models;

public enum Seat { North = 0, East = 1, South = 2, West = 3 }

public enum Strain { Spades = 0, Hearts = 1, Diamonds = 2, Clubs = 3, NoTrump = 4 }

public enum Vulnerability { None = 0, Both = 1, NorthSouth = 2, EastWest = 3 }

public enum DoubleStatus { Undoubled = 0, Doubled = 1 }
```

Note on `Strain` ordering: DDS internally uses the order Spades=0, Hearts=1, Diamonds=2, Clubs=3, NT=4. Match this to avoid mapping bugs.

Note on `Vulnerability` encoding: DDS uses 0=None, 1=Both, 2=NS, 3=EW. Match this exactly.

#### Step 1.2: Add `DdResult` value object

**File:** `src/BridgeGameCalculator.Shared/Models/DdResult.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public sealed record DdResult(Seat Declarer, Strain Strain, int Tricks)
{
    public int Tricks { get; init; } = Tricks >= 0 && Tricks <= 13
        ? Tricks
        : throw new ArgumentOutOfRangeException(nameof(Tricks), "Tricks must be 0-13.");
}
```

#### Step 1.3: Add `DdTable` domain object

**File:** `src/BridgeGameCalculator.Shared/Models/DdTable.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public sealed class DdTable
{
    public int BoardNumber { get; init; }
    public IReadOnlyList<DdResult> Results { get; init; }

    public DdTable(int boardNumber, IReadOnlyList<DdResult> results)
    {
        BoardNumber = boardNumber;
        Results = results.Count == 20
            ? results
            : throw new ArgumentException("DdTable must have exactly 20 results.", nameof(results));
    }

    public int GetTricks(Seat declarer, Strain strain)
        => Results.First(r => r.Declarer == declarer && r.Strain == strain).Tricks;
}
```

#### Step 1.4: Add `ParContract` and `ParResult` domain objects

**File:** `src/BridgeGameCalculator.Shared/Models/ParContract.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public sealed record ParContract(int Level, Strain Strain, Seat Declarer, DoubleStatus Doubled);
```

**File:** `src/BridgeGameCalculator.Shared/Models/ParResult.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public sealed class ParResult
{
    public int BoardNumber { get; init; }
    public int ParScore { get; init; }   // From NS perspective
    public IReadOnlyList<ParContract> ParContracts { get; init; }

    public static ParResult PassedOut(int boardNumber)
        => new() { BoardNumber = boardNumber, ParScore = 0, ParContracts = [] };
}
```

#### Step 1.5: Add `BoardAnalysisResult` (success/failure envelope)

**File:** `src/BridgeGameCalculator.Shared/Models/BoardAnalysisResult.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public sealed class BoardAnalysisResult
{
    public int BoardNumber { get; init; }
    public bool IsSuccess { get; init; }
    public DdTable? DdTable { get; init; }
    public ParResult? ParResult { get; init; }
    public string? ErrorMessage { get; init; }

    public static BoardAnalysisResult Success(DdTable ddTable, ParResult parResult)
        => new()
        {
            BoardNumber = ddTable.BoardNumber,
            IsSuccess = true,
            DdTable = ddTable,
            ParResult = parResult
        };

    public static BoardAnalysisResult Failure(int boardNumber, string errorMessage)
        => new()
        {
            BoardNumber = boardNumber,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
```

---

### Phase 2: DDS P/Invoke Layer (Server Project)

#### Step 2.1: Create native library directory and build target

**Directory:** `src/BridgeGameCalculator.Server/native/`

Place compiled `dds.dll` (Windows x64) and/or `libdds.so` (Linux x64) here.

**Modify:** `src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj`

Add the following `ItemGroup` to copy native libraries to the output directory:

```xml
<ItemGroup>
  <None Include="native/**/*" CopyToOutputDirectory="PreserveNewest" Link="%(Filename)%(Extension)" />
</ItemGroup>
```

#### Step 2.2: Create DDS P/Invoke struct definitions

**File:** `src/BridgeGameCalculator.Server/Dds/DdsStructs.cs`

These structs mirror the DDS C structs exactly for P/Invoke marshaling. The key DDS constants and struct layouts:

```csharp
using System.Runtime.InteropServices;

namespace BridgeGameCalculator.Server.Dds;

/// <summary>
/// Constants matching DDS library definitions.
/// </summary>
internal static class DdsConstants
{
    public const int MaxNoOfBoards = 200;
    public const int DdsSuits = 5;    // S, H, D, C, NT
    public const int DdsHands = 4;    // N, E, S, W
}

/// <summary>
/// A single deal in PBN format for DD table calculation.
/// Maps to DDS struct ddTableDealPBN.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTableDealPbn
{
    /// <summary>
    /// PBN deal string, e.g., "N:QJ6.K532.J85.A98 874.A4.K76.QJT52 ..."
    /// DDS expects a fixed 80-char buffer.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string Cards;
}

/// <summary>
/// Batch of deals for CalcAllTablesPBN.
/// Maps to DDS struct ddTableDealsPBN.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTableDealsPbn
{
    public int NoOfTables;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.MaxNoOfBoards * 5)]
    public DdTableDealPbn[] Deals;
}

/// <summary>
/// DD trick results for a single deal: 5 strains x 4 hands.
/// Maps to DDS struct ddTableResults.
/// Layout: resTable[5][4] where [strain][hand] = tricks.
/// DDS strain order: Spades=0, Hearts=1, Diamonds=2, Clubs=3, NT=4.
/// DDS hand order: North=0, East=1, South=2, West=3.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTableResults
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public int[] ResTable; // resTable[5][4] flattened: [strain * 4 + hand]
}

/// <summary>
/// Batch results from CalcAllTablesPBN.
/// Maps to DDS struct ddTablesRes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTablesRes
{
    public int NoOfBoards;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.MaxNoOfBoards * 5)]
    public DdTableResults[] Results;
}

/// <summary>
/// Par score results from DealerPar.
/// Maps to DDS struct parResultsDealer.
/// Contains two strings: one for NS par, one for EW par.
/// Each string is up to 256 chars.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct ParResultsDealer
{
    /// <summary>
    /// Par score as an integer for each side.
    /// Index 0 = NS, Index 1 = EW.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] Score;

    /// <summary>
    /// Par contract string for each side (e.g., "4S-N" or "4S*-N").
    /// Index 0 = NS, Index 1 = EW.
    /// Each is a fixed 128-char buffer.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Number;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Contracts0;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Contracts1;
}

/// <summary>
/// Scheduling mode indicators for CalcAllTablesPBN.
/// Maps to the int[5] trumpFilter and int[5] mode arrays.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTableDealsPbnTrumpFilter
{
    /// <summary>
    /// 5-element array: one per strain (S, H, D, C, NT).
    /// 0 = calculate this strain, 1 = skip.
    /// For par calculation, set all to 0 (calculate everything).
    /// </summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public int[] Filter;

    public static DdTableDealsPbnTrumpFilter CalculateAll()
        => new() { Filter = [0, 0, 0, 0, 0] };
}
```

**Important marshaling notes (documented in comments in the file):**

- The `DdTableDealsPbn.Deals` array is sized at `MAXNOOFBOARDS * 5`. DDS documentation says "multiply by 5 for PBN deals." In practice, the array is `MAXNOOFBOARDS` entries. Verify against the actual header: if the DDS version uses `MAXNOOFBOARDS` without the `* 5` multiplier, adjust. The `* 5` appears in some DDS versions because `ddTableDealsPBN` uses a `[MAXNOOFBOARDS * 5]` size.
- The `DdTablesRes.Results` array similarly uses `MAXNOOFBOARDS * 5`.
- `DdTableResults.ResTable` is a flattened 2D array: `resTable[strain][hand]` becomes `resTable[strain * 4 + hand]`.

#### Step 2.3: Create `DdsInterop` static class with P/Invoke declarations

**File:** `src/BridgeGameCalculator.Server/Dds/DdsInterop.cs`

```csharp
using System.Runtime.InteropServices;

namespace BridgeGameCalculator.Server.Dds;

/// <summary>
/// P/Invoke declarations for Bo Haglund's DDS library.
/// All methods return an int status code: 1 = success, negative = error.
/// </summary>
internal static class DdsInterop
{
    private const string DdsLibrary = "dds";

    /// <summary>
    /// Set maximum number of threads DDS may use internally.
    /// Pass 0 to let DDS auto-detect based on CPU cores.
    /// Also serves as a library load health check.
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall)]
    public static extern void SetMaxThreads(int userThreads);

    /// <summary>
    /// Batch DD table calculation for multiple deals in PBN format.
    /// </summary>
    /// <param name="dealsp">Input: batch of PBN deals.</param>
    /// <param name="resp">Output: batch of DD table results.</param>
    /// <param name="mode">Array of 5 ints, one per strain. Not used when trumpFilter is all zeros.</param>
    /// <param name="trumpFilter">Array of 5 ints: 0 = calculate, 1 = skip. Use all zeros.</param>
    /// <param name="parResults">Output: par results (one per deal). Pass IntPtr.Zero if not needed.</param>
    /// <returns>Status code: 1 = success.</returns>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall)]
    public static extern int CalcAllTablesPBN(
        ref DdTableDealsPbn dealsp,
        int mode,
        int[] trumpFilter,
        ref DdTablesRes resp,
        ref AllParResults presp);

    /// <summary>
    /// Calculate par score for a single deal given the DD table results.
    /// Uses dealer-relative par calculation.
    /// </summary>
    /// <param name="tablep">Input: DD table results for one deal.</param>
    /// <param name="presp">Output: par results.</param>
    /// <param name="dealer">Dealer seat: 0=N, 1=E, 2=S, 3=W.</param>
    /// <param name="vulnerable">Vulnerability: 0=None, 1=Both, 2=NS, 3=EW.</param>
    /// <returns>Status code: 1 = success.</returns>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall)]
    public static extern int DealerPar(
        ref DdTableResults tablep,
        ref ParResultsDealer presp,
        int dealer,
        int vulnerable);

    /// <summary>
    /// Calculate DD table for a single PBN deal (non-batch).
    /// Used for single-hand analysis (FEAT-006).
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall)]
    public static extern int CalcDDtablePBN(
        DdTableDealPbn tableDeal,
        ref DdTableResults tablep);

    /// <summary>
    /// Convert a DDS error code to a human-readable message.
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall)]
    public static extern void ErrorMessage(int code, [MarshalAs(UnmanagedType.LPStr)] StringBuilder message);
}

/// <summary>
/// AllParResults struct for CalcAllTablesPBN output.
/// Maps to DDS struct allParResults.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AllParResults
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.MaxNoOfBoards * 5)]
    public ParResultsDealer[] PresResults;
}
```

**Calling convention note:** DDS on Windows uses `__stdcall` by default. On Linux, calling convention is irrelevant (all `cdecl`). Use `CallingConvention.StdCall` and override with `CallingConvention.Cdecl` on Linux if needed via a build constant. Start with `StdCall` for the primary Windows target.

#### Step 2.4: Create `DdsErrorHelper` utility

**File:** `src/BridgeGameCalculator.Server/Dds/DdsErrorHelper.cs`

```csharp
using System.Text;

namespace BridgeGameCalculator.Server.Dds;

/// <summary>
/// Translates DDS return codes into human-readable error messages.
/// </summary>
internal static class DdsErrorHelper
{
    private const int DdsReturnNoFault = 1;

    public static bool IsSuccess(int returnCode) => returnCode == DdsReturnNoFault;

    public static string GetErrorMessage(int returnCode)
    {
        if (IsSuccess(returnCode))
            return string.Empty;

        var sb = new StringBuilder(80);
        DdsInterop.ErrorMessage(returnCode, sb);
        var message = sb.ToString().Trim();
        return string.IsNullOrEmpty(message)
            ? $"DDS error code: {returnCode}"
            : message;
    }
}
```

---

### Phase 3: Analysis Service

#### Step 3.1: Define `IDdsAnalysisService` interface

**File:** `src/BridgeGameCalculator.Server/Services/IDdsAnalysisService.cs`

```csharp
using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Server.Services;

/// <summary>
/// Analyzes bridge boards using the DDS library.
/// </summary>
public interface IDdsAnalysisService
{
    /// <summary>
    /// Analyze a batch of boards (full session). Uses the DDS batch API for performance.
    /// Per-board failures are captured in the result, not thrown.
    /// </summary>
    Task<IReadOnlyList<BoardAnalysisResult>> AnalyzeSessionAsync(
        IReadOnlyList<Board> boards,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze a single board. Suitable for single-hand entry (FEAT-006).
    /// </summary>
    Task<BoardAnalysisResult> AnalyzeBoardAsync(
        Board board,
        CancellationToken cancellationToken = default);
}
```

#### Step 3.2: Implement `DdsAnalysisService`

**File:** `src/BridgeGameCalculator.Server/Services/DdsAnalysisService.cs`

This is the central implementation class. Key responsibilities:

1. **`AnalyzeSessionAsync`**: Convert `Board` list to `DdTableDealsPbn`, call `CalcAllTablesPBN`, then call `DealerPar` per board to get par scores. Wrap each board's processing in a try/catch for per-board error isolation.

2. **`AnalyzeBoardAsync`**: Convert a single `Board` to `DdTableDealPbn`, call `CalcDDtablePBN`, then `DealerPar`.

3. **PBN deal string construction**: Convert `Board.Hands` to the DDS PBN format: `"N:SAKQ.HAKQ.DAKQ.CAKQ SAKQ..."`. DDS expects `"<first>:<suit>.<suit>.<suit>.<suit> <suit>.<suit>.<suit>.<suit> ..."` where first = dealer direction. Since the deal string from the PBN file is already in this format (per FEAT-001), pass it through directly.

4. **Passed-out board short-circuit**: Per EC-3 in the spec, if a board is passed out (no contract possible or all four hands sum to a passed-out scenario), still compute the DD table and par. The DDS library handles this correctly — a truly passed-out board at the DD level means par is 0. Do NOT skip the DDS call for passed-out boards, because "passed out" in the PBN means the actual result was a pass-out, not that the DD par is zero. The DD par should still be computed.

5. **Thread offloading**: The DDS library is CPU-intensive and blocks the calling thread. Wrap native calls in `Task.Run` to avoid blocking the ASP.NET request thread.

```csharp
namespace BridgeGameCalculator.Server.Services;

public sealed class DdsAnalysisService : IDdsAnalysisService
{
    private readonly ILogger<DdsAnalysisService> _logger;

    public DdsAnalysisService(ILogger<DdsAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<BoardAnalysisResult>> AnalyzeSessionAsync(
        IReadOnlyList<Board> boards, CancellationToken cancellationToken)
    {
        // 1. Build DdTableDealsPbn from boards
        // 2. Call CalcAllTablesPBN on a background thread
        // 3. For each board, call DealerPar to get par score
        // 4. Map results to domain objects, catching per-board errors
        // Return list of BoardAnalysisResult (success or failure per board)
    }

    public async Task<BoardAnalysisResult> AnalyzeBoardAsync(
        Board board, CancellationToken cancellationToken)
    {
        // 1. Build DdTableDealPbn from board
        // 2. Call CalcDDtablePBN on a background thread
        // 3. Call DealerPar
        // 4. Map to domain objects
        // 5. Return BoardAnalysisResult.Success or .Failure
    }

    // --- Private helpers ---

    private static DdTableDealPbn ToDdsDeal(Board board)
    {
        // Convert Board.Hands to PBN deal string for DDS.
        // The Board already stores hands in PBN format from FEAT-001.
        // DDS expects: "N:s.h.d.c s.h.d.c s.h.d.c s.h.d.c"
        // Return new DdTableDealPbn { Cards = board.Hands.ToPbnDealString() };
    }

    private static DdTable MapDdTableResults(
        int boardNumber, DdTableResults ddsResults)
    {
        // DDS layout: resTable[strain * 4 + hand]
        // Strain order: S=0, H=1, D=2, C=3, NT=4
        // Hand order:   N=0, E=1, S=2, W=3
        var results = new List<DdResult>(20);
        for (int strain = 0; strain < 5; strain++)
        {
            for (int hand = 0; hand < 4; hand++)
            {
                int tricks = ddsResults.ResTable[strain * 4 + hand];
                results.Add(new DdResult(
                    (Seat)hand, (Strain)strain, tricks));
            }
        }
        return new DdTable(boardNumber, results);
    }

    private static ParResult MapParResults(
        int boardNumber, ParResultsDealer parResults)
    {
        // parResults.Score[0] = NS par score
        // parResults.Contracts0 = NS par contracts string (e.g., "4S*-N")
        // Parse the contracts string into ParContract objects
        int parScore = parResults.Score[0]; // NS perspective
        var contracts = ParseParContracts(parResults.Contracts0);
        return new ParResult
        {
            BoardNumber = boardNumber,
            ParScore = parScore,
            ParContracts = contracts
        };
    }

    private static IReadOnlyList<ParContract> ParseParContracts(string contractsString)
    {
        // DDS DealerPar contract format: "4S-N", "4S*-N", "4S*-NS" (multiple declarers)
        // Parse into ParContract objects.
        // If string is empty or "pass", return empty list (passed out).
    }

    private static int MapVulnerability(Vulnerability vulnerability)
    {
        // DDS vulnerability encoding: 0=None, 1=Both, 2=NS, 3=EW
        return vulnerability switch
        {
            Vulnerability.None => 0,
            Vulnerability.Both => 1,
            Vulnerability.NorthSouth => 2,
            Vulnerability.EastWest => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(vulnerability))
        };
    }

    private static int MapDealer(Seat dealer)
    {
        // DDS dealer encoding: 0=N, 1=E, 2=S, 3=W
        return (int)dealer;
    }
}
```

#### Step 3.3: Add `DdsParContractParser` helper

**File:** `src/BridgeGameCalculator.Server/Dds/DdsParContractParser.cs`

Dedicated parser for the DDS `DealerPar` contract output strings. The DDS format is documented as:

- `"4S-N"` = 4 Spades by North, undoubled
- `"4S*-N"` = 4 Spades by North, doubled
- `"4S-NS"` = 4 Spades by North or South
- `"pass"` = passed out

The parser must handle:

- Level: single digit 1-7
- Strain: S, H, D, C, NT (case-insensitive)
- Optional `*` for doubled
- Declarer(s): N, E, S, W, or combinations like NS, EW
- Multiple contracts separated by commas

```csharp
namespace BridgeGameCalculator.Server.Dds;

internal static class DdsParContractParser
{
    public static IReadOnlyList<ParContract> Parse(string contractsString)
    {
        // Returns empty list for null/empty/"pass"
        // Splits on comma for multiple contracts
        // For each token: extract level, strain, doubled flag, declarer(s)
        // If multiple declarers (e.g., "NS"), emit one ParContract per declarer
    }
}
```

#### Step 3.4: Register service in DI

**Modify:** `src/BridgeGameCalculator.Server/Program.cs`

```csharp
// --- Service registration ---
builder.Services.AddSingleton<IDdsAnalysisService, DdsAnalysisService>();

// --- DDS library health check at startup ---
try
{
    DdsInterop.SetMaxThreads(0);
}
catch (DllNotFoundException ex)
{
    // Log fatal error and exit
    Log.Fatal("DD solver library could not be loaded. Ensure dds.dll/libdds.so is in the application directory. Error: {Error}", ex.Message);
    return 1;
}
```

Register as **singleton** because `DdsAnalysisService` is stateless and DDS manages its own internal thread pool. Creating multiple instances would waste resources.

---

### Phase 4: API Endpoint

#### Step 4.1: Create API DTOs

**File:** `src/BridgeGameCalculator.Shared/Dtos/SessionAnalysisResponse.cs`

```csharp
namespace BridgeGameCalculator.Shared.Dtos;

public sealed class SessionAnalysisResponse
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<BoardAnalysisResultDto> Boards { get; init; }
    public int TotalBoards => Boards.Count;
    public int SuccessfulBoards => Boards.Count(b => b.IsSuccess);
    public int FailedBoards => Boards.Count(b => !b.IsSuccess);
}

public sealed class BoardAnalysisResultDto
{
    public required int BoardNumber { get; init; }
    public required bool IsSuccess { get; init; }
    public DdTableDto? DdTable { get; init; }
    public ParResultDto? ParResult { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class DdTableDto
{
    public required int BoardNumber { get; init; }
    /// <summary>
    /// 20 entries: tricks for each (declarer, strain) pair.
    /// </summary>
    public required IReadOnlyList<DdResultDto> Results { get; init; }
}

public sealed class DdResultDto
{
    public required string Declarer { get; init; }  // "N", "E", "S", "W"
    public required string Strain { get; init; }    // "S", "H", "D", "C", "NT"
    public required int Tricks { get; init; }
}

public sealed class ParResultDto
{
    public required int BoardNumber { get; init; }
    public required int ParScore { get; init; }
    public required IReadOnlyList<ParContractDto> ParContracts { get; init; }
}

public sealed class ParContractDto
{
    public required int Level { get; init; }
    public required string Strain { get; init; }
    public required string Declarer { get; init; }
    public required string Doubled { get; init; }   // "Undoubled" or "Doubled"
}
```

#### Step 4.2: Create the analysis endpoint

**Modify:** `src/BridgeGameCalculator.Server/Program.cs` (or a separate endpoint class if the project uses endpoint grouping)

```csharp
app.MapPost("/api/sessions/{id}/analyze", async (
    string id,
    IDdsAnalysisService analysisService,
    ISessionStore sessionStore,    // From FEAT-001: in-memory session storage
    CancellationToken cancellationToken) =>
{
    var session = sessionStore.Get(id);
    if (session is null)
        return Results.NotFound(new { error = $"Session '{id}' not found." });

    var results = await analysisService.AnalyzeSessionAsync(
        session.Boards, cancellationToken);

    var response = MapToResponse(id, results);
    return Results.Ok(response);
});
```

The endpoint:
- Retrieves the `Session` (uploaded boards) from the in-memory session store created by FEAT-001
- Calls `AnalyzeSessionAsync` with all boards
- Maps domain results to DTOs
- Returns 200 with the full result set (including per-board failures)
- Returns 404 if the session ID is invalid

#### Step 4.3: Create a single-board analysis endpoint

**Modify:** `src/BridgeGameCalculator.Server/Program.cs`

```csharp
app.MapPost("/api/sessions/{id}/boards/{boardNumber:int}/analyze", async (
    string id,
    int boardNumber,
    IDdsAnalysisService analysisService,
    ISessionStore sessionStore,
    CancellationToken cancellationToken) =>
{
    var session = sessionStore.Get(id);
    if (session is null)
        return Results.NotFound(new { error = $"Session '{id}' not found." });

    var board = session.Boards.FirstOrDefault(b => b.BoardNumber == boardNumber);
    if (board is null)
        return Results.NotFound(new { error = $"Board {boardNumber} not found in session." });

    var result = await analysisService.AnalyzeBoardAsync(board, cancellationToken);
    var dto = MapToBoardDto(result);
    return Results.Ok(dto);
});
```

---

### Phase 5: Tests

#### Step 5.1: Create test helper / fake implementation

**File:** `tests/BridgeGameCalculator.Tests/Fakes/FakeDdsAnalysisService.cs`

```csharp
namespace BridgeGameCalculator.Tests.Fakes;

/// <summary>
/// Fake IDdsAnalysisService for unit testing endpoint and consumer logic
/// without calling the real DDS library.
/// </summary>
public sealed class FakeDdsAnalysisService : IDdsAnalysisService
{
    private readonly Dictionary<int, BoardAnalysisResult> _results = new();
    private readonly BoardAnalysisResult? _defaultResult;

    public FakeDdsAnalysisService(BoardAnalysisResult? defaultResult = null)
    {
        _defaultResult = defaultResult;
    }

    public void SetResult(int boardNumber, BoardAnalysisResult result)
        => _results[boardNumber] = result;

    public Task<IReadOnlyList<BoardAnalysisResult>> AnalyzeSessionAsync(
        IReadOnlyList<Board> boards, CancellationToken ct)
    {
        var results = boards.Select(b =>
            _results.TryGetValue(b.BoardNumber, out var r) ? r
            : _defaultResult ?? BoardAnalysisResult.Failure(b.BoardNumber, "No fake result configured"))
            .ToList();
        return Task.FromResult<IReadOnlyList<BoardAnalysisResult>>(results);
    }

    public Task<BoardAnalysisResult> AnalyzeBoardAsync(
        Board board, CancellationToken ct)
    {
        var result = _results.TryGetValue(board.BoardNumber, out var r) ? r
            : _defaultResult ?? BoardAnalysisResult.Failure(board.BoardNumber, "No fake result configured");
        return Task.FromResult(result);
    }
}
```

#### Step 5.2: Create test data builders

**File:** `tests/BridgeGameCalculator.Tests/TestData/BoardBuilder.cs`

Provides fluent methods to create `Board` objects with known hands for testing:

```csharp
namespace BridgeGameCalculator.Tests.TestData;

internal static class BoardBuilder
{
    /// <summary>
    /// Creates a board with a known hand where par is 4S by North, +420 NS.
    /// </summary>
    public static Board KnownPar4SpadesBoard() => new()
    {
        BoardNumber = 1,
        Dealer = Seat.North,
        Vulnerability = Vulnerability.None,
        Hands = new Hands { /* known 52-card deal */ }
    };

    /// <summary>
    /// Creates a board with a trivially passed-out deal (par = 0).
    /// </summary>
    public static Board PassedOutBoard() => new()
    {
        BoardNumber = 2,
        Dealer = Seat.East,
        Vulnerability = Vulnerability.Both,
        Hands = new Hands { /* balanced deal with no game */ }
    };
}
```

#### Step 5.3: Domain model unit tests

**File:** `tests/BridgeGameCalculator.Tests/Models/DdTableTests.cs`

Test:
- `DdTable` constructor rejects lists with != 20 results
- `DdTable.GetTricks` returns correct value for given (declarer, strain)
- `DdResult` rejects tricks outside 0-13
- `ParResult.PassedOut` returns score=0 and empty contracts
- `BoardAnalysisResult.Success` and `.Failure` factory methods

#### Step 5.4: `DdsParContractParser` unit tests

**File:** `tests/BridgeGameCalculator.Tests/Dds/DdsParContractParserTests.cs`

Test cases:
- `"4S-N"` parses to level=4, strain=Spades, declarer=North, undoubled
- `"4S*-N"` parses to doubled
- `"3NT-E"` parses NT strain correctly
- `"4S-NS"` produces two ParContracts (one for N, one for S)
- `"pass"` or `""` returns empty list
- `"4S-N,3NT-E"` returns two contracts (multiple comma-separated)
- Invalid input returns empty list (does not throw)

#### Step 5.5: `DdsAnalysisService` unit tests (with mocked interop)

**File:** `tests/BridgeGameCalculator.Tests/Services/DdsAnalysisServiceTests.cs`

These tests verify the service's mapping and error-handling logic. Since P/Invoke cannot be easily mocked, extract the mapping logic into testable private/internal methods and test those, OR use `[InternalsVisibleTo]` to access the mapping helpers.

Add to Server `.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="BridgeGameCalculator.Tests" />
</ItemGroup>
```

Test cases:
- `MapDdTableResults` correctly maps the flattened DDS array to 20 `DdResult` objects
- `MapParResults` correctly extracts NS par score
- `MapVulnerability` maps each enum value to the correct DDS integer
- `MapDealer` maps each Seat to the correct integer

#### Step 5.6: API endpoint tests

**File:** `tests/BridgeGameCalculator.Tests/Endpoints/AnalyzeEndpointTests.cs`

Use `WebApplicationFactory<Program>` to test the endpoint with `FakeDdsAnalysisService` injected:

Test cases:
- POST `/api/sessions/{id}/analyze` with valid session returns 200 with analysis results
- POST with unknown session ID returns 404
- Response includes both successful and failed board results
- Single-board endpoint returns 200 for valid board
- Single-board endpoint returns 404 for unknown board number
- Cancellation token is respected

#### Step 5.7: Integration test (conditional, requires DDS library)

**File:** `tests/BridgeGameCalculator.Tests/Integration/DdsIntegrationTests.cs`

These tests call the real DDS library and verify correctness against known reference hands. Guard with a test category/trait so they only run when the DDS native library is available:

```csharp
[Trait("Category", "Integration")]
public class DdsIntegrationTests
{
    [SkippableFact] // Skip if DDS library not available
    public async Task AnalyzeBoard_KnownHand_ReturnsExpectedPar()
    {
        // Use a well-known bridge hand with published DD results
        // Verify par score matches expected value
    }

    [SkippableFact]
    public async Task AnalyzeSession_28Boards_CompletesWithin30Seconds()
    {
        // Performance test: 28 boards < 30 seconds
    }
}
```

---

## File Inventory

### New Files

| File | Purpose |
|------|---------|
| `src/BridgeGameCalculator.Shared/Models/Enums.cs` | `Seat`, `Strain`, `Vulnerability`, `DoubleStatus` enums (if not already created by FEAT-001) |
| `src/BridgeGameCalculator.Shared/Models/DdResult.cs` | DD result value object (declarer, strain, tricks) |
| `src/BridgeGameCalculator.Shared/Models/DdTable.cs` | DD table domain object (20 results per board) |
| `src/BridgeGameCalculator.Shared/Models/ParContract.cs` | Par contract value object |
| `src/BridgeGameCalculator.Shared/Models/ParResult.cs` | Par result domain object (score + contracts) |
| `src/BridgeGameCalculator.Shared/Models/BoardAnalysisResult.cs` | Success/failure envelope for per-board analysis |
| `src/BridgeGameCalculator.Shared/Dtos/SessionAnalysisResponse.cs` | API response DTOs (session, board, DD table, par) |
| `src/BridgeGameCalculator.Server/Dds/DdsStructs.cs` | C-compatible P/Invoke struct definitions |
| `src/BridgeGameCalculator.Server/Dds/DdsInterop.cs` | `[DllImport]` declarations for DDS functions |
| `src/BridgeGameCalculator.Server/Dds/DdsErrorHelper.cs` | DDS error code to message translation |
| `src/BridgeGameCalculator.Server/Dds/DdsParContractParser.cs` | Parser for DDS `DealerPar` contract output strings |
| `src/BridgeGameCalculator.Server/Services/IDdsAnalysisService.cs` | Analysis service interface |
| `src/BridgeGameCalculator.Server/Services/DdsAnalysisService.cs` | Concrete DDS-backed analysis service |
| `src/BridgeGameCalculator.Server/native/` | Directory for DDS native binaries (`dds.dll`, `libdds.so`) |
| `tests/BridgeGameCalculator.Tests/Fakes/FakeDdsAnalysisService.cs` | Fake service for unit testing |
| `tests/BridgeGameCalculator.Tests/TestData/BoardBuilder.cs` | Test data builders for known hands |
| `tests/BridgeGameCalculator.Tests/Models/DdTableTests.cs` | Domain model unit tests |
| `tests/BridgeGameCalculator.Tests/Dds/DdsParContractParserTests.cs` | Par contract parser unit tests |
| `tests/BridgeGameCalculator.Tests/Services/DdsAnalysisServiceTests.cs` | Service mapping/logic unit tests |
| `tests/BridgeGameCalculator.Tests/Endpoints/AnalyzeEndpointTests.cs` | API endpoint integration tests |
| `tests/BridgeGameCalculator.Tests/Integration/DdsIntegrationTests.cs` | Real DDS library integration tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj` | Add native library copy target, `InternalsVisibleTo` |
| `src/BridgeGameCalculator.Server/Program.cs` | Register `DdsAnalysisService`, add startup health check, add API endpoints |

---

## Testing Strategy

### Unit Tests (no DDS library required)

- **Domain models**: Validate construction constraints (20 results, tricks range, passed-out factory).
- **Par contract parser**: Full coverage of DDS output format variations (undoubled, doubled, multi-declarer, multi-contract, pass, empty, malformed).
- **Service mapping helpers**: Verify DDS struct-to-domain-object mapping is correct for all 20 strain/hand combinations and vulnerability/dealer encoding.
- **API endpoints**: Use `WebApplicationFactory` with `FakeDdsAnalysisService` to test HTTP layer (routing, status codes, response shape, error handling).

### Integration Tests (DDS library required)

- **Known hands**: Verify par scores against published reference results. Use at least 3 reference hands: a game hand, a slam hand, and a part-score hand.
- **Performance**: 28-board session completes within 30 seconds.
- **Error isolation**: Include one invalid board in a batch of valid boards; verify the invalid board fails and all others succeed.
- **Library load failure**: Test that a missing DDS library produces the expected startup error (this can be tested by temporarily renaming the library file).

### Test Patterns

- Use `[Trait("Category", "Integration")]` to separate integration tests from unit tests.
- Use `Xunit.SkippableFact` (or a custom check) to skip integration tests when the DDS library is not present.
- All unit tests use `FakeDdsAnalysisService` — never call the real DDS library in unit tests.
- Use `[InternalsVisibleTo]` to test internal mapping helpers directly.

---

## Migration Notes

- **No database migration required.** The application is stateless; all data lives in memory.
- **FEAT-001 dependency**: This plan assumes `Board`, `Hands`, `Session`, and the in-memory session store exist from FEAT-001. If FEAT-001 is not yet implemented, the domain models from Phase 1 can still be built, but the API endpoints (Phase 4) and the `DdsAnalysisService.ToDdsDeal` method (Phase 3) require the FEAT-001 `Board` shape. Coordinate on the `Hands.ToPbnDealString()` method — it must produce the DDS-expected format: `"N:s.h.d.c s.h.d.c s.h.d.c s.h.d.c"`.
- **DDS library compilation**: The DDS library must be compiled separately from the C++ source. For MVP, compile for Windows x64 only. Linux support can follow. The compiled binary is checked into `src/BridgeGameCalculator.Server/native/` (or fetched via a build script — but for MVP, checking in the binary is simpler).
- **DDS license**: The DDS library uses the Apache 2.0 license. Include the license file in the `native/` directory and add attribution in the application's about/credits section.
- **Backwards compatibility**: Not applicable — greenfield project, no existing users or data.

## P/Invoke Marshaling Risks

The DDS C structs use fixed-size arrays and specific memory layouts. The most likely source of bugs:

1. **Array size mismatch in `DdTableDealsPBN` and `DdTablesRes`**: Some DDS versions use `MAXNOOFBOARDS` (200), others use `MAXNOOFBOARDS * 5` (1000). Verify against the exact DDS version being compiled. The plan uses `MAXNOOFBOARDS * 5` to match the most common DDS source layout, but this must be confirmed.

2. **`DdTableResults.ResTable` indexing**: The 2D array `resTable[5][4]` is stored row-major in C: `resTable[strain][hand]`. In the flattened C# array, element `[i]` corresponds to `resTable[i / 4][i % 4]` = `(strain=i/4, hand=i%4)`. This is equivalent to `resTable[strain * 4 + hand]`.

3. **`ParResultsDealer` string fields**: The DDS struct contains two fixed-size `char[128]` arrays for contract strings. The exact layout must be verified against the header. If the struct uses a nested `char[2][128]` array, the marshaling must be two separate `ByValTStr` fields (as shown above).

4. **CallingConvention**: Windows DDS builds typically use `__stdcall`. Verify the compiled library's export table matches. If using a Linux `.so`, switch to `CallingConvention.Cdecl`.

**Mitigation**: Write a single "smoke test" integration test that calls `SetMaxThreads(0)` and `CalcDDtablePBN` on one known hand. If this test passes, the struct layouts are correct. Run this test first before writing more complex tests.
