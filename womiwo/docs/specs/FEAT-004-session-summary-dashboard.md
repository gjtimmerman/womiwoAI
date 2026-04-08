# Feature Specification: Session Summary Dashboard

<!--
  Covers the board-list view shown after a full session has been analyzed.
  Technology-agnostic.
-->

## 1. Overview

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| Feature ID      | FEAT-004                                       |
| Status          | Draft                                          |
| Author          | Team                                           |
| Created         | 2026-04-08                                     |
| Last updated    | 2026-04-08                                     |
| Epic / Parent   | Results Display                                |
| Arc42 reference | 5. Building Blocks, 6. Runtime View            |

### 1.1 Problem Statement

After DD analysis completes, the player needs to scan all boards at a glance to find where they gained or lost the most IMPs. Without a summary view, they would have to click through every board individually — defeating the goal of session review under 5 minutes.

### 1.2 Goal

Display all boards in the session as a sortable list with color-coded IMP deltas, so the user can spot their biggest gains and losses instantly, and drill into any board with one click.

### 1.3 Non-Goals

- Sorting or filtering the board list (deferred to 1.0)
- Exporting or printing results (deferred to beyond 1.0)
- Matchpoint scoring display
- Field/traveller result comparison (deferred to 1.0)
- Session statistics aggregates beyond total delta (deferred to 1.0)

---

## 2. User Stories

### US-001: Scan all boards at a glance

**As a** Post-Tournament Reviewer,
**I want** to see all boards in my session listed together with their actual contract, par contract, and IMP delta,
**so that** I can quickly identify which boards had the biggest swings.

### US-002: See session total

**As a** Post-Tournament Reviewer,
**I want** to see my total IMPs gained/lost versus par for the whole session,
**so that** I have an overall picture of how I performed.

### US-003: Navigate to board detail

**As a** Post-Tournament Reviewer,
**I want** to click on a board row to go to the full board detail view,
**so that** I can examine the hand diagram and understand what happened on that board.

---

## 3. Functional Requirements

| ID     | Requirement                                                                                                          | Priority | User Story |
| ------ | -------------------------------------------------------------------------------------------------------------------- | -------- | ---------- |
| FR-001 | The system shall display a row for each board in the session, ordered by board number.                               | Must     | US-001     |
| FR-002 | Each row shall show: board number, vulnerability, contract played (or "Pass"), result, actual score, par contract, par score, and IMP delta. | Must     | US-001     |
| FR-003 | The IMP delta shall be color-coded: green for positive (NS beats par), red for negative (NS loses to par), neutral/grey for zero. | Must     | US-001     |
| FR-004 | The dashboard shall display a session summary bar showing: total IMP delta, number of boards where NS beat par, number where NS lost to par, and number at par. | Must     | US-002     |
| FR-005 | Clicking a board row shall navigate to the board detail view (FEAT-005) for that board.                              | Must     | US-003     |
| FR-006 | The dashboard shall render within 1 second of analysis completing for a 28-board session.                            | Must     | US-001     |
| FR-007 | Boards where the contract result is missing (N/A) shall display "N/A" in the delta column without error.            | Must     | US-001     |
| FR-008 | The dashboard shall display the name of the uploaded PBN file and the board count.                                   | Should   | US-001     |

---

## 4. Acceptance Scenarios

### SC-001: Full session dashboard renders (FR-001, FR-002)

```gherkin
Given a 24-board session has been analyzed successfully
When the dashboard is displayed
Then all 24 boards appear as rows, ordered by board number 1–24
  And each row shows: board number, vulnerability, contract, result, actual score, par contract, par score, and IMP delta
```

### SC-002: Color coding (FR-003)

```gherkin
Given board 5 has an IMP delta of +3 (NS beat par)
  And board 8 has an IMP delta of −5 (NS lost to par)
  And board 12 has an IMP delta of 0
When the dashboard is displayed
Then board 5's delta cell is shown in green
  And board 8's delta cell is shown in red
  And board 12's delta cell is shown in neutral/grey
```

### SC-003: Session summary totals (FR-004)

```gherkin
Given a 10-board session where NS beat par on 4 boards (+14 total), lost on 3 (−9 total), and was at par on 3
When the dashboard is displayed
Then the summary shows: Total: +5 IMPs, Positive: 4 boards, Negative: 3 boards, Par: 3 boards
```

### SC-004: Navigate to board detail (FR-005)

```gherkin
Given the session dashboard is displayed
When the user clicks on the row for board 7
Then the user is taken to the board detail view for board 7
```

### SC-005: Dashboard renders within 1 second (FR-006)

```gherkin
Given a 28-board session has just finished DD analysis
When the dashboard is rendered
Then all board rows and the summary bar are fully visible within 1 second
```

### SC-006: Board with missing result (FR-007)

```gherkin
Given board 3 has a contract but no result recorded in the PBN file
When the dashboard is displayed
Then board 3 shows "N/A" in the IMP delta column
  And the session total excludes board 3 from the IMP sum
```

