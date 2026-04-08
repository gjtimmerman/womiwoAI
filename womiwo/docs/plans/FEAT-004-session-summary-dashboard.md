# Implementation Plan: Session Summary Dashboard

**Spec:** `docs/specs/FEAT-004-session-summary-dashboard.md`
**ADR:** `docs/architecture/adr/001-technology-stack.md`
**Created:** 2026-04-08
**Status:** Draft

## Summary

Implement the Session Summary Dashboard as a set of Blazor WASM components in the `BridgeGameCalculator.Client` project, backed by shared DTOs in `BridgeGameCalculator.Shared` and a scoped `SessionStateService` that holds the current analysis result in memory. The dashboard renders after the upload page calls the Server's analyze endpoint. It displays all boards in a color-coded table, aggregate session totals, and supports click-through navigation to the board detail route.

## Key Design Decisions

1. **Shared DTOs, not duplicated models.** `SessionAnalysisResult` and `BoardResult` live in `BridgeGameCalculator.Shared` so both Server and Client reference them directly. The Server already returns these from the analyze endpoint; the Client deserializes into the same types. No mapping layer needed.

2. **Scoped `SessionStateService` for in-memory navigation state.** A DI-registered scoped service holds the `SessionAnalysisResult` after analysis completes. This avoids passing the entire result through URL parameters and lets the board detail view (FEAT-005) access individual boards without a second API call. Scoped lifetime in Blazor WASM means the service lives for the duration of the browser tab, which matches the "stateless, in-memory" constraint.

3. **Session summary bar at the top.** The open question in the spec (top vs bottom) is resolved as top, because the user's first question is "how did I do overall?" -- the summary should be visible without scrolling.

4. **Three components, one page.** `SessionDashboard.razor` is the routable page (`/session`). It composes `SessionSummary.razor` (aggregate stats) and `BoardRow.razor` (one per board). This keeps each component focused and independently testable with bUnit.

5. **CSS classes on the delta cell, not the entire row.** Color coding applies only to the IMP delta column via `.delta-positive`, `.delta-negative`, and `.delta-neutral`. The delta text is also bold (`font-weight: 700`) per NFR-003. The `+`/`-` sign prefix satisfies the accessibility requirement from FEAT-005's NFR-004.

6. **`NavigationManager` for board detail navigation.** Clicking a `BoardRow` calls `NavigationManager.NavigateTo($"/boards/{boardNumber}")`. The board detail route is defined in FEAT-005, but the navigation call is implemented here. No custom event bus or message passing needed.

7. **bUnit tests validate rendering and interaction.** Tests inject a mock `SessionStateService` with known data and verify rendered HTML structure, CSS class assignment, row count, summary arithmetic, and click-navigation calls. No HTTP mocking is needed -- the dashboard is purely a rendering layer over the state service.

8. **Upload page orchestration.** The existing upload page (FEAT-001) is modified to: call the analyze API, set `SessionStateService.CurrentSession`, then navigate to `/session`. This wiring is a small change to the upload page, not a new component.

## Implementation Steps

### Phase 1: Shared DTOs

Define the data contracts that the Server returns and the Client consumes. These are the foundation that all subsequent phases build on.

**Step 1.1 -- Create `SessionAnalysisResult` DTO**

- **File:** `BridgeGameCalculator.Shared/Models/SessionAnalysisResult.cs`
- Define a record (or class) with properties matching the spec's SessionAnalysis entity:
  - `string SourceFile`
  - `int BoardCount`
  - `IReadOnlyList<BoardResult> BoardResults`
  - `int TotalImps`
  - `int PositiveCount`
  - `int NegativeCount`
  - `int ParCount`
- Use `System.Text.Json` serialization attributes if needed (Blazor WASM uses System.Text.Json by default).

**Step 1.2 -- Create `BoardResult` DTO**

- **File:** `BridgeGameCalculator.Shared/Models/BoardResult.cs`
- Define a record with properties:
  - `int BoardNumber`
  - `string Vulnerability` (string representation: "None", "NS", "EW", "Both")
  - `string? ContractPlayed` (human-readable, e.g., "4S by N", or "Pass", or null)
  - `string? Result` (e.g., "=", "+1", "-2", or null)
  - `int? ActualScore`
  - `string? ParContract`
  - `int ParScore`
  - `int? ImpDelta` (null when result is missing or analysis failed)

