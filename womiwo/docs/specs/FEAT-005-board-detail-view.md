# Feature Specification: Board Detail View

<!--
  Covers the per-board drill-down view showing the hand diagram, contract,
  par contract, and IMP delta. Shared between session drill-down and single-hand analysis.
  Technology-agnostic.
-->

## 1. Overview

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| Feature ID      | FEAT-005                                       |
| Status          | Draft                                          |
| Author          | Team                                           |
| Created         | 2026-04-08                                     |
| Last updated    | 2026-04-08                                     |
| Epic / Parent   | Results Display                                |
| Arc42 reference | 5. Building Blocks, 6. Runtime View            |

### 1.1 Problem Statement

The session dashboard (FEAT-004) shows whether the player gained or lost IMPs on each board, but not what the hand actually looked like. Without seeing the four hands and the par contract, the player cannot understand why the result differed from par or what the optimal line of play was. The detail view bridges this gap.

### 1.2 Goal

Show the full hand diagram (all four hands in compass layout) alongside board metadata, the actual contract and result, the par contract and score, and the IMP delta — so the player can instantly understand what happened on a given board and compare their result to optimal play.

### 1.3 Non-Goals

- Full 20-combination DD table display (deferred to 1.0)
- Play-by-play card animation or suit-by-suit analysis
- Bidding sequence display
- Editing the contract or result from the detail view
- Sharing or exporting individual board views

---

## 2. User Stories

### US-001: Drill into a specific board

**As a** Post-Tournament Reviewer,
**I want** to see the four hands in a compass layout along with the par contract and my actual result,
**so that** I understand exactly what the optimal play was and how my result compared.

### US-002: Navigate between boards

**As a** Post-Tournament Reviewer,
**I want** to step through boards with previous/next navigation,
**so that** I can review consecutive boards without returning to the dashboard each time.

### US-003: View a single entered hand

**As a** Post-Tournament Reviewer,
**I want** the same detail view to appear after I enter a single hand manually,
**so that** I have a consistent experience whether I uploaded a session or entered a hand directly.

---

## 3. Functional Requirements

| ID     | Requirement                                                                                                               | Priority | User Story |
| ------ | ------------------------------------------------------------------------------------------------------------------------- | -------- | ---------- |
| FR-001 | The system shall display all four hands in a compass layout (North at top, South at bottom, West at left, East at right). | Must     | US-001, US-003 |
| FR-002 | Each hand shall show cards grouped by suit (♠ ♥ ♦ ♣), ordered high-to-low within each suit (A K Q J T 9 … 2).           | Must     | US-001, US-003 |
| FR-003 | Heart (♥) and Diamond (♦) suit symbols/cards shall be displayed in red; Spades (♠) and Clubs (♣) in black.               | Must     | US-001, US-003 |
| FR-004 | The system shall display board metadata: board number, dealer, and vulnerability.                                         | Must     | US-001, US-003 |
| FR-005 | The system shall display the actual contract and result (e.g., "4♠ by South, +1 overtrick, +650 NS") or "Passed out".    | Must     | US-001, US-003 |
| FR-006 | The system shall display the par contract and par score (e.g., "Par: 4♠ by South = +620 NS") or "Par: Pass (0)".         | Must     | US-001, US-003 |
| FR-007 | The system shall display the IMP delta with color coding (green/red/neutral) as defined in FEAT-004.                     | Must     | US-001, US-003 |
| FR-008 | The system shall provide "← Previous board" and "Next board →" navigation buttons when viewing a board within a session. | Must     | US-002     |
| FR-009 | The system shall provide a "Back to session" link that returns to the dashboard (FEAT-004).                               | Must     | US-002     |
| FR-010 | The board detail view shall be the output view for single-hand analysis (FEAT-006) with the same layout.                 | Must     | US-003     |
| FR-011 | The detail view shall render instantly when navigating from the dashboard (data already in memory).                       | Must     | US-001, US-002 |

