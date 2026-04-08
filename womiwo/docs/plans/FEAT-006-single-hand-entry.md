# Implementation Plan: Single-Hand Manual Entry

**Spec:** `docs/specs/FEAT-006-single-hand-entry.md`  
**Created:** 2026-04-08  
**Status:** Draft

## Summary

Implement a Blazor WASM page at `/hand` where a user types in four bridge hands, selects dealer and vulnerability, optionally enters a contract/result, and submits for instant DD analysis. The form is validated entirely client-side with inline errors, then calls a server-side `/api/hands/analyze` endpoint that invokes the existing `DdsAnalysisService` and `DeltaCalculationService`. On success the result is stored in `SessionStateService` as a single-board session and the browser navigates to `/boards/1` (the `BoardDetail.razor` page from FEAT-005).

The heavy lifting lives in two new classes in the Shared project -- `HandParser` (text-to-cards) and `HandValidator` (13-card / 52-unique / contract-completeness rules) -- so they are reusable by both client-side inline validation and server-side request validation. The API endpoint is a thin POST handler in the Server project.

## Key Design Decisions

1. **`HandParser` and `HandValidator` go in `BridgeGameCalculator.Shared`** because both the Blazor WASM client (for instant inline validation) and the Server (for defense-in-depth before DDS calls) need them. Shared is the correct project per ADR-001.

2. **Use `EditForm` with a custom model (`SingleHandFormModel`) and `FluentValidation`-style manual validation rather than DataAnnotations.** DataAnnotations cannot express cross-field rules like "all 52 cards unique across four hands" or "contract/declarer/result must all be present or all absent." Instead, implement `HandValidator` as a plain C# class with a `Validate(SingleHandFormModel) -> ValidationResult` method. Wire it into the `EditForm` via `EditContext.OnValidationRequested` and field-level `OnFieldChanged` events to achieve blur-triggered inline errors without a page reload.

3. **No `FluentValidation` NuGet dependency.** The validation logic is domain-specific enough that a hand-rolled validator is simpler and avoids adding a dependency for a single form. Return a `ValidationResult` dictionary keyed by field name.

4. **Single API endpoint: `POST /api/hands/analyze`.** This returns the complete `BoardDetailDto` (board + par + delta) in one round-trip. The client does not need to orchestrate multiple API calls.

5. **`HandParser` is a static class with a single public entry point: `ParseResult Parse(string input)`.** It accepts both formats (`S:AKQ H:JT9 D:87 C:654` and `AKQ.JT9.87.654`), normalizes `10`->`T` and lowercase->uppercase, and returns either a `Hand` (list of 13 `Card` values) or a structured parse error. Static because it holds no state.

6. **Reuse the existing `Board`, `Hands`, `Contract`, `Seat`, `Vulnerability`, and `Strain` domain types from FEAT-001 Shared models.** The manually-entered hand produces the exact same `Board` object that PBN parsing produces. No parallel model.

7. **`SessionStateService` stores a `Session` with a single `Board`.** When FEAT-006 creates a result, it overwrites any existing session with a new single-board session. This keeps the state model identical to FEAT-001 and lets `BoardDetail.razor` (FEAT-005) render without modification -- it just sees board number 1 in a one-board session with `prevBoardNumber = null`, `nextBoardNumber = null`, and no "back to session" link.

8. **Card is a value object (record struct) with `Suit` and `Rank` properties.** `Suit` enum: Spades, Hearts, Diamonds, Clubs. `Rank` enum: Two through Ace (int values 2-14). This gives `HandParser` a concrete return type and enables set-based duplicate detection in `HandValidator`.

9. **The `HandInput.razor` component encapsulates one seat's text input.** It exposes `[Parameter] Seat Seat`, `[Parameter] string Value`, `[Parameter] EventCallback<string> ValueChanged`, and `[Parameter] string? ErrorMessage`. The parent `SingleHandEntry.razor` owns the form model and validation state; `HandInput` is a pure presentation component.

10. **Server-side re-validation.** The API endpoint re-runs `HandParser` + `HandValidator` before calling DDS. Never trust client-only validation. On validation failure, return `400 Bad Request` with a `ProblemDetails` body.

## Implementation Steps

### Phase 1: Shared Domain Types and Parsing

