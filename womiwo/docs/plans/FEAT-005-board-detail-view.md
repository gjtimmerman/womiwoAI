# Implementation Plan: Board Detail View

**Spec:** `docs/specs/FEAT-005-board-detail-view.md`
**Created:** 2026-04-08
**Status:** Draft

## Summary

Implement the board detail view as a Blazor WASM page (`BoardDetail.razor`) at route `/boards/{boardNumber:int}` that renders the four-hand compass diagram, contract/result, par contract/score, and color-coded IMP delta. The page is composed of two child components -- `HandDiagram.razor` (compass grid layout) and `HandDisplay.razor` (single hand, cards by suit). A `PbnDealParser` utility in the Shared project handles parsing PBN deal strings into strongly-typed card collections. The page reads data from `SessionStateService` when present (session drill-down from FEAT-004) and accepts data via a parameter when there is no session context (single-hand analysis from FEAT-006). Previous/Next navigation and "Back to session" are conditionally rendered based on session context.

## Key Design Decisions

1. **CSS Grid for compass layout, not HTML table.** The compass layout (N top-center, W left, E right, S bottom-center) does not map cleanly to a rectangular table. A 3x3 CSS grid with named areas (`north`, `west`, `center`, `east`, `south`) produces cleaner, more maintainable markup. The center cell holds the board metadata (board number, dealer, vulnerability).

2. **Strongly-typed card model in Shared project.** Cards are represented as `Card` (record with `Suit` and `Rank` enums) and a hand as `ParsedHand` (four `IReadOnlyList<Card>` collections, one per suit, pre-sorted high-to-low). This keeps parsing logic testable and reusable by both FEAT-005 and FEAT-006 without coupling to UI concerns.

3. **`PbnDealParser` as a static utility, not a service.** Parsing a PBN deal string like `"AKQ.JT9.87.654"` into a `ParsedHand` is a pure function with no dependencies. A static class in the Shared project is the simplest approach and trivially unit-testable.

4. **`SessionStateService` provides navigation context via an interface.** `BoardDetail.razor` depends on `ISessionStateService` (injected). When the service has a loaded session, the page reads `BoardDetailViewModel` from it, including `PrevBoardNumber` and `NextBoardNumber`. When accessed from FEAT-006 (no session), the component receives data through a cascading parameter or direct property set, and navigation controls are hidden.

5. **`BoardDetailViewModel` assembles the read model.** Rather than passing raw domain entities to the page, a `BoardDetailViewModel` record in the Shared project flattens Board, ParResult, and BoardDelta into a single display-ready object matching the spec's `BoardDetailView` schema. This keeps the Razor page thin and the assembly logic testable.

6. **Reuse FEAT-004 IMP delta CSS classes.** The spec requires the same green/red/neutral color coding as FEAT-004. Define the CSS classes (`imp-positive`, `imp-negative`, `imp-neutral`) in a shared `app.css` or `shared-styles.css` file in the Client project, referenced by both FEAT-004 dashboard and FEAT-005 detail view.

7. **Void suit display: show the suit symbol followed by a dash.** When a hand is void in a suit, display the suit symbol in its color followed by "---" (e.g., red heart symbol then dash). This is standard in bridge software and clearer than "(void)".

8. **Use `NavigationManager` for board-to-board navigation.** Previous/Next buttons call `NavigationManager.NavigateTo($"/boards/{n}")`. This keeps routing simple and each board detail page is a fresh route with its own URL, enabling browser back/forward.

9. **Component isolation scoped CSS.** Each component (`HandDiagram.razor.css`, `HandDisplay.razor.css`, `BoardDetail.razor.css`) uses Blazor CSS isolation. No global style pollution.

10. **`BoardDetailViewModel` construction lives in a `BoardDetailViewModelFactory` static class.** This factory takes a `Board`, `ParResult?`, and `BoardDelta?` and produces the view model. It lives in the Shared project so both the session flow and the single-hand flow can use it.

## Implementation Steps

### Phase 1: Shared Domain Types and Parsing

#### Step 1.1: Define card enums and types

Create `Suit`, `Rank`, `Card`, and `ParsedHand` types in the Shared project.