---

## 4. Acceptance Scenarios

### SC-001: Hand diagram displayed correctly (FR-001, FR-002, FR-003)

```gherkin
Given board 5 with known card distributions for all four hands
When the user opens the board detail view for board 5
Then North's cards appear at the top, South at the bottom, West at left, East at right
  And each hand's cards are grouped by suit in order ♠ ♥ ♦ ♣
  And cards within each suit are ordered A K Q J T 9 8 7 6 5 4 3 2
  And ♥ and ♦ cards are displayed in red, ♠ and ♣ in black
```

### SC-002: Contract and par information displayed (FR-005, FR-006, FR-007)

```gherkin
Given board 7 where NS played 4♠ by South making +1 (NS +650, non-vulnerable)
  And the par is 4♠ by South = NS +620
When the user views the board detail
Then the view shows "4♠ by South, +1, +650 NS"
  And the view shows "Par: 4♠ by South = +620 NS"
  And the IMP delta "+1" is displayed in green
```

### SC-003: Passed-out board (FR-005, FR-006)

```gherkin
Given board 3 is a passed-out board
When the user views the board detail
Then the actual contract shows "Passed out"
  And the par shows "Par: Pass (0)"
  And the IMP delta shows "0"
```

### SC-004: Previous/next navigation (FR-008)

```gherkin
Given a 10-board session and the user is viewing board 5
When the user clicks "Next board →"
Then the detail view updates to show board 6
  And the "← Previous board" button is still present
When the user clicks "← Previous board"
Then the detail view returns to board 5
```

### SC-005: First and last board navigation (FR-008)

```gherkin
Given a 10-board session and the user is viewing board 1
Then the "← Previous board" button is disabled or hidden
Given the user is viewing board 10
Then the "Next board →" button is disabled or hidden
```

### SC-006: Render speed (FR-011)

```gherkin
Given the session analysis is complete and all board data is in memory
When the user clicks on any board row in the dashboard
Then the board detail view renders in under 200 ms
```

---

## 5. Domain Model

This feature renders data from FEAT-001 (Board, Hands), FEAT-002 (ParResult), and FEAT-003 (BoardDelta). No new domain entities are introduced; the detail view is a projection of existing data.

### 5.1 Entities

#### BoardDetailView (read model / projection)

| Attribute      | Type         | Constraints | Description                                        |
| -------------- | ------------ | ----------- | -------------------------------------------------- |
| boardNumber    | integer      | required    | Board number                                       |
| dealer         | Seat         | required    | N/E/S/W                                            |
| vulnerability  | Vulnerability| required    | None/NS/EW/Both                                    |
| hands          | Hands        | required    | Four hands with all card distributions             |
| contractPlayed | string?      | nullable    | Human-readable contract or null if passed out      |
| declarer       | Seat?        | nullable    | Declaring seat or null                             |
| result         | string?      | nullable    | "+1", "=", "−2" or null                           |
| actualScore    | integer?     | nullable    | Points, NS perspective                             |
| parContract    | string?      | nullable    | Human-readable par contract or null                |
| parScore       | integer      | required    | Points, NS perspective                             |
| impDelta       | integer?     | nullable    | IMPs, null if no result                            |
| prevBoardNumber| integer?     | nullable    | Board number of previous board; null if first      |
| nextBoardNumber| integer?     | nullable    | Board number of next board; null if last           |

### 5.2 Relationships

- **BoardDetailView** is assembled from **Board** (FEAT-001), **ParResult** (FEAT-002), and **BoardDelta** (FEAT-003).
- Part of a **SessionAnalysis** when accessed from the dashboard; standalone when accessed from single-hand entry (FEAT-006).

### 5.4 Domain Rules and Invariants