This phase is entirely in `BridgeGameCalculator.Shared`. It has no UI or API dependencies and is fully unit-testable in isolation.

#### Step 1.1: Create the `Card` value object

**File:** `BridgeGameCalculator.Shared/Models/Card.cs`

Create a `readonly record struct Card(Suit Suit, Rank Rank)`. Implement `IComparable<Card>` to sort by suit then rank descending (for display). Override `ToString()` to return e.g. `"SA"` (Spade Ace).

This assumes `Suit` and `Rank` enums already exist from FEAT-001. If not, create them here:
- `Suit`: Spades = 0, Hearts = 1, Diamonds = 2, Clubs = 3
- `Rank`: Two = 2, Three = 3, ... Ten = 10, Jack = 11, Queen = 12, King = 13, Ace = 14

#### Step 1.2: Create `Hand` value object

**File:** `BridgeGameCalculator.Shared/Models/Hand.cs`

A `readonly record struct Hand` wrapping `IReadOnlyList<Card> Cards` (always exactly 13). Provide a factory method `Hand.FromCards(IReadOnlyList<Card>)` that throws if count != 13. Provide `IEnumerable<Card> BySuit(Suit suit)` for display grouping.

#### Step 1.3: Implement `HandParser`

**File:** `BridgeGameCalculator.Shared/Parsing/HandParser.cs`

Static class. Single public method:

```csharp
public static HandParseResult Parse(string input)
```

Where `HandParseResult` is a discriminated union (use a record with `bool Success`, `Hand? Hand`, `string? Error`).

Parsing algorithm:
1. Trim whitespace, normalize: uppercase the entire string, replace `"10"` with `"T"` globally.
2. Detect format:
   - If input contains `:` (e.g. `S:AKQ H:JT9 D:87 C:654`): split on whitespace, parse each `X:cards` token where X is S/H/D/C.
   - If input contains `.` but no `:` (e.g. `AKQ.JT9.87.654`): split on `.` into exactly 4 segments; map positionally to S/H/D/C.
   - Otherwise: return error "Unrecognized hand format. Use S:AKQ H:JT9 D:87 C:654 or AKQ.JT9.87.654".
3. For each suit segment, map each character to a `Rank` (A=Ace, K=King, Q=Queen, J=Jack, T=Ten, 9-2=numeric). Unknown characters produce an error.
4. Combine all cards. If total != 13, return error with actual count.
5. Check for duplicate cards within the hand (should not happen if input is well-formed, but defend).
6. Return `Hand` on success.

Support empty suit segments (void): `S:AKQ H: D:JT987654 C:32` or `AKQ..JT987654.32`.

#### Step 1.4: Implement `HandValidator`

**File:** `BridgeGameCalculator.Shared/Validation/HandValidator.cs`

Public class with a single method:

```csharp
public static HandValidationResult Validate(
    Hand? north, Hand? east, Hand? south, Hand? west,
    ContractInfo? contract)
```

Where `ContractInfo` is a small DTO: `record ContractInfo(int? Level, Strain? Strain, DoubledState? Doubled, Seat? Declarer, int? Result)`.

Validation rules (each produces a keyed error message):
1. Each non-null hand must have exactly 13 cards (already enforced by `Hand.FromCards`, but handle null -- "North hand is required").
2. All 52 cards across the four hands must be unique. On duplicate, report: `"{card} appears in more than one hand"`.
3. All 52 cards must be present (the 52-unique check plus 4x13=52 count covers this).
4. Contract completeness: if any of Level/Strain/Declarer/Result is provided, all four must be provided. Specific messages:
   - Level set but Declarer missing: "Declarer is required when a contract is entered."
   - Result set but Level missing: "Contract is required when a result is entered."
   - Declarer set but Level missing: "Contract level is required when a declarer is entered."
   - Level set but Result missing: "Result (tricks made) is required when a contract is entered."
5. If Result is provided, it must be 0-13 inclusive.
6. If Level is provided, it must be 1-7 inclusive.

Return a `HandValidationResult`: a record with `bool IsValid` and `IReadOnlyDictionary<string, string> Errors` keyed by field name (e.g. `"North"`, `"Contract"`, `"Declarer"`).

#### Step 1.5: Create `SingleHandRequest` DTO

**File:** `BridgeGameCalculator.Shared/Dtos/SingleHandRequest.cs`