**Step 1.3 -- Create `Vulnerability` enum (if not already defined by FEAT-001)**

- **File:** `BridgeGameCalculator.Shared/Models/Vulnerability.cs`
- Enum: `None`, `NS`, `EW`, `Both`
- If FEAT-001 already defines this in Shared, reuse it. The `BoardResult` DTO can use either the enum or a string; use the enum for type safety and a `[JsonConverter]` if needed for serialization.

### Phase 2: Client State Management

**Step 2.1 -- Create `SessionStateService`**

- **File:** `BridgeGameCalculator.Client/Services/SessionStateService.cs`
- A plain C# class (no interface needed for MVP -- YAGNI):
  ```csharp
  public class SessionStateService
  {
      public SessionAnalysisResult? CurrentSession { get; set; }

      public BoardResult? GetBoard(int boardNumber)
          => CurrentSession?.BoardResults.FirstOrDefault(b => b.BoardNumber == boardNumber);

      public bool HasSession => CurrentSession is not null;
  }
  ```
- `GetBoard` is a convenience method used by FEAT-005 (board detail) -- define it now to avoid revisiting this class later.

**Step 2.2 -- Register `SessionStateService` in DI**

- **File:** `BridgeGameCalculator.Client/Program.cs`
- Add: `builder.Services.AddScoped<SessionStateService>();`
- Scoped in Blazor WASM means singleton per tab, which is exactly the desired lifetime.

### Phase 3: Dashboard Components

**Step 3.1 -- Create `SessionDashboard.razor` (routable page)**

- **File:** `BridgeGameCalculator.Client/Pages/SessionDashboard.razor`
- Route: `@page "/session"`
- Inject `SessionStateService` and `NavigationManager`.
- **OnInitialized**: if `SessionStateService.HasSession` is false, redirect to the upload page (`/`) -- guard against direct URL access.
- **Layout**:
  1. Header: file name and board count from `CurrentSession.SourceFile` and `CurrentSession.BoardCount` (FR-008).
  2. `<SessionSummary>` component, passing `CurrentSession`.
  3. `<table>` with `<thead>` defining columns: `#`, `Vul`, `Contract`, `Result`, `Actual`, `Par Contract`, `Par Score`, `IMPs`.
  4. `<tbody>`: for each `BoardResult` in `CurrentSession.BoardResults`, render a `<BoardRow>` component.
- Use `@key="board.BoardNumber"` on `BoardRow` for efficient diffing.

**Step 3.2 -- Create `BoardRow.razor` (child component)**

- **File:** `BridgeGameCalculator.Client/Components/BoardRow.razor`
- Parameters:
  - `[Parameter] public BoardResult Board { get; set; }`
  - `[Parameter] public EventCallback<int> OnBoardClicked { get; set; }`
- Renders a `<tr>` with `@onclick` calling `OnBoardClicked.InvokeAsync(Board.BoardNumber)`.
- Add `role="button"` and `style="cursor: pointer"` for UX clarity.
- Columns:
  - `Board.BoardNumber`
  - `Board.Vulnerability` (display the enum value or formatted string)
  - `Board.ContractPlayed ?? "Pass"`
  - `Board.Result ?? "N/A"`
  - `Board.ActualScore?.ToString() ?? "N/A"`
  - `Board.ParContract ?? "Pass"`
  - `Board.ParScore`
  - IMP delta cell: apply CSS class based on value:
    - `Board.ImpDelta > 0` -> class `delta-positive`, display `$"+{Board.ImpDelta}"`
    - `Board.ImpDelta < 0` -> class `delta-negative`, display `$"{Board.ImpDelta}"` (negative sign is inherent)
    - `Board.ImpDelta == 0` -> class `delta-neutral`, display `"0"`
    - `Board.ImpDelta == null` -> class `delta-neutral`, display `"N/A"`
- The delta `<td>` also gets CSS class `delta-value` for the shared bold styling.

**Step 3.3 -- Create `SessionSummary.razor` (child component)**

- **File:** `BridgeGameCalculator.Client/Components/SessionSummary.razor`
- Parameters:
  - `[Parameter] public SessionAnalysisResult Session { get; set; }`