**File:** `BridgeGameCalculator.Shared/Models/Card.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public enum Suit { Spades, Hearts, Diamonds, Clubs }

public enum Rank
{
    Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
    Jack, Queen, King, Ace
}

public record Card(Suit Suit, Rank Rank);
```

**File:** `BridgeGameCalculator.Shared/Models/ParsedHand.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public record ParsedHand(
    IReadOnlyList<Card> Spades,
    IReadOnlyList<Card> Hearts,
    IReadOnlyList<Card> Diamonds,
    IReadOnlyList<Card> Clubs);
```

- `Rank` values enable natural descending sort (Ace=14 > King=13 > ... > Two=2).
- Each suit list in `ParsedHand` is pre-sorted high-to-low at parse time.

#### Step 1.2: Implement `PbnDealParser`

Create a static utility that parses a single PBN hand string (e.g., `"AKQ.JT9.87.654"`) into a `ParsedHand`.

**File:** `BridgeGameCalculator.Shared/Parsing/PbnDealParser.cs`

- Public method: `static ParsedHand ParseHand(string pbnHand)` -- parses one hand.
- Public method: `static Dictionary<Seat, ParsedHand> ParseDeal(string pbnDeal)` -- parses a full PBN Deal tag value (e.g., `"N:AKQ.JT9.87.654 T98.876.654.T32 ..."`) into all four hands keyed by Seat.
- Suit sections are separated by `.` (dot). Order is always Spades.Hearts.Diamonds.Clubs.
- Card characters: A K Q J T 9 8 7 6 5 4 3 2. Also accept "10" as "T".
- An empty section between dots means void in that suit.
- Each suit's cards are sorted descending by Rank before returning.
- Throws `PbnParseException` (custom exception in Shared) for malformed input.

#### Step 1.3: Define `BoardDetailViewModel`

Create the flattened read model for the board detail page.

**File:** `BridgeGameCalculator.Shared/ViewModels/BoardDetailViewModel.cs`

```csharp
namespace BridgeGameCalculator.Shared.ViewModels;

public record BoardDetailViewModel
{
    public required int BoardNumber { get; init; }
    public required Seat Dealer { get; init; }
    public required Vulnerability Vulnerability { get; init; }
    public required Dictionary<Seat, ParsedHand> Hands { get; init; }
    public string? ContractDisplay { get; init; }    // "4S by South, +1, +650 NS" or null
    public string? ParDisplay { get; init; }          // "Par: 4S by South = +620 NS" or null
    public int? ImpDelta { get; init; }               // null if no result
    public bool IsPassedOut { get; init; }
    public bool AnalysisFailed { get; init; }
    public int? PrevBoardNumber { get; init; }        // null if first or no session
    public int? NextBoardNumber { get; init; }        // null if last or no session
    public bool HasSessionContext { get; init; }      // false when from FEAT-006
}
```

#### Step 1.4: Implement `BoardDetailViewModelFactory`

**File:** `BridgeGameCalculator.Shared/ViewModels/BoardDetailViewModelFactory.cs`

Static class with method:

```csharp
public static BoardDetailViewModel Create(
    Board board,
    ParResult? parResult,
    BoardDelta? boardDelta,
    int? prevBoardNumber,
    int? nextBoardNumber,
    bool hasSessionContext)
```

Responsibilities:
- Parse `board.Hands` (the PBN deal strings) into `Dictionary<Seat, ParsedHand>` using `PbnDealParser`.
- Format the contract display string: level + strain symbol + "by" + declarer + result + actual score. Handle passed-out boards ("Passed out").
- Format the par display string: "Par: " + level + strain symbol + "by" + declarer + "=" + par score. Handle no-par ("Par: Pass (0)").
- Copy through `ImpDelta`, navigation, and context flags.
- If `parResult` is null, set `AnalysisFailed = true`.

Helper: `FormatStrain(Strain s)` returns the Unicode suit symbol (Spades="\u2660", Hearts="\u2665", Diamonds="\u2666", Clubs="\u2663", NoTrump="NT").

### Phase 2: Client Services

#### Step 2.1: Define `ISessionStateService` interface

**File:** `BridgeGameCalculator.Client/Services/ISessionStateService.cs`