```csharp
public record SingleHandRequest(
    string NorthHand,
    string EastHand,
    string SouthHand,
    string WestHand,
    Seat Dealer,
    Vulnerability Vulnerability,
    int? ContractLevel,
    Strain? ContractStrain,
    DoubledState? Doubled,
    Seat? Declarer,
    int? Result);
```

#### Step 1.6: Create `SingleHandResponse` DTO

**File:** `BridgeGameCalculator.Shared/Dtos/SingleHandResponse.cs`

```csharp
public record SingleHandResponse(
    int BoardNumber,           // always 1
    Seat Dealer,
    Vulnerability Vulnerability,
    HandDto North,
    HandDto East,
    HandDto South,
    HandDto West,
    string? ContractPlayed,    // human-readable or null
    Seat? Declarer,
    string? ResultDisplay,     // "+1", "=", "-2" or null
    int? ActualScore,
    string? ParContract,       // human-readable
    int ParScore,
    int? ImpDelta);
```

`HandDto` is `record HandDto(string Spades, string Hearts, string Diamonds, string Clubs)` -- card characters per suit for display.

### Phase 2: Server API Endpoint

#### Step 2.1: Create the `/api/hands/analyze` endpoint

**File:** `BridgeGameCalculator.Server/Endpoints/HandAnalysisEndpoints.cs`

Register as a Minimal API endpoint group in `Program.cs`:

```csharp
app.MapPost("/api/hands/analyze", async (SingleHandRequest request, 
    DdsAnalysisService dds, 
    DeltaCalculationService delta) => { ... });
```

Implementation:
1. Parse all four hands with `HandParser.Parse()`. If any fails, return `Results.BadRequest(ProblemDetails)` with the parse errors.
2. Build `ContractInfo` from optional fields. Run `HandValidator.Validate()`. If invalid, return 400 with validation errors.
3. Construct a `Board` object: `BoardNumber = 1`, `Dealer` from request, `Vulnerability` from request, `Hands` from parsed hands, `Contract`/`Declarer`/`Result` from optional fields.
4. Call `DdsAnalysisService.AnalyzeSingleBoard(board)` to get `ParResult`. Wrap in try/catch; on DDS failure, return `Results.Problem(statusCode: 500, detail: "Analysis failed...")`.
5. If contract/result were provided, call `DeltaCalculationService.CalculateDelta(board, parResult)` to get `BoardDelta`.
6. Map to `SingleHandResponse` and return `Results.Ok(response)`.

#### Step 2.2: Register the endpoint in `Program.cs`

**File:** `BridgeGameCalculator.Server/Program.cs`

Add `app.MapHandAnalysisEndpoints();` alongside existing endpoint registrations.

### Phase 3: Blazor Client UI

#### Step 3.1: Create `SingleHandFormModel`

**File:** `BridgeGameCalculator.Client/Models/SingleHandFormModel.cs`

A mutable class used as the `EditForm` model:

```csharp
public class SingleHandFormModel
{
    public string NorthHand { get; set; } = "";
    public string EastHand { get; set; } = "";
    public string SouthHand { get; set; } = "";
    public string WestHand { get; set; } = "";
    public Seat Dealer { get; set; } = Seat.North;
    public Vulnerability Vulnerability { get; set; } = Vulnerability.None;
    public int? ContractLevel { get; set; }
    public Strain? ContractStrain { get; set; }
    public DoubledState? Doubled { get; set; }
    public Seat? Declarer { get; set; }
    public int? Result { get; set; }
}
```

#### Step 3.2: Create `HandInput.razor` component

**File:** `BridgeGameCalculator.Client/Components/HandInput.razor`

A reusable child component for entering one seat's hand:

Parameters:
- `[Parameter] Seat Seat` -- N/E/S/W label
- `[Parameter] string Value` -- current text value (bound to parent model)
- `[Parameter] EventCallback<string> ValueChanged` -- two-way binding callback
- `[Parameter] string? ErrorMessage` -- inline validation error to display (null = no error)

Markup:
- A `<label>` showing the seat name (e.g. "North")
- A `<input type="text">` (single line, not textarea -- bridge hand notation fits in one line) with `placeholder="S:AKQ H:JT9 D:87 C:654"` or `placeholder="AKQ.JT9.87.654"`
- An `@onblur` handler that fires `ValueChanged` to trigger parent validation
- A `<span class="validation-error">` conditionally rendered when `ErrorMessage` is non-null

