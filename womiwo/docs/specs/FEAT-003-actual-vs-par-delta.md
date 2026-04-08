# Feature Specification: Actual-vs-Par Delta Calculation

<!--
  Covers computing the IMP delta between the actual contract result and the par score
  for each board. Technology-agnostic.
-->

## 1. Overview

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| Feature ID      | FEAT-003                                       |
| Status          | Draft                                          |
| Author          | Team                                           |
| Created         | 2026-04-08                                     |
| Last updated    | 2026-04-08                                     |
| Epic / Parent   | DD Analysis Engine                             |
| Arc42 reference | 5. Building Blocks, 8. Crosscutting Concepts   |

### 1.1 Problem Statement

Knowing the par score for a board tells you what optimal play achieves, but it doesn't tell you how far your actual result deviated from it. Players need a delta — expressed in IMPs using the standard WBF IMP scale — that shows whether they gained or lost relative to par, and by how much. Without this delta, users still have to do the mental arithmetic themselves, which is exactly the problem the tool is trying to solve.

### 1.2 Goal

For each board in a session (or a single entered hand), compute the IMP delta between the actual score and the par score, where positive means the NS side outperformed par and negative means they underperformed.

### 1.3 Non-Goals

- Matchpoint (MP/percentage) scoring — only IMPs are in scope for MVP
- Running session total (displayed in FEAT-004, not computed here beyond individual board deltas)
- Field result comparison (deferred to 1.0)
- Bidding analysis or "could have done better" suggestions

---

## 2. User Stories

### US-001: See my result vs par per board

**As a** Post-Tournament Reviewer,
**I want** to see, for each board, how many IMPs I gained or lost compared to double-dummy par,
**so that** I can quickly identify which boards cost or earned me the most IMPs.

### US-002: See delta for a manually entered hand

**As a** Post-Tournament Reviewer,
**I want** to see the IMP delta when I enter a single hand with the contract and result I played,
**so that** I can judge how far off par my actual result was on that hand.

---

## 3. Functional Requirements

| ID     | Requirement                                                                                                              | Priority | User Story |
| ------ | ------------------------------------------------------------------------------------------------------------------------ | -------- | ---------- |
| FR-001 | The system shall compute the actual bridge score (in points) from the contract, declarer, vulnerability, and result.     | Must     | US-001, US-002 |
| FR-002 | The system shall compute the IMP delta between the actual score and the par score using the standard WBF IMP table.      | Must     | US-001, US-002 |
| FR-003 | The system shall express the delta from the NS perspective: positive = NS beats par, negative = NS loses to par.         | Must     | US-001, US-002 |
| FR-004 | The system shall return a delta of 0 IMPs for a passed-out board (par = 0, actual = 0).                                  | Must     | US-001, US-002 |
| FR-005 | The system shall handle boards where the contract was not played (missing result): delta is undefined/not shown.         | Must     | US-001     |
| FR-006 | The system shall correctly handle EW-declaring contracts (converting EW score to NS perspective before IMP lookup).      | Must     | US-001, US-002 |
| FR-007 | The system shall compute deltas for all boards in a session in under 100 ms (pure arithmetic, no I/O).                   | Must     | US-001     |

---

## 4. Acceptance Scenarios

### SC-001: NS makes overtrick above par (FR-001, FR-002, FR-003)

```gherkin
Given board 1 is NS non-vulnerable
  And the par contract is 4♠ by North = +420 (10 tricks)
  And North actually bid 4♠ and made 11 tricks (+450)
When the system computes the delta
Then the actual score is +450 NS
  And the difference is 30 points
  And the IMP delta is +1 (the WBF IMP value for a 30-point difference)
```

### SC-002: NS goes down below par (FR-001, FR-002, FR-003)

```gherkin
Given board 2 is NS vulnerable
  And the par contract is 3NT by North = +600 (9 tricks)
  And North actually bid 3NT and made only 8 tricks (−100)
When the system computes the delta
Then the actual score is −100 NS
  And the difference is 700 points
  And the IMP delta is −12
```

### SC-003: EW declaring contract (FR-006)

```gherkin
Given board 3 is EW non-vulnerable
  And the par contract is 4♥ by East = −420 from NS perspective
  And East actually bid 4♥ and made exactly 10 tricks (EW +420 = NS −420)
When the system computes the delta
Then the NS actual score is −420
  And the par score is −420
  And the IMP delta is 0
```

### SC-004: Passed-out board (FR-004)

```gherkin
Given board 4 has no contract (passed out)
When the system computes the delta
Then the actual score is 0
  And the par score is 0
  And the IMP delta is 0
```

### SC-005: Board with no contract result in PBN (FR-005)

```gherkin
Given board 5 has a contract but no Result tag in the PBN file
When the system computes the delta
Then the delta for board 5 is shown as "N/A" or left blank
  And all other boards are computed normally
```

---

## 5. Domain Model

### 5.1 Entities

#### BoardDelta

The computed delta for a single board.