```csharp
public interface ISessionStateService
{
    bool HasSession { get; }
    SessionAnalysis? CurrentSession { get; }
    BoardDetailViewModel? GetBoardDetail(int boardNumber);
    IReadOnlyList<int> BoardNumbers { get; }
}
```

`GetBoardDetail(int boardNumber)` internally:
- Finds the `Board` by number in the session.
- Finds the corresponding `ParResult` and `BoardDelta`.
- Determines prev/next board numbers from the ordered `BoardNumbers` list.
- Calls `BoardDetailViewModelFactory.Create(...)`.

Note: The full `SessionStateService` implementation is part of FEAT-004. This plan defines the interface method that FEAT-005 requires. If FEAT-004 is implemented first, add the `GetBoardDetail` method to the existing class. If FEAT-005 is implemented first, create a stub/partial implementation.

#### Step 2.2: Register service in DI

**File:** `BridgeGameCalculator.Client/Program.cs`

Add `builder.Services.AddSingleton<ISessionStateService, SessionStateService>();` (or confirm it is already registered from FEAT-004).

### Phase 3: Blazor UI Components

#### Step 3.1: Create `HandDisplay.razor` component

**File:** `BridgeGameCalculator.Client/Components/HandDisplay.razor`

Parameters:
- `[Parameter] public ParsedHand Hand { get; set; }`

Renders a vertical list of four suit rows. Each row:
- Suit symbol (Unicode char) with CSS class `suit-red` (for Hearts, Diamonds) or `suit-black` (for Spades, Clubs).
- Space-separated card ranks (A K Q J T 9 ... 2), or "---" if the suit list is empty (void).
- Each rank character inherits the suit color from the parent span.

Order: Spades first, then Hearts, Diamonds, Clubs (top to bottom).

**File:** `BridgeGameCalculator.Client/Components/HandDisplay.razor.css`

```css
.suit-red { color: #d32f2f; }    /* Red 700 -- accessible on white */
.suit-black { color: #212121; }  /* Grey 900 */
.hand-display { font-family: 'Segoe UI', sans-serif; line-height: 1.6; }
.suit-row { white-space: nowrap; }
```

#### Step 3.2: Create `HandDiagram.razor` component

**File:** `BridgeGameCalculator.Client/Components/HandDiagram.razor`

Parameters:
- `[Parameter] public Dictionary<Seat, ParsedHand> Hands { get; set; }`
- `[Parameter] public int BoardNumber { get; set; }`
- `[Parameter] public Seat Dealer { get; set; }`
- `[Parameter] public Vulnerability Vulnerability { get; set; }`

Layout: 3x3 CSS grid.

```
.hand-diagram {
    display: grid;
    grid-template-areas:
        ".     north ."
        "west  info  east"
        ".     south .";
    grid-template-columns: 1fr auto 1fr;
    grid-template-rows: auto auto auto;
    gap: 1rem;
    max-width: 700px;
}
```

- Grid area `north`: `<HandDisplay Hand="@Hands[Seat.North]" />`
- Grid area `west`: `<HandDisplay Hand="@Hands[Seat.West]" />`
- Grid area `east`: `<HandDisplay Hand="@Hands[Seat.East]" />`
- Grid area `south`: `<HandDisplay Hand="@Hands[Seat.South]" />`
- Grid area `info` (center cell): Board number, dealer label ("Dlr: N"), vulnerability label ("Vul: Both"). Compass indicator letters (N/S/E/W) positioned at edges of center cell for visual clarity.

**File:** `BridgeGameCalculator.Client/Components/HandDiagram.razor.css`

Contains the grid layout CSS above plus alignment for each grid area (north is `justify-self: center`, west is `justify-self: end`, east is `justify-self: start`, south is `justify-self: center`).

#### Step 3.3: Create `BoardDetail.razor` page

**File:** `BridgeGameCalculator.Client/Pages/BoardDetail.razor`

Route: `@page "/boards/{BoardNumber:int}"`

Inject: `ISessionStateService`, `NavigationManager`.