Use a text input rather than textarea because a single hand's notation is always one line (max ~30 characters). This is simpler and better for keyboard tabbing (NFR-004).

#### Step 3.3: Create `SingleHandEntry.razor` page

**File:** `BridgeGameCalculator.Client/Pages/SingleHandEntry.razor`

Route: `@page "/hand"`

Layout:
```
+--------------------------------------------+
|  Single-Hand Analysis                       |
|                                             |
|  Dealer: [N v]   Vulnerability: [None v]   |
|                                             |
|  North: [________________________] (error)  |
|  East:  [________________________] (error)  |
|  South: [________________________] (error)  |
|  West:  [________________________] (error)  |
|                                             |
|  --- Optional: Actual Contract ---          |
|  Level: [_ v]  Strain: [_ v]               |
|  Doubled: [Undoubled v]                     |
|  Declarer: [_ v]  Result: [__ v]           |
|  (contract group error)                     |
|                                             |
|  [ Analyze ]                                |
|  (general error / loading spinner)          |
+--------------------------------------------+
```

Code-behind (`SingleHandEntry.razor.cs` or `@code` block):
- Private `SingleHandFormModel _model = new()`.
- Private `Dictionary<string, string> _errors = new()` for field-keyed inline errors.
- Private `bool _isSubmitting` for loading state.
- Private `string? _generalError` for server/DDS errors.

**Inline validation on blur:**

When any `HandInput` fires `ValueChanged`:
1. Parse the changed hand with `HandParser.Parse()`.
2. If parse fails, set `_errors["North"] = parseResult.Error` (or whichever seat).
3. If parse succeeds, clear that field's error.
4. If all four hands parse successfully, run `HandValidator.Validate()` for cross-hand duplicate check. Set/clear `_errors` for duplicate card errors.

When contract-related fields change (`@onchange` on selects):
1. Run the contract-completeness portion of `HandValidator`.
2. Set/clear errors for `"Contract"`, `"Declarer"`, `"Result"`.

**Submit handler (`HandleSubmit`):**
1. Parse all four hands. If any fail, set errors and return.
2. Run full `HandValidator.Validate()`. If invalid, set errors and return.
3. Set `_isSubmitting = true`. Call `StateHasChanged()`.
4. Build `SingleHandRequest` from `_model`.
5. `POST` to `/api/hands/analyze` via `HttpClient`.
6. On success (200): deserialize `SingleHandResponse`, build a single-board `Session`, store in `SessionStateService`, navigate to `/boards/1`.
7. On 400: deserialize `ProblemDetails`, map to `_errors`.
8. On 500 or network error: set `_generalError = "Analysis failed. Please check the hand data and try again."`.
9. Set `_isSubmitting = false`.

**Format help text:** Display a small example below the hand inputs: `Format: S:AKQ H:JT9 D:87 C:654 or AKQ.JT9.87.654. Use T for Ten.`

#### Step 3.4: Wire `SessionStateService` for single-board sessions

**File:** `BridgeGameCalculator.Client/Services/SessionStateService.cs`

Add a method (or reuse existing):

```csharp
public void SetSingleBoardSession(Board board, ParResult parResult, BoardDelta? delta)
```

This creates a `Session` with `sourceFile = "Manual Entry"`, a single board, and stores the associated analysis results. The existing `BoardDetail.razor` reads from this service -- it will see a single-board session and render with no prev/next navigation per FEAT-005 EC-2.

If `SessionStateService` already has a `SetSession()` method from FEAT-001/004 planning, call that with the single-board session. The key point is that the service interface remains the same; FEAT-006 just provides a different session shape.

#### Step 3.5: Add navigation link

**File:** `BridgeGameCalculator.Client/Shared/NavMenu.razor`

Add a nav link to the manual entry page:

```html
<NavLink class="nav-link" href="hand">
    Single Hand
</NavLink>
```

### Phase 4: Tests

#### Step 4.1: `HandParserTests`

**File:** `BridgeGameCalculator.Shared.Tests/Parsing/HandParserTests.cs`

xUnit test class. Test cases:

| Test | Input | Expected |
|------|-------|----------|
| Parses suit-colon format | `"S:AKQ H:JT9 D:87 C:65432"` | Success, 13 cards, correct suits |
| Parses dot-separated format | `"AKQ.JT9.87.65432"` | Success, 13 cards, correct suits |
| Normalizes lowercase | `"s:akq h:jt9 d:87 c:65432"` | Success, same result as uppercase |
| Normalizes 10 to T | `"S:AKQ H:J109 D:87 C:6543"` | Success, Ten of Hearts present (note: "10" becomes "T" so "J109" becomes "JT9", yielding 3 heart cards; adjust test input to ensure 13 total) |
| Rejects unknown character | `"S:AKX H:JT9 D:87 C:65432"` | Error mentioning unrecognized card 'X' |
| Rejects wrong card count (12) | `"S:AKQ H:JT9 D:87 C:6543"` | Error: expected 13 cards, found 12 |
| Rejects wrong card count (14) | `"S:AKQJ H:JT9 D:87 C:65432"` | Error: expected 13 cards, found 14 |
| Handles void suit (colon format) | `"S:AKQJT98765432 H: D: C:"` | Success, 13 spades, 0 in other suits |
| Handles void suit (dot format) | `"AKQJT98765432..."` | Success, 13 spades |
| Rejects unrecognized format | `"random text"` | Error about unrecognized format |
| Handles extra whitespace | `"  S:AKQ   H:JT9  D:87  C:65432  "` | Success |
| Duplicate card within hand | `"S:AAK H:JT9 D:87 C:6543"` | Error: duplicate card |

#### Step 4.2: `HandValidatorTests`

**File:** `BridgeGameCalculator.Shared.Tests/Validation/HandValidatorTests.cs`

xUnit test class. Test cases:

| Test | Scenario | Expected |
|------|----------|----------|
| Valid 52 unique cards, no contract | Four valid hands, no contract fields | `IsValid = true` |
| Valid 52 unique cards, full contract | Four valid hands + Level=4, Strain=Spades, Declarer=South, Result=10 | `IsValid = true` |
| Duplicate card across hands | Ace of Spades in both North and East | Error on cross-hand field: "Ace of Spades appears in more than one hand" |
| Missing hand (null North) | North is null, others valid | Error: "North hand is required" |
| Contract without declarer | Level=4, Strain=Spades, Declarer=null, Result=10 | Error: "Declarer is required when a contract is entered." |
| Contract without result | Level=4, Strain=Spades, Declarer=South, Result=null | Error: "Result (tricks made) is required when a contract is entered." |
| Result without contract | Level=null, Result=10 | Error: "Contract is required when a result is entered." |
| Declarer without contract | Level=null, Declarer=South | Error: "Contract level is required when a declarer is entered." |
| Result out of range (14) | Full contract, Result=14 | Error: "Result must be between 0 and 13." |
| Result out of range (-1) | Full contract, Result=-1 | Error: "Result must be between 0 and 13." |
| Level out of range (0) | Level=0 | Error: "Contract level must be between 1 and 7." |
| Level out of range (8) | Level=8 | Error: "Contract level must be between 1 and 7." |
| All contract fields absent | All optional fields null | `IsValid = true` (no contract is valid) |
| Result = 0 is valid | Full contract, Result=0 | `IsValid = true` (EC-3 from spec) |

#### Step 4.3: `HandAnalysisEndpointTests`

**File:** `BridgeGameCalculator.Server.Tests/Endpoints/HandAnalysisEndpointTests.cs`

Integration tests using `WebApplicationFactory<Program>`. Test cases:

| Test | Scenario | Expected |
|------|----------|----------|
| Valid request returns 200 | POST valid four hands + dealer + vulnerability | 200 OK with `SingleHandResponse` containing par result |
| Invalid hand returns 400 | POST with 12-card North hand | 400 with ProblemDetails mentioning "13 cards" |
| Duplicate card returns 400 | POST with Ace of Spades in two hands | 400 with ProblemDetails mentioning duplicate |
| Incomplete contract returns 400 | POST with Level but no Declarer | 400 with ProblemDetails mentioning Declarer |
| Valid request with contract | POST valid hands + Level=4/Spades/South/10 | 200 OK with `ImpDelta` populated |
| No contract returns null delta | POST valid hands, no contract fields | 200 OK with `ImpDelta = null` |

