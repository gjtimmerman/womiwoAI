# Feature Specification: Single-Hand Manual Entry

<!--
  Covers Goal G2: quick single-hand DD analysis without uploading a PBN file.
  Technology-agnostic.
-->

## 1. Overview

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| Feature ID      | FEAT-006                                       |
| Status          | Draft                                          |
| Author          | Team                                           |
| Created         | 2026-04-08                                     |
| Last updated    | 2026-04-08                                     |
| Epic / Parent   | Data Import                                    |
| Arc42 reference | 3. System Scope & Context, 5. Building Blocks  |

### 1.1 Problem Statement

A player sometimes remembers one specific interesting hand from a session — perhaps a slam missed or a difficult game — and wants to analyze just that hand quickly, without finding and uploading the full PBN file. The PBN upload path (FEAT-001) is too heavy for this use case. A dedicated manual entry form lets the player get a par score in seconds.

### 1.2 Goal

Allow the user to enter all four hands of a single bridge deal manually (cards per hand, dealer, vulnerability, and optionally the contract played), submit the form, and see the board detail view (FEAT-005) with par contract, par score, and IMP delta within 30 seconds of opening the form.

### 1.3 Non-Goals

- Entering multiple hands simultaneously (one hand at a time only)
- Saving entered hands for later retrieval (stateless tool)
- Drag-and-drop card selection UI (text input is sufficient for MVP)
- Importing hand data from other text formats (e.g., LIN notation)
- Bidding entry or play-by-play entry

---

## 2. User Stories

### US-001: Enter a hand for par analysis

**As a** Post-Tournament Reviewer,
**I want** to type in the four hands, dealer, and vulnerability,
**so that** I can get the par contract and score for that hand without needing the full PBN file.

### US-002: Enter my actual result for delta comparison

**As a** Post-Tournament Reviewer,
**I want** to optionally enter the contract I played and how many tricks I made,
**so that** I can see the IMP delta between my result and par.

### US-003: See validation errors immediately

**As a** Post-Tournament Reviewer,
**I want** the form to tell me immediately if I've entered an invalid hand (wrong card count, duplicate cards, etc.),
**so that** I can fix mistakes before submitting.

---

## 3. Functional Requirements

| ID     | Requirement                                                                                                         | Priority | User Story |
| ------ | ------------------------------------------------------------------------------------------------------------------- | -------- | ---------- |
| FR-001 | The system shall present a form with four hand-entry fields (North, East, South, West), a Dealer selector, and a Vulnerability selector. | Must     | US-001     |
| FR-002 | Each hand-entry field shall accept card input as space-separated cards per suit, e.g. "♠AKQ ♥JT9 ♦87 ♣654" or the text equivalents "S:AKQ H:JT9 D:87 C:654". | Must     | US-001     |
| FR-003 | The system shall validate that each hand contains exactly 13 cards.                                                 | Must     | US-003     |
| FR-004 | The system shall validate that all 52 cards across the four hands are unique (no duplicates, no missing cards).     | Must     | US-003     |
| FR-005 | The form shall provide optional fields for: Contract (level + strain), Declarer (N/E/S/W), and Result (tricks made 0–13). | Must     | US-002     |
| FR-006 | If Contract and Result are provided, the system shall compute and display the IMP delta (as in FEAT-003).           | Must     | US-002     |
| FR-007 | The system shall display validation errors inline next to the relevant field, without requiring a round-trip to the server. | Must     | US-003     |
| FR-008 | On successful submission, the system shall run DD analysis (FEAT-002) and display the board detail view (FEAT-005). | Must     | US-001     |
| FR-009 | The full flow from opening the form to seeing the board detail view shall complete within 30 seconds.               | Must     | US-001     |
| FR-010 | The Dealer selector shall offer N / E / S / W options.                                                              | Must     | US-001     |
| FR-011 | The Vulnerability selector shall offer None / NS / EW / Both options.                                               | Must     | US-001     |

---

## 4. Acceptance Scenarios

### SC-001: Successful single-hand analysis (FR-001, FR-002, FR-008)

```gherkin
Given the user opens the single-hand entry form
When the user enters valid hands for N/E/S/W, selects Dealer=North, Vulnerability=None
  And submits the form
Then the system runs DD analysis
  And displays the board detail view with the four hands, par contract, and par score
```

### SC-002: With actual contract and result (FR-005, FR-006)

```gherkin
Given the user enters valid hands and also enters Contract=4♠, Declarer=South, Result=10
When the user submits the form
Then the board detail view shows the actual result "4♠ by South =" and the par contract
  And the IMP delta is computed and color-coded
```

### SC-003: Without actual contract (FR-008)

```gherkin
Given the user enters valid hands but leaves the Contract fields blank
When the user submits the form
Then the board detail view shows the par contract and par score
  And the IMP delta shows "N/A"
```

### SC-004: Hand with wrong card count (FR-003, FR-007)

```gherkin
Given the user enters only 12 cards for North's hand
When the user submits the form (or the field loses focus)
Then an inline validation error appears next to the North field: "North must have exactly 13 cards"
  And the form is not submitted
```

### SC-005: Duplicate card across hands (FR-004, FR-007)

```gherkin
Given the user enters the Ace of Spades in both North's and East's hands
When the user submits the form
Then an inline error appears: "Ace of Spades appears in more than one hand"
  And the form is not submitted
```