- **Card display order**: Cards within each suit must always be displayed in descending rank (A K Q J T 9 8 7 6 5 4 3 2).
- **Compass layout**: North is always top, South bottom, West left, East right — never rotated or rearranged.
- **Passed-out display**: When contract is null, show "Passed out" for the actual contract and "Par: Pass (0)" for the par contract.

---

## 6. Non-Functional Requirements

| ID      | Category   | Requirement                                                             |
| ------- | ---------- | ----------------------------------------------------------------------- |
| NFR-001 | Performance| Board detail view renders in under 200 ms when navigating from the dashboard (data is in memory). |
| NFR-002 | Usability  | The compass layout must be visually clear on a standard 13" laptop screen at 1280×800 resolution. |
| NFR-003 | Usability  | Card ranks must use standard notation: A K Q J T (not 10) for Ten.     |
| NFR-004 | Accessibility | Red/green IMP delta color coding must be accompanied by a +/− sign, not color alone. |

---

## 7. Edge Cases and Error Scenarios

| ID   | Scenario                                              | Expected Behavior                                                          |
| ---- | ----------------------------------------------------- | -------------------------------------------------------------------------- |
| EC-1 | Board has no contract result (N/A delta)              | Show actual contract without result; show "N/A" for IMP delta.             |
| EC-2 | Single-hand entry (no session context)                | Hide previous/next navigation and "back to session" link.                  |
| EC-3 | A hand is void in a suit                              | Display "—" or "(void)" for that suit in the hand diagram.                |
| EC-4 | Par has multiple equal par contracts                  | Display the first par contract; optionally show "(or equivalent)" note.    |
| EC-5 | DD analysis failed for this board                     | Show "Analysis unavailable" for par and delta; still show the hand diagram.|

---

## 8. Success Criteria

| ID     | Criterion                                                                          |
| ------ | ---------------------------------------------------------------------------------- |
| SC-001 | All acceptance scenarios pass.                                                      |
| SC-002 | Hand diagram is visually correct for 5 reference boards checked against PBN source.|
| SC-003 | Board detail renders in under 200 ms on navigation from dashboard.                 |
| SC-004 | Previous/next navigation cycles correctly through all boards in sequence.          |
| SC-005 | Single-hand entry (FEAT-006) uses the same view component without duplication.     |

---

## 9. Dependencies and Constraints

### 9.1 Dependencies

- **FEAT-001** (PBN File Upload): provides Board and Hands data.
- **FEAT-002** (DD Analysis Engine): provides ParResult.
- **FEAT-003** (Delta Calculation): provides BoardDelta.
- **FEAT-004** (Session Dashboard): the navigation source; must pass board number to this view.
- **FEAT-006** (Single-Hand Entry): uses this view as its output.

### 9.2 Constraints

- No data persistence: the view is built from in-memory analysis results.
- The view is read-only; no editing from the detail view.

### 9.3 Architecture References

| Arc42 Section                    | Relevance to This Feature                              |
| -------------------------------- | ------------------------------------------------------ |
| 5. Building Block View           | Results Display component                              |
| 6. Runtime View                  | Navigation flow from dashboard → detail                |
| 8. Crosscutting Concepts         | Accessibility conventions, color coding standards      |

---

## 10. Open Questions

| #   | Question                                                                                | Owner   | Status | Resolution |
| --- | --------------------------------------------------------------------------------------- | ------- | ------ | ---------- |
| 1   | When there are multiple equal par contracts, should all be displayed or just the first? | Product | Open   |            |

---

<!--
  CHECKLIST
  - [x] Problem statement is clear and concise
  - [x] All user stories have acceptance scenarios
  - [x] Each functional requirement traces to a user story
  - [x] Domain model covers all entities mentioned in the requirements
  - [x] Domain rules and invariants are listed
  - [x] Edge cases cover failure modes, not just happy paths
  - [x] Non-functional requirements are specific and measurable
  - [x] Arc42 references point to the right sections
  - [x] Open questions are assigned and have a resolution path
-->