- Renders a summary bar (a `<div class="session-summary">`) containing:
  - Total IMPs: `Session.TotalImps` with color class (positive/negative/neutral) and `+`/`-` prefix.
  - Positive boards: `Session.PositiveCount` with label "boards above par".
  - Negative boards: `Session.NegativeCount` with label "boards below par".
  - Par boards: `Session.ParCount` with label "boards at par".
- Layout: horizontal flex container with four stat cards.

**Step 3.4 -- Wire `BoardRow` click to navigation**

- In `SessionDashboard.razor`, define the handler:
  ```csharp
  private void NavigateToBoard(int boardNumber)
  {
      NavigationManager.NavigateTo($"/boards/{boardNumber}");
  }
  ```
- Pass `OnBoardClicked="NavigateToBoard"` to each `<BoardRow>`.

### Phase 4: CSS Styling

**Step 4.1 -- Create dashboard CSS**

- **File:** `BridgeGameCalculator.Client/Pages/SessionDashboard.razor.css` (component-scoped CSS)
- Styles for the table, header, and layout.

**Step 4.2 -- Create shared delta color classes in the app-wide stylesheet**

- **File:** `BridgeGameCalculator.Client/wwwroot/css/app.css` (or create it if it does not exist)
- Add these classes, which are used by both the dashboard (FEAT-004) and the board detail view (FEAT-005):
  ```css
  .delta-positive {
      color: #2e7d32;
  }

  .delta-negative {
      color: #c62828;
  }

  .delta-neutral {
      color: #757575;
  }

  .delta-value {
      font-weight: 700;
  }
  ```
- These go in the global stylesheet because FEAT-005 reuses the same color-coding convention. Scoped CSS would force duplication.

**Step 4.3 -- Style the session summary bar**

- **File:** `BridgeGameCalculator.Client/Components/SessionSummary.razor.css` (component-scoped)
- `.session-summary` -- flexbox row, gap between stat cards, border-bottom separator.
- `.stat-card` -- minimal card styling: padding, centered text, label below value.
- Apply `.delta-positive`/`.delta-negative`/`.delta-neutral` to the total IMPs value (these are global classes, accessible from scoped CSS via `::deep` if needed, or applied directly since they are in app.css).

**Step 4.4 -- Style the board row hover**

- **File:** `BridgeGameCalculator.Client/Components/BoardRow.razor.css` (component-scoped)
- `tr:hover` -- subtle background highlight to indicate clickability.

### Phase 5: Upload Page Integration

**Step 5.1 -- Modify the upload page to set state and navigate**

- **File:** `BridgeGameCalculator.Client/Pages/Upload.razor` (created by FEAT-001, or create a placeholder)
- After the HTTP POST to the analyze endpoint returns `SessionAnalysisResult`:
  1. `SessionStateService.CurrentSession = result;`
  2. `NavigationManager.NavigateTo("/session");`
- This wiring depends on FEAT-001's upload page existing. If it does not exist yet, create a minimal stub that can be fleshed out when FEAT-001 is implemented:
  - Route: `@page "/"`
  - A file input and a button that, for now, loads hardcoded test data into `SessionStateService` and navigates to `/session`. This enables FEAT-004 development and testing independently of FEAT-001/002/003.

### Phase 6: bUnit Tests

All tests use xUnit + bUnit, consistent with ADR-001.

**Step 6.1 -- Create the test project (if it does not exist)**

- **File:** `BridgeGameCalculator.Client.Tests/BridgeGameCalculator.Client.Tests.csproj`
- Target: `net8.0`
- Package references: `bunit` (latest stable), `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`.
- Project reference: `BridgeGameCalculator.Client`, `BridgeGameCalculator.Shared`.

**Step 6.2 -- Create a test data builder**

- **File:** `BridgeGameCalculator.Client.Tests/Helpers/TestSessionBuilder.cs`
- A builder class that creates a `SessionAnalysisResult` with configurable boards for test scenarios:
  - `WithBoard(int number, int? impDelta, ...)` -- adds a board with specified values.
  - `Build()` -- computes `TotalImps`, `PositiveCount`, `NegativeCount`, `ParCount` from the board list and returns the result.