| Attribute    | Type     | Constraints        | Description                                          |
| ------------ | -------- | ------------------ | ---------------------------------------------------- |
| boardNumber  | integer  | required           | Links to the Board                                   |
| actualScore  | integer? | nullable           | Actual score in points, NS perspective               |
| parScore     | integer  | required           | Par score in points, NS perspective (from FEAT-002)  |
| impDelta     | integer? | nullable, −24..+24 | IMP delta (positive = NS beats par); null if no result |

### 5.2 Relationships

- Each **Board** (FEAT-001) produces exactly one **BoardDelta**.
- **BoardDelta** depends on **ParResult** (FEAT-002) for the par score.

### 5.3 Value Objects

#### ImpTable

The standard WBF IMP scale (read-only lookup):

| Score difference | IMPs |
| ---------------- | ---- |
| 0–10             | 0    |
| 20–40            | 1    |
| 50–80            | 2    |
| 90–120           | 3    |
| 130–160          | 4    |
| 170–210          | 5    |
| 220–260          | 6    |
| 270–310          | 7    |
| 320–360          | 8    |
| 370–420          | 9    |
| 430–490          | 10   |
| 500–590          | 11   |
| 600–740          | 12   |
| 750–890          | 13   |
| 900–1090         | 14   |
| 1100–1290        | 15   |
| 1300–1490        | 16   |
| 1500–1740        | 17   |
| 1750–1990        | 18   |
| 2000–2240        | 19   |
| 2250–2490        | 20   |
| 2500+            | 24   |

### 5.4 Domain Rules and Invariants

- **NS perspective**: All scores (actual and par) are expressed from the NS perspective. EW scores must be negated before comparison.
- **IMP scale symmetry**: The IMP table applies to the absolute difference; the sign of the delta is determined by whether actual > par (positive) or actual < par (negative).
- **Passed-out invariant**: For a passed-out board, actualScore = 0, parScore = 0, impDelta = 0.
- **No result → no delta**: If the board has a contract but no result recorded, impDelta is null (undefined), not 0.
- **Bridge scoring rules**: The actual score computation follows WBF duplicate scoring rules (vulnerable/non-vulnerable, level, strain, doubled/redoubled, undertricks, overtricks).

---

## 6. Non-Functional Requirements

| ID      | Category    | Requirement                                                          |
| ------- | ----------- | -------------------------------------------------------------------- |
| NFR-001 | Performance | Delta computation for all boards in a 28-board session < 100 ms.    |
| NFR-002 | Correctness | Score and IMP calculations must conform to WBF Laws of Duplicate Bridge scoring tables. |

---

## 7. Edge Cases and Error Scenarios

| ID   | Scenario                                              | Expected Behavior                                                        |
| ---- | ----------------------------------------------------- | ------------------------------------------------------------------------ |
| EC-1 | Doubled contract making with overtricks               | Apply correct doubled/redoubled scoring before IMP lookup.               |
| EC-2 | NS vulnerability affects scoring                      | Use correct vulnerable/non-vulnerable scoring tables for NS and EW.      |
| EC-3 | EW declares, EW vulnerable                            | Convert EW score to NS perspective (negate), then apply IMP table.       |
| EC-4 | Board has no result (missing from PBN)                | impDelta = null; display as "N/A" in the UI.                             |
| EC-5 | Score difference lands exactly on IMP boundary (e.g., 40 points = 1 IMP, 50 points = 2 IMPs) | Use the IMP table exactly as defined by WBF (lower bound inclusive). |
| EC-6 | Score difference > 2500 (e.g., grand slam swing)      | Return 24 IMPs as per WBF scale ceiling.                                 |

---

## 8. Success Criteria

| ID     | Criterion                                                                               |
| ------ | --------------------------------------------------------------------------------------- |
| SC-001 | All acceptance scenarios pass.                                                           |
| SC-002 | Delta computation results match hand-calculated WBF IMP values for 10 reference boards. |
| SC-003 | Delta computation for a 28-board session completes in under 100 ms.                    |
| SC-004 | Boards with missing results display "N/A" delta without error.                         |

---

## 9. Dependencies and Constraints

### 9.1 Dependencies

- **FEAT-001** (PBN File Upload): provides Board (contract, declarer, vulnerability, result).
- **FEAT-002** (DD Analysis Engine): provides ParResult (par score) for each board.

### 9.2 Constraints

- IMP scale used is the official WBF duplicate scoring scale; no alternative scoring methods.
- Delta calculation is purely arithmetic — no external service calls.

### 9.3 Architecture References

| Arc42 Section                    | Relevance to This Feature                                  |
| -------------------------------- | ---------------------------------------------------------- |
| 5. Building Block View           | Delta Calculator component                                 |
| 8. Crosscutting Concepts         | Error/null handling for boards with missing data           |

---

## 10. Open Questions

| #   | Question                                                                     | Owner | Status | Resolution |
| --- | ---------------------------------------------------------------------------- | ----- | ------ | ---------- |
| 1   | Should we display IMPs as whole numbers only, or allow fractional IMPs for cross-IMP formats? | Product | Open | WBF IMPs are always whole numbers — likely resolved as whole numbers. |

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