Logic in `OnParametersSet`:
1. If `ISessionStateService.HasSession`, call `GetBoardDetail(BoardNumber)` to get `BoardDetailViewModel`.
2. If no session context but a `[CascadingParameter] BoardDetailViewModel? SingleHandResult` is present, use that (FEAT-006 flow).
3. If neither source has data, show "Board not found" message.

Markup (top to bottom):
1. **Navigation bar (conditional):** "Back to session" link (`/session`) and Prev/Next buttons. Rendered only when `ViewModel.HasSessionContext` is true. Prev button disabled when `PrevBoardNumber` is null. Next button disabled when `NextBoardNumber` is null.
2. **Board heading:** "Board {n}"
3. **Hand diagram:** `<HandDiagram Hands="@ViewModel.Hands" BoardNumber="@ViewModel.BoardNumber" Dealer="@ViewModel.Dealer" Vulnerability="@ViewModel.Vulnerability" />`
4. **Contract/Result section:**
   - If `IsPassedOut`: "Passed out"
   - If `AnalysisFailed`: show contract line normally, par/delta show "Analysis unavailable"
   - Otherwise: contract display string
5. **Par section:** Par display string, or "Par: Pass (0)" if passed out, or "Analysis unavailable" if analysis failed.
6. **IMP delta section:** Rendered in a `<span>` with CSS class `imp-positive` / `imp-negative` / `imp-neutral` based on sign. Shows "+N", "-N", or "0". Shows "N/A" if `ImpDelta` is null and not passed out.

**File:** `BridgeGameCalculator.Client/Pages/BoardDetail.razor.css`

Scoped styles for page layout, section spacing, heading styles, and navigation button styling. Import shared IMP color classes.

#### Step 3.4: Define shared IMP color CSS

**File:** `BridgeGameCalculator.Client/wwwroot/css/app.css` (modify existing)

Add (or confirm these exist from FEAT-004):

```css
.imp-positive { color: #2e7d32; font-weight: 700; }  /* Green 800 */
.imp-negative { color: #c62828; font-weight: 700; }  /* Red 800 */
.imp-neutral  { color: #616161; font-weight: 700; }   /* Grey 700 */
```

These are global, not scoped, because they are shared across FEAT-004 and FEAT-005.

### Phase 4: Tests

#### Step 4.1: Unit tests for `PbnDealParser`

**File:** `BridgeGameCalculator.Shared.Tests/Parsing/PbnDealParserTests.cs`

Test cases:
- `ParseHand_ValidFullHand_ReturnsCorrectCards` -- e.g., `"AKQ.JT9.87.654"` produces Spades=[A,K,Q], Hearts=[J,T,9], Diamonds=[8,7], Clubs=[6,5,4].
- `ParseHand_VoidSuit_ReturnsEmptyList` -- e.g., `"AKQ..87.654321T98"` (void in hearts).
- `ParseHand_AllCardsInOneSuit_Works` -- e.g., `"AKQJT98765432..."`.
- `ParseHand_TenAsT_Parsed` -- "T" maps to Rank.Ten.
- `ParseHand_TenAs10_Parsed` -- "10" maps to Rank.Ten.
- `ParseHand_CardsAreSortedDescending` -- e.g., input `"23A"` in spades produces [A,3,2].
- `ParseHand_MalformedInput_ThrowsPbnParseException` -- e.g., `"AKQ.XYZ.87.654"`.
- `ParseDeal_FullDealString_ParsesAllFourHands` -- full PBN Deal tag value with compass prefix.

#### Step 4.2: Unit tests for `BoardDetailViewModelFactory`

**File:** `BridgeGameCalculator.Shared.Tests/ViewModels/BoardDetailViewModelFactoryTests.cs`

Test cases:
- `Create_NormalBoard_FormatsContractDisplayCorrectly` -- "4\u2660 by South, +1, +650 NS".
- `Create_PassedOutBoard_SetsIsPassedOutTrue` -- contract display null, par display "Par: Pass (0)".
- `Create_NullParResult_SetsAnalysisFailedTrue`.
- `Create_WithNavigation_SetsPrevAndNextCorrectly`.
- `Create_NoSessionContext_HasSessionContextFalse`.
- `Create_NullDelta_ImpDeltaIsNull`.