- Prevents test boilerplate duplication across all dashboard and future board-detail tests.

**Step 6.3 -- `SessionDashboard` rendering tests**

- **File:** `BridgeGameCalculator.Client.Tests/Pages/SessionDashboardTests.cs`
- Tests:
  1. **Renders correct number of rows.** Given 5 boards in the state service, assert 5 `<tr>` elements in `<tbody>`.
  2. **Redirects when no session.** Given `CurrentSession` is null, assert `NavigationManager` was called with `/`.
  3. **Displays file name and board count.** Assert header text contains `SourceFile` and `BoardCount`.
  4. **Rows are ordered by board number.** Assert the board numbers in rendered rows match ascending order.

**Step 6.4 -- `BoardRow` rendering and interaction tests**

- **File:** `BridgeGameCalculator.Client.Tests/Components/BoardRowTests.cs`
- Tests:
  1. **Renders all columns.** Given a `BoardResult`, assert all 8 column values are present in the rendered `<tr>`.
  2. **Positive delta gets correct CSS class.** Given `ImpDelta = 3`, assert the delta `<td>` has class `delta-positive` and text `+3`.
  3. **Negative delta gets correct CSS class.** Given `ImpDelta = -5`, assert class `delta-negative` and text `-5`.
  4. **Zero delta gets neutral class.** Given `ImpDelta = 0`, assert class `delta-neutral` and text `0`.
  5. **Null delta shows N/A.** Given `ImpDelta = null`, assert class `delta-neutral` and text `N/A`.
  6. **Click invokes callback with board number.** Click the `<tr>`, assert `OnBoardClicked` was invoked with the correct board number.
  7. **Missing contract shows Pass.** Given `ContractPlayed = null`, assert "Pass" is rendered.

**Step 6.5 -- `SessionSummary` rendering tests**

- **File:** `BridgeGameCalculator.Client.Tests/Components/SessionSummaryTests.cs`
- Tests:
  1. **Displays total IMPs with correct sign and color.** Given `TotalImps = 5`, assert text `+5` with class `delta-positive`.
  2. **Displays negative total.** Given `TotalImps = -3`, assert text `-3` with class `delta-negative`.
  3. **Displays zero total.** Given `TotalImps = 0`, assert text `0` with class `delta-neutral`.
  4. **Displays board counts.** Assert `PositiveCount`, `NegativeCount`, and `ParCount` values are rendered.

**Step 6.6 -- Edge case tests**

- **File:** `BridgeGameCalculator.Client.Tests/Pages/SessionDashboardEdgeCaseTests.cs`
- Tests:
  1. **Single board session.** One board renders one row; summary still displays.
  2. **All boards passed out.** All rows show "Pass", all deltas show "0", total is 0.
  3. **All boards have null delta.** All delta cells show "N/A", total shows 0.
  4. **Large IMP swing (24 IMPs).** Renders the number correctly, applies `delta-positive` or `delta-negative`.

## File Inventory

### New Files

| File | Purpose |
|------|---------|
| `BridgeGameCalculator.Shared/Models/SessionAnalysisResult.cs` | Session analysis result DTO |
| `BridgeGameCalculator.Shared/Models/BoardResult.cs` | Per-board result DTO |
| `BridgeGameCalculator.Shared/Models/Vulnerability.cs` | Vulnerability enum (if not from FEAT-001) |
| `BridgeGameCalculator.Client/Services/SessionStateService.cs` | Scoped state holder for current session |
| `BridgeGameCalculator.Client/Pages/SessionDashboard.razor` | Routable dashboard page at `/session` |
| `BridgeGameCalculator.Client/Pages/SessionDashboard.razor.css` | Scoped styles for dashboard layout |
| `BridgeGameCalculator.Client/Components/BoardRow.razor` | Table row component for a single board |
| `BridgeGameCalculator.Client/Components/BoardRow.razor.css` | Scoped hover styles for board row |
| `BridgeGameCalculator.Client/Components/SessionSummary.razor` | Aggregate stats summary bar |
| `BridgeGameCalculator.Client/Components/SessionSummary.razor.css` | Scoped styles for summary bar |
| `BridgeGameCalculator.Client/wwwroot/css/app.css` | Global CSS with delta color classes |
| `BridgeGameCalculator.Client.Tests/BridgeGameCalculator.Client.Tests.csproj` | bUnit test project |
| `BridgeGameCalculator.Client.Tests/Helpers/TestSessionBuilder.cs` | Test data builder |
| `BridgeGameCalculator.Client.Tests/Pages/SessionDashboardTests.cs` | Dashboard rendering tests |
| `BridgeGameCalculator.Client.Tests/Pages/SessionDashboardEdgeCaseTests.cs` | Dashboard edge case tests |
| `BridgeGameCalculator.Client.Tests/Components/BoardRowTests.cs` | Board row rendering + click tests |
| `BridgeGameCalculator.Client.Tests/Components/SessionSummaryTests.cs` | Summary bar rendering tests |