Note: These integration tests require DDS to be available. For initial development, mock `DdsAnalysisService` behind an interface `IDdsAnalysisService` so tests can run without the native library.

#### Step 4.4: `SingleHandEntry.razor` bUnit tests

**File:** `BridgeGameCalculator.Client.Tests/Pages/SingleHandEntryTests.cs`

bUnit component tests. Test cases:

| Test | Scenario | Expected |
|------|----------|----------|
| Renders four hand inputs | Mount component | Four `HandInput` components present with labels N/E/S/W |
| Renders dealer dropdown | Mount component | `<select>` with options N/E/S/W, default North |
| Renders vulnerability dropdown | Mount component | `<select>` with options None/NS/EW/Both |
| Shows inline error on invalid hand blur | Enter "AKQ" in North, blur | Error "North must have exactly 13 cards" displayed |
| Clears error when hand corrected | Enter valid 13-card hand after error | Error disappears |
| Submit button disabled while submitting | Click Analyze with valid data | Button shows loading state |
| Navigates to /boards/1 on success | Submit valid form, mock 200 response | `NavigationManager.Uri` ends with `/boards/1` |

### Phase 5: CSS and Polish

#### Step 5.1: Add styles for the form layout

**File:** `BridgeGameCalculator.Client/Pages/SingleHandEntry.razor.css` (scoped CSS)

- Two-column grid for Dealer + Vulnerability dropdowns.
- Stack hand inputs vertically with consistent label width.
- Optional contract section visually separated (light border or background).
- `.validation-error` class: red text, small font, appears below the field.
- `.submitting` state: Analyze button shows a spinner or "Analyzing..." text, inputs disabled.
- Ensure tab order is logical: Dealer -> Vulnerability -> North -> East -> South -> West -> Contract Level -> Strain -> Doubled -> Declarer -> Result -> Analyze button.

#### Step 5.2: Add format help text

Render a `<p class="format-help">` below the hand inputs area with example notation and "Use T for Ten" note (NFR-003 from spec).

## File Inventory

### New Files

- `BridgeGameCalculator.Shared/Models/Card.cs` -- Card value object (Suit + Rank record struct)
- `BridgeGameCalculator.Shared/Models/Hand.cs` -- Hand value object (13-card wrapper)
- `BridgeGameCalculator.Shared/Parsing/HandParser.cs` -- Static parser for hand notation strings
- `BridgeGameCalculator.Shared/Parsing/HandParseResult.cs` -- Parse result type (success/error)
- `BridgeGameCalculator.Shared/Validation/HandValidator.cs` -- Cross-hand and contract-completeness validator
- `BridgeGameCalculator.Shared/Validation/HandValidationResult.cs` -- Validation result type
- `BridgeGameCalculator.Shared/Validation/ContractInfo.cs` -- DTO for optional contract fields passed to validator
- `BridgeGameCalculator.Shared/Dtos/SingleHandRequest.cs` -- API request DTO
- `BridgeGameCalculator.Shared/Dtos/SingleHandResponse.cs` -- API response DTO
- `BridgeGameCalculator.Shared/Dtos/HandDto.cs` -- Per-suit card string DTO for display
- `BridgeGameCalculator.Server/Endpoints/HandAnalysisEndpoints.cs` -- Minimal API endpoint for `/api/hands/analyze`
- `BridgeGameCalculator.Client/Models/SingleHandFormModel.cs` -- Mutable form model for Blazor EditForm
- `BridgeGameCalculator.Client/Components/HandInput.razor` -- Reusable single-seat hand input component
- `BridgeGameCalculator.Client/Pages/SingleHandEntry.razor` -- Main page at `/hand`
- `BridgeGameCalculator.Client/Pages/SingleHandEntry.razor.css` -- Scoped styles
- `BridgeGameCalculator.Shared.Tests/Parsing/HandParserTests.cs` -- xUnit tests for HandParser
- `BridgeGameCalculator.Shared.Tests/Validation/HandValidatorTests.cs` -- xUnit tests for HandValidator
- `BridgeGameCalculator.Server.Tests/Endpoints/HandAnalysisEndpointTests.cs` -- Integration tests for the API endpoint
- `BridgeGameCalculator.Client.Tests/Pages/SingleHandEntryTests.cs` -- bUnit tests for the page component