#### Step 4.3: bUnit tests for `HandDisplay`

**File:** `BridgeGameCalculator.Client.Tests/Components/HandDisplayTests.cs`

Test cases:
- `Renders_FourSuitRows_InOrder` -- verify four `.suit-row` elements appear, Spades first, Clubs last.
- `Renders_RedColor_ForHeartsAndDiamonds` -- Hearts and Diamonds rows have `.suit-red` class.
- `Renders_BlackColor_ForSpadesAndClubs` -- Spades and Clubs rows have `.suit-black` class.
- `Renders_VoidSuit_AsDash` -- when a suit list is empty, the row shows "---".
- `Renders_CardsInDescendingOrder` -- A K Q J T 9 etc.

#### Step 4.4: bUnit tests for `HandDiagram`

**File:** `BridgeGameCalculator.Client.Tests/Components/HandDiagramTests.cs`

Test cases:
- `Renders_FourHandDisplayComponents` -- verify four `HandDisplay` child components are present.
- `Renders_BoardMetadataInCenter` -- board number, dealer, vulnerability text appears in the center cell.
- `GridLayout_HasCorrectAreas` -- verify the `.hand-diagram` element has the expected grid CSS class.

#### Step 4.5: bUnit tests for `BoardDetail`

**File:** `BridgeGameCalculator.Client.Tests/Pages/BoardDetailTests.cs`

Test cases:
- `WithSessionContext_ShowsNavigationControls` -- Prev/Next buttons and "Back to session" link are rendered.
- `WithSessionContext_FirstBoard_PrevDisabled` -- Prev button has `disabled` attribute.
- `WithSessionContext_LastBoard_NextDisabled` -- Next button has `disabled` attribute.
- `WithoutSessionContext_HidesNavigationControls` -- Prev/Next and "Back to session" are absent.
- `PassedOutBoard_ShowsPassedOut` -- "Passed out" text appears in contract section.
- `NormalBoard_ShowsContractAndPar` -- contract display and par display strings are rendered.
- `ImpDelta_Positive_HasGreenClass` -- delta element has `.imp-positive`.
- `ImpDelta_Negative_HasRedClass` -- delta element has `.imp-negative`.
- `ImpDelta_Zero_HasNeutralClass` -- delta element has `.imp-neutral`.
- `ImpDelta_Null_ShowsNA` -- "N/A" text shown.
- `AnalysisFailed_ShowsUnavailable` -- "Analysis unavailable" in par and delta sections.
- `NextButton_Click_NavigatesToNextBoard` -- verify `NavigationManager` is called with correct URL.
- `PrevButton_Click_NavigatesToPrevBoard`.