### Modified Files

| File | Change |
|------|--------|
| `BridgeGameCalculator.Client/Program.cs` | Register `SessionStateService` as scoped |
| `BridgeGameCalculator.Client/Pages/Upload.razor` | Set `SessionStateService.CurrentSession` on analysis success, navigate to `/session` |

### Notes on Project Structure

The ADR specifies three projects (`Server`, `Client`, `Shared`) but the repo currently contains only a placeholder `BridgeDDCalculator` console app. The projects listed above assume the solution restructuring has been done (either by FEAT-001's plan or as a prerequisite). If the `BridgeGameCalculator.Client` and `BridgeGameCalculator.Shared` projects do not yet exist when implementation begins, create them first following the ADR-001 structure:

```
BridgeGameCalculator.sln
  BridgeGameCalculator.Server/
  BridgeGameCalculator.Client/    <-- Blazor WASM
  BridgeGameCalculator.Shared/    <-- Shared DTOs
  BridgeGameCalculator.Client.Tests/
```

## Testing Strategy

### Component Tests (bUnit)

All dashboard UI testing is done via bUnit. This is the right tool because the dashboard is a pure rendering layer -- it takes a `SessionAnalysisResult` from the state service and projects it into HTML. There is no HTTP, no database, no native interop.

**Test setup pattern:**
1. Create a `TestContext`.
2. Register `SessionStateService` with pre-populated test data (using `TestSessionBuilder`).
3. Register a `FakeNavigationManager` (bUnit provides this automatically).
4. Render the component under test.
5. Assert on rendered markup (CSS classes, text content, element counts).

**What to verify per component:**
- `BoardRow`: correct column rendering for all data states (present, null, zero, negative, positive), CSS class assignment on the delta cell, click event propagation.
- `SessionSummary`: correct total display with sign prefix, correct board counts, correct color class on total.
- `SessionDashboard`: correct row count, board ordering, redirect on missing session, file name display.

### Edge Cases to Cover

- Null `ImpDelta` (no result / DDS error) -- renders "N/A", excluded from totals.
- Zero `ImpDelta` -- renders "0" with neutral styling, counted in `ParCount`.
- Single-board session -- summary and table both render.
- All boards passed out -- all zeroes, no errors.
- Large swings (24 IMPs) -- renders correctly, no truncation.

### What is NOT tested here

- HTTP integration between Client and Server (covered by FEAT-001/002/003 integration tests).
- Server-side analysis correctness (covered by FEAT-002 and FEAT-003 unit tests).
- Board detail view rendering (covered by FEAT-005's tests).

## Migration Notes

- **No database migration.** The application is stateless and in-memory per the project constraints.
- **No breaking changes.** This feature adds new components and DTOs. It does not modify any existing domain entities.
- **Feature flag: not needed.** The dashboard is the natural destination after analysis. There is no pre-existing flow to protect.
- **Incremental development.** Phase 1 (DTOs) and Phase 2 (state service) can be built and unit-tested before any Blazor components exist. Phase 3 (components) can be developed using a hardcoded stub in the upload page, decoupling FEAT-004 from FEAT-001/002/003 completion. Phase 6 (tests) should be written alongside Phase 3, not deferred.
- **FEAT-005 dependency.** The click handler on `BoardRow` navigates to `/boards/{boardNumber}`, a route defined by FEAT-005. If FEAT-005 is not yet implemented, the navigation will land on a "not found" page. This is acceptable during development. Do not create a placeholder board detail page here -- that is FEAT-005's responsibility.