---

## 5. Domain Model

### 5.1 Entities

#### SessionAnalysis

The complete analysis result for a session, aggregating board-level results.

| Attribute    | Type           | Constraints  | Description                                     |
| ------------ | -------------- | ------------ | ----------------------------------------------- |
| sourceFile   | string         | required     | PBN filename                                    |
| boardCount   | integer        | required     | Total number of boards                          |
| boardResults | BoardResult[]  | required     | One per board, ordered by board number          |
| totalImps    | integer        | required     | Sum of all non-null IMP deltas                  |
| positiveCount| integer        | required     | Boards where NS beat par                        |
| negativeCount| integer        | required     | Boards where NS lost to par                     |
| parCount     | integer        | required     | Boards at exact par                             |

#### BoardResult

The display-ready result for a single board (combines data from FEAT-001, FEAT-002, FEAT-003).

| Attribute      | Type       | Constraints | Description                                      |
| -------------- | ---------- | ----------- | ------------------------------------------------ |
| boardNumber    | integer    | required    | Board number                                     |
| vulnerability  | Vulnerability | required | NS/EW/Both/None                                 |
| contractPlayed | string?    | nullable    | Human-readable (e.g., "4♠ by N") or "Pass"      |
| result         | string?    | nullable    | e.g., "=", "+1", "−2"                           |
| actualScore    | integer?   | nullable    | Points, NS perspective                           |
| parContract    | string?    | nullable    | Human-readable par contract or "Pass"            |
| parScore       | integer    | required    | Points, NS perspective                           |
| impDelta       | integer?   | nullable    | IMPs, null if no result                          |

### 5.2 Relationships

- **SessionAnalysis** aggregates one **BoardResult** per board.
- **BoardResult** is derived from **Board** (FEAT-001), **ParResult** (FEAT-002), and **BoardDelta** (FEAT-003).

### 5.4 Domain Rules and Invariants

- **Total IMP correctness**: `totalImps` = sum of all non-null `impDelta` values in `boardResults`.
- **Count correctness**: `positiveCount + negativeCount + parCount` = number of boards with non-null impDelta.
- **Board ordering**: `boardResults` are always ordered ascending by `boardNumber`.

---

## 6. Non-Functional Requirements

| ID      | Category   | Requirement                                                        |
| ------- | ---------- | ------------------------------------------------------------------ |
| NFR-001 | Performance| Dashboard renders within 1 second of analysis completing for up to 36 boards. |
| NFR-002 | Usability  | Positive deltas shown in green, negative in red, zero in neutral — using colorblind-accessible colors. |
| NFR-003 | Usability  | The IMP delta values must be prominent — larger or bolder than surrounding data. |

---

## 7. Edge Cases and Error Scenarios

| ID   | Scenario                                        | Expected Behavior                                              |
| ---- | ----------------------------------------------- | -------------------------------------------------------------- |
| EC-1 | All boards passed out                           | Show all rows with "Pass" and 0 IMPs; total = 0.              |
| EC-2 | All boards have missing results                 | All delta cells show "N/A"; total IMPs = 0.                   |
| EC-3 | Session with 1 board                           | Show a single board row; session summary still displays.       |
| EC-4 | Very large IMP swing on one board (e.g., 24 IMPs)| Display the correct large number; color coding still applies. |
| EC-5 | Analysis failed for one board (DDS error)       | Show "Error" in that board's delta cell; exclude from total.   |

---

## 8. Success Criteria

| ID     | Criterion                                                                         |
| ------ | --------------------------------------------------------------------------------- |
| SC-001 | All acceptance scenarios pass.                                                     |
| SC-002 | Dashboard renders within 1 second for a 28-board session.                        |
| SC-003 | Session total IMP is mathematically correct for all test sessions.               |
| SC-004 | Color coding is applied consistently: green/red/neutral with no misclassifications. |

---

## 9. Dependencies and Constraints

### 9.1 Dependencies

- **FEAT-001** (PBN File Upload): provides Session and Board data.
- **FEAT-002** (DD Analysis Engine): provides ParResult per board.
- **FEAT-003** (Delta Calculation): provides BoardDelta per board.
- **FEAT-005** (Board Detail View): target of board row click navigation.

### 9.2 Constraints

- No persistent storage: all data lives in memory for the current session.
- The view is read-only; users cannot edit results from the dashboard.

### 9.3 Architecture References

| Arc42 Section                    | Relevance to This Feature                              |
| -------------------------------- | ------------------------------------------------------ |
| 5. Building Block View           | Results Display component                              |
| 6. Runtime View                  | Session analysis → dashboard rendering flow            |
| 8. Crosscutting Concepts         | UI color conventions, accessibility                    |

---

## 10. Open Questions

| #   | Question                                                                  | Owner   | Status | Resolution |
| --- | ------------------------------------------------------------------------- | ------- | ------ | ---------- |
| 1   | Should the session summary bar be at the top or bottom of the board list? | Product | Open   |            |

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