Setup: Register a mock `ISessionStateService` (using bUnit's built-in service registration) that returns prepared `BoardDetailViewModel` instances. Use `bUnit.TestDoubles.FakeNavigationManager` to assert navigation calls.

## File Inventory

### New Files

- `BridgeGameCalculator.Shared/Models/Card.cs` -- Suit enum, Rank enum, Card record
- `BridgeGameCalculator.Shared/Models/ParsedHand.cs` -- Parsed hand with four suit collections
- `BridgeGameCalculator.Shared/Parsing/PbnDealParser.cs` -- Static PBN deal string parser
- `BridgeGameCalculator.Shared/Parsing/PbnParseException.cs` -- Custom exception for parse errors
- `BridgeGameCalculator.Shared/ViewModels/BoardDetailViewModel.cs` -- Flattened read model for the page
- `BridgeGameCalculator.Shared/ViewModels/BoardDetailViewModelFactory.cs` -- Assembles view model from domain entities
- `BridgeGameCalculator.Client/Services/ISessionStateService.cs` -- Interface for session state access
- `BridgeGameCalculator.Client/Components/HandDisplay.razor` -- Single hand card display component
- `BridgeGameCalculator.Client/Components/HandDisplay.razor.css` -- Scoped styles for hand display
- `BridgeGameCalculator.Client/Components/HandDiagram.razor` -- Compass layout grid component
- `BridgeGameCalculator.Client/Components/HandDiagram.razor.css` -- Scoped styles for compass grid
- `BridgeGameCalculator.Client/Pages/BoardDetail.razor` -- Board detail page component
- `BridgeGameCalculator.Client/Pages/BoardDetail.razor.css` -- Scoped styles for board detail page
- `BridgeGameCalculator.Shared.Tests/Parsing/PbnDealParserTests.cs` -- Parser unit tests
- `BridgeGameCalculator.Shared.Tests/ViewModels/BoardDetailViewModelFactoryTests.cs` -- Factory unit tests
- `BridgeGameCalculator.Client.Tests/Components/HandDisplayTests.cs` -- bUnit tests for HandDisplay
- `BridgeGameCalculator.Client.Tests/Components/HandDiagramTests.cs` -- bUnit tests for HandDiagram
- `BridgeGameCalculator.Client.Tests/Pages/BoardDetailTests.cs` -- bUnit tests for BoardDetail page

### Modified Files

- `BridgeGameCalculator.Client/wwwroot/css/app.css` -- Add shared IMP color-coding CSS classes
- `BridgeGameCalculator.Client/Program.cs` -- Register `ISessionStateService` in DI (if not already done by FEAT-004)

### Projects to Create (if not yet created by earlier features)

The ADR specifies three projects. The current repo only has a console app placeholder. These projects need to exist before FEAT-005 files can be placed:

- `BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj` -- Class library, net8.0
- `BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj` -- Blazor WASM, net8.0, references Shared
- `BridgeGameCalculator.Shared.Tests/BridgeGameCalculator.Shared.Tests.csproj` -- xUnit, net8.0, references Shared
- `BridgeGameCalculator.Client.Tests/BridgeGameCalculator.Client.Tests.csproj` -- xUnit + bUnit, net8.0, references Client

If FEAT-001 or FEAT-004 implementation creates these projects first, FEAT-005 simply adds files to them.

## Testing Strategy

**Unit tests (xUnit, Shared project):**
- `PbnDealParser`: Pure input/output testing. Cover normal hands, voids, edge cases (all 13 in one suit), malformed input. No mocks needed.
- `BoardDetailViewModelFactory`: Supply constructed `Board`, `ParResult`, `BoardDelta` objects and assert the view model fields. Pure logic, no mocks.

**Component tests (bUnit, Client project):**
- `HandDisplay`: Render with a known `ParsedHand`, assert DOM structure -- suit symbols, color classes, card text, void handling.
- `HandDiagram`: Render with four known hands and metadata, assert grid structure and that four `HandDisplay` components are present.
- `BoardDetail`: Register mock `ISessionStateService`, render the page, assert navigation controls, contract/par/delta display, and navigation behavior. Use bUnit's `FakeNavigationManager` for nav assertions.

**What to skip:** No integration tests needed for FEAT-005 specifically -- this feature is entirely client-side rendering of in-memory data. The integration boundary (data flowing from FEAT-001/002/003 into `SessionStateService`) will be tested by FEAT-004 integration tests.

**Edge cases to cover explicitly:**
- Void suit in a hand
- Passed-out board (no contract, no declarer, par=0)
- Analysis-failed board (null ParResult)
- First board in session (no prev)
- Last board in session (no next)
- No session context (FEAT-006 mode)
- "10" vs "T" normalization in parser

## Migration Notes

- **No database migrations.** The application is stateless with in-memory data only.
- **No breaking changes.** All new files; no existing code is modified beyond adding CSS classes and DI registration.
- **Dependency ordering:** Phase 1 (Shared types and parsing) has no dependencies on other features. Phase 2 (ISessionStateService interface) depends on domain types from FEAT-001 (Board, ParResult, BoardDelta) existing in the Shared project. Phase 3 (Blazor components) depends on Phases 1 and 2. Phase 4 (tests) depends on all prior phases.
- **Feature flag:** Not needed. The route `/boards/{boardNumber:int}` is only reachable via navigation from FEAT-004 dashboard or FEAT-006 single-hand flow. There is no entry point to accidentally expose an incomplete feature.
- **Backwards compatibility with existing console app:** The current `BridgeDDCalculator` console project is a placeholder. The new Blazor project structure (per ADR-001) will supersede it. Coordinate with whichever feature first scaffolds the Blazor solution structure.