### SC-006: Analysis completes within 30 seconds (FR-009)

```gherkin
Given the user has filled in a valid hand form
When the user submits
Then the board detail view is displayed within 30 seconds (target: under 5 seconds)
```

---

## 5. Domain Model

This feature introduces no new domain entities beyond those in FEAT-001. The four manually entered hands produce a **Board** object identical in structure to a PBN-parsed board, which is then passed to FEAT-002 and FEAT-003 in the same way.

### 5.1 Entities

#### ManualEntryForm (UI model, not persisted)

| Attribute     | Type         | Constraints                          | Description                          |
| ------------- | ------------ | ------------------------------------ | ------------------------------------ |
| northHand     | string       | required, resolves to 13 cards       | Raw text input for North's hand      |
| eastHand      | string       | required, resolves to 13 cards       | Raw text input for East's hand       |
| southHand     | string       | required, resolves to 13 cards       | Raw text input for South's hand      |
| westHand      | string       | required, resolves to 13 cards       | Raw text input for West's hand       |
| dealer        | Seat         | required                             | N/E/S/W                              |
| vulnerability | Vulnerability| required                             | None/NS/EW/Both                      |
| contract      | Contract?    | optional                             | Level + strain + doubled state       |
| declarer      | Seat?        | optional, required if contract set   | N/E/S/W                              |
| result        | integer?     | optional, 0–13                       | Tricks made by declarer              |

### 5.2 Relationships

- **ManualEntryForm** is converted to a **Board** (FEAT-001 domain model) before analysis.
- The resulting **Board** flows through FEAT-002 → FEAT-003 → FEAT-005 identically to a PBN-imported board.

### 5.4 Domain Rules and Invariants

- **Complete deck**: 52 unique cards across the four hands (same invariant as FEAT-001).
- **Contract completeness**: If any of Contract/Declarer/Result is provided, all three must be provided.
- **Result range**: Result must be 0–13 if provided.
- **Declarer consistency**: If Declarer is E or W, the actual score is negated to NS perspective (same rule as FEAT-003).

---

## 6. Non-Functional Requirements

| ID      | Category    | Requirement                                                              |
| ------- | ----------- | ------------------------------------------------------------------------ |
| NFR-001 | Performance | Full flow (form open → board detail displayed) completes in under 30 seconds. Target: under 5 seconds. |
| NFR-002 | Usability   | Validation errors appear inline without a full page reload.              |
| NFR-003 | Usability   | Card input format must be clearly documented on the form with an example. |
| NFR-004 | Usability   | The form must be completable with keyboard only (tab navigation between fields). |

---

## 7. Edge Cases and Error Scenarios

| ID   | Scenario                                           | Expected Behavior                                                        |
| ---- | -------------------------------------------------- | ------------------------------------------------------------------------ |
| EC-1 | User provides only Contract without Declarer       | Inline error: "Declarer is required when a contract is entered."         |
| EC-2 | Result entered without Contract                   | Inline error: "Contract is required when a result is entered."           |
| EC-3 | Result = 0 (complete set)                         | Valid — declarer took 0 tricks; compute score as penalties.              |
| EC-4 | Hand uses lowercase or mixed-case input            | Parser normalizes to uppercase before validation.                        |
| EC-5 | User enters "10" instead of "T" for Ten           | Accept "10" as equivalent to "T" and normalize.                          |
| EC-6 | DD analysis fails (DDS error)                     | Show error: "Analysis failed. Please check the hand data and try again."|
| EC-7 | All four hands are passed out (no contract played) | Valid — submit without contract; show par of 0 if that is the par.      |

---

## 8. Success Criteria

| ID     | Criterion                                                                             |
| ------ | ------------------------------------------------------------------------------------- |
| SC-001 | All acceptance scenarios pass.                                                         |
| SC-002 | A valid hand is entered and analyzed end-to-end in under 30 seconds.                 |
| SC-003 | Invalid hands (wrong count, duplicates) are rejected with clear inline error messages.|
| SC-004 | The resulting board detail view (FEAT-005) is identical in layout to a PBN-imported board. |

---

## 9. Dependencies and Constraints

### 9.1 Dependencies

- **FEAT-002** (DD Analysis Engine): runs par calculation on the manually entered board.
- **FEAT-003** (Delta Calculation): computes IMP delta if contract/result provided.
- **FEAT-005** (Board Detail View): the output view displayed after analysis.

### 9.2 Constraints

- No persistence: the entered hand is not saved after the session ends.
- Input format must be accessible via a standard keyboard — no drag-and-drop required for MVP.

### 9.3 Architecture References

| Arc42 Section                    | Relevance to This Feature                                    |
| -------------------------------- | ------------------------------------------------------------ |
| 3. System Scope & Context        | Manual entry is an alternate entry point alongside PBN upload|
| 5. Building Block View           | Data Import component (alternate entry path)                 |
| 8. Crosscutting Concepts         | Input validation pattern, error display                      |

---

## 10. Open Questions

| #   | Question                                                                          | Owner   | Status | Resolution |
| --- | --------------------------------------------------------------------------------- | ------- | ------ | ---------- |
| 1   | Should the form support PBN deal notation directly (e.g., "N:AKQ.JT9.87.654 ...")? | Product | Open   | Would reduce typing for users who know PBN format. |

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