### Modified Files

- `BridgeGameCalculator.Server/Program.cs` -- Register the hand analysis endpoint
- `BridgeGameCalculator.Client/Shared/NavMenu.razor` -- Add "Single Hand" navigation link
- `BridgeGameCalculator.Client/Services/SessionStateService.cs` -- Add `SetSingleBoardSession()` method (or confirm existing `SetSession()` suffices)

### Files Assumed to Exist (from FEAT-001, FEAT-002, FEAT-003, FEAT-005)

- `BridgeGameCalculator.Shared/Models/Board.cs` -- Board domain model
- `BridgeGameCalculator.Shared/Models/Hands.cs` -- Four-hand container
- `BridgeGameCalculator.Shared/Models/Contract.cs` -- Contract value object
- `BridgeGameCalculator.Shared/Enums/Seat.cs` -- N/E/S/W enum
- `BridgeGameCalculator.Shared/Enums/Vulnerability.cs` -- None/NS/EW/Both enum
- `BridgeGameCalculator.Shared/Enums/Strain.cs` -- S/H/D/C/NT enum
- `BridgeGameCalculator.Shared/Enums/DoubledState.cs` -- Undoubled/Doubled/Redoubled enum
- `BridgeGameCalculator.Server/Services/DdsAnalysisService.cs` -- DD solver wrapper (FEAT-002)
- `BridgeGameCalculator.Server/Services/DeltaCalculationService.cs` -- IMP delta calculator (FEAT-003)
- `BridgeGameCalculator.Client/Services/SessionStateService.cs` -- Session state container
- `BridgeGameCalculator.Client/Pages/BoardDetail.razor` -- Board detail view (FEAT-005)

## Testing Strategy

### Unit Tests (Shared project -- no mocks needed)

**`HandParser`**: Pure input/output tests. Each test provides a string and asserts either a successful `Hand` with the correct cards or a specific error message. Cover both notation formats, normalization edge cases (lowercase, "10"), void suits, and malformed input. These are the highest-value tests because the parser is the most complex new logic.

**`HandValidator`**: Pure input/output tests. Construct `Hand` objects directly (bypassing the parser) and assert validation results. Cover: valid complete deck, duplicate cards across hands, missing hands, each contract-completeness permutation, boundary values for result (0, 13) and level (1, 7), and out-of-range values.

### Integration Tests (Server project)

Use `WebApplicationFactory<Program>` with `DdsAnalysisService` replaced by a mock/stub via DI override. The mock returns a fixed `ParResult` for any valid board. This tests the full HTTP pipeline: JSON deserialization -> parse -> validate -> analyze -> serialize response. Cover success and all 400-level error paths.

### Component Tests (Client project -- bUnit)

Use bUnit to mount `SingleHandEntry.razor` with mocked `HttpClient` and `SessionStateService`. Test:
- Rendering: all inputs, dropdowns, and button present
- Inline validation: trigger blur events, assert error messages appear/disappear
- Form submission: mock HTTP response, assert navigation occurs
- Error display: mock 400/500 responses, assert error messages render

### Manual Smoke Test

After all automated tests pass, manually test the end-to-end flow: open `/hand`, enter a known deal, submit, verify the board detail page shows correct par and delta. Use the classic "Deal 1" from the DDS test suite as a reference hand with known par.

## Migration Notes

- **No database migrations.** The application is stateless; all data is in-memory per session.
- **No backwards compatibility concerns.** This is a new feature on a greenfield project with no existing users.
- **No feature flags needed.** The feature is entirely additive -- a new page at a new route. It does not modify any existing page or behavior.
- **Dependency on FEAT-001 domain model.** If `Card`, `Suit`, `Rank` are not yet implemented as part of FEAT-001, Phase 1 of this plan creates them. If FEAT-001 already defines a different card representation (e.g. strings in `Hands`), align `HandParser`'s output to match that representation rather than creating a parallel model. The key principle: one `Board` type, one `Hands` type, shared across PBN import and manual entry.
- **Dependency on FEAT-002/003 services.** If those services are not yet implemented, the API endpoint can be developed against their interfaces with stub implementations. The `HandParser` and `HandValidator` (Phase 1) and the UI form (Phase 3) can be built and tested independently of the analysis services.
