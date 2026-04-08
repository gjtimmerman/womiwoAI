# Feature Specification: DD Analysis Engine

<!--
  Covers wrapping Bo Haglund's DDS library to compute the par score for each board.
  Technology-agnostic — implementation details belong in the technical plan.
-->

## 1. Overview

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| Feature ID      | FEAT-002                                       |
| Status          | Draft                                          |
| Author          | Team                                           |
| Created         | 2026-04-08                                     |
| Last updated    | 2026-04-08                                     |
| Epic / Parent   | DD Analysis Engine                             |
| Arc42 reference | 4. Solution Strategy, 5. Building Blocks       |

### 1.1 Problem Statement

Calculating the double-dummy optimal outcome for a bridge hand by hand is a combinatorially hard problem that takes an expert player 30+ minutes per session. Bo Haglund's open-source DDS library solves this in milliseconds per board. The system must wrap this library to compute the par score for every board in an uploaded session, as well as for individually entered hands.

### 1.2 Goal

For each board (given four hands + dealer + vulnerability), compute the par contract(s) and par score using the DDS library, making results available to the delta calculation (FEAT-003) and display features (FEAT-004, FEAT-005).

### 1.3 Non-Goals

- Exposing the full 20-combination DD table to the UI (deferred to 1.0)
- Building a custom double-dummy solver
- Calculating par score without the DDS library
- Bidding analysis or suggested bidding
- Matchpoint par (only IMP/rubber-style par score is in scope)

---

## 2. User Stories

### US-001: Analyze a full session

**As a** Post-Tournament Reviewer,
**I want** the system to automatically calculate the par score for every board in my uploaded session,
**so that** I can see where I gained or lost against optimal play without doing any manual calculation.

### US-002: Analyze a single hand

**As a** Post-Tournament Reviewer,
**I want** the system to calculate the par score for a single hand I entered manually,
**so that** I can quickly explore an interesting deal in under 30 seconds.

---

## 3. Functional Requirements

| ID     | Requirement                                                                                                        | Priority | User Story |
| ------ | ------------------------------------------------------------------------------------------------------------------ | -------- | ---------- |
| FR-001 | The system shall calculate the DD trick table (all 20 strain/declarer combinations) for each board using the DDS library. | Must     | US-001, US-002 |
| FR-002 | The system shall calculate the par contract(s) and par score for each board using the DDS par function.            | Must     | US-001, US-002 |
| FR-003 | The system shall process all boards in a session (up to 36 boards) within 30 seconds.                              | Must     | US-001     |
| FR-004 | The system shall calculate the par score for a single board within 2 seconds.                                      | Must     | US-002     |
| FR-005 | The system shall correctly handle passed-out boards (par score = 0, no par contract).                              | Must     | US-001, US-002 |
| FR-006 | The system shall use the DDS batch API (CalcAllTablesPBN or equivalent) for session analysis to maximize throughput. | Should   | US-001     |
| FR-007 | The system shall propagate DDS library errors as structured error results, not crashes.                            | Must     | US-001, US-002 |

---

## 4. Acceptance Scenarios

### SC-001: Par score for a known hand (FR-001, FR-002)

```gherkin
Given board 1 with hands that have a known double-dummy par of 4♠ = by North, +420 NS, NS non-vulnerable
When the system runs DD analysis on board 1
Then the par contract is "4♠ by North" and the par score is +420
```

### SC-002: Session analyzed within 30 seconds (FR-003)

```gherkin
Given a session containing 28 boards
When the system runs DD analysis on the full session
Then all 28 par scores are available within 30 seconds
```

### SC-003: Single hand analyzed within 2 seconds (FR-004)

```gherkin
Given a single board entered manually
When the user triggers DD analysis
Then the par contract and par score are displayed within 2 seconds
```

### SC-004: Passed-out board (FR-005)

```gherkin
Given a board where both sides pass (no contract)
When the system calculates the par score
Then the par score is 0 and there is no par contract
```

### SC-005: DDS library error is handled gracefully (FR-007)

```gherkin
Given a board with internally inconsistent hand data that causes a DDS error
When the system runs DD analysis
Then the board is marked as "analysis failed" with an error message
  And all other boards in the session are analyzed normally
```

---

## 5. Domain Model

### 5.1 Entities

#### DdTable

The complete double-dummy trick result for a single board: 20 entries (4 declarers × 5 strains).

| Attribute  | Type             | Constraints                    | Description                                    |
| ---------- | ---------------- | ------------------------------ | ---------------------------------------------- |
| boardNumber| integer          | required                       | Links to the Board this table belongs to       |
| results    | DdResult[20]     | required, exactly 20 entries   | Tricks won for each (declarer, strain) pair    |

#### DdResult

| Attribute  | Type    | Constraints | Description                              |
| ---------- | ------- | ----------- | ---------------------------------------- |
| declarer   | Seat    | required    | N/E/S/W                                  |
| strain     | Strain  | required    | S/H/D/C/NT                               |
| tricks     | integer | 0–13        | Number of tricks declarer takes DD       |

#### ParResult

The par outcome for a board.

| Attribute    | Type        | Constraints | Description                                    |
| ------------ | ----------- | ----------- | ---------------------------------------------- |
| boardNumber  | integer     | required    | Links to the Board                             |
| parScore     | integer     | required    | Score in points from NS perspective            |
| parContracts | ParContract[]| required   | One or more par contracts (ties are possible)  |

#### ParContract

| Attribute  | Type    | Constraints | Description                            |
| ---------- | ------- | ----------- | -------------------------------------- |
| level      | integer | 1–7         | Contract level                         |
| strain     | Strain  | required    | S/H/D/C/NT                             |
| declarer   | Seat    | required    | Who declares                           |
| doubled    | enum    | Undoubled/Doubled | Whether the par contract is doubled |

### 5.2 Relationships

- Each **Board** (from FEAT-001) produces exactly one **DdTable** and one **ParResult**.
- A **DdTable** contains exactly 20 **DdResult** values.
- A **ParResult** contains one or more **ParContract** values.

### 5.3 Value Objects

Seat, Strain, and Contract are defined in FEAT-001 and reused here.

### 5.4 Domain Rules and Invariants

- **Complete DD table**: A DdTable must have exactly 20 entries — one per (declarer, strain) combination.
- **Tricks range**: Each DdResult.tricks must be between 0 and 13 inclusive.
- **Passed-out par**: For a passed-out board, parScore = 0 and parContracts is empty.
- **Par score sign**: Par score is always expressed from the NS perspective. A par that benefits EW is negative.

---

## 6. Non-Functional Requirements

| ID      | Category    | Requirement                                                               |
| ------- | ----------- | ------------------------------------------------------------------------- |
| NFR-001 | Performance | Full session (28 boards) analyzed in under 30 seconds; ideally under 10 s using batch DDS API. |
| NFR-002 | Performance | Single board par score calculated in under 2 seconds.                    |
| NFR-003 | Reliability | DDS library errors on individual boards must not abort the full session analysis. |
| NFR-004 | Correctness | Par scores must match the DDS reference implementation output exactly.    |

---

## 7. Edge Cases and Error Scenarios

| ID   | Scenario                                          | Expected Behavior                                                         |
| ---- | ------------------------------------------------- | ------------------------------------------------------------------------- |
| EC-1 | DDS library not found / failed to load            | Fail fast at startup with a clear error: "DD solver library could not be loaded." |
| EC-2 | Board hands are inconsistent (52 cards not unique)| Return a structured error for that board; log the issue; continue with others. |
| EC-3 | Board is passed out                               | Return par score = 0, no par contract, without calling DDS.               |
| EC-4 | Multiple par contracts at the same score          | Return all par contracts in the ParResult (e.g., 4♠ by N = 4♠ by S both par). |
| EC-5 | DDS returns an error code for a specific board    | Mark that board's ParResult as failed; do not propagate to other boards.  |
| EC-6 | Session contains more than 28 boards (e.g., 36)  | Process all boards; performance target is still < 30 seconds.            |

---

## 8. Success Criteria

| ID     | Criterion                                                                             |
| ------ | ------------------------------------------------------------------------------------- |
| SC-001 | All acceptance scenarios pass.                                                         |
| SC-002 | Par scores match expected values for a set of reference hands with known DD results.  |
| SC-003 | A 28-board session is analyzed in under 30 seconds on a standard laptop.             |
| SC-004 | A DDS library load failure produces a clear startup error, not an unhandled exception.|

---

## 9. Dependencies and Constraints

### 9.1 Dependencies

- **FEAT-001** (PBN File Upload): provides the Board domain objects that are the input to this engine.
- **Bo Haglund's DDS library**: the mandated external C/C++ library. Source: https://github.com/dds-bridge/dds. Must be compiled and available as a shared library (.dll on Windows, .so on Linux/Mac).

### 9.2 Constraints

- **No custom solver**: The DDS library must be used; building a custom DD solver is explicitly out of scope.
- The DDS library is C/C++ and must be called from the application server, not from the browser/client.
- The DDS license (open source) must be reviewed and complied with for distribution.

### 9.3 Architecture References

| Arc42 Section                    | Relevance to This Feature                                      |
| -------------------------------- | -------------------------------------------------------------- |
| 4. Solution Strategy             | Technology decision: wrap DDS, no custom solver                |
| 5. Building Block View           | DD Analysis Engine component and its interface                 |
| 6. Runtime View                  | Server-side analysis invocation flow                           |
| 8. Crosscutting Concepts         | Error handling pattern for external library failures           |

---

## 10. Open Questions

| #   | Question                                                                                 | Owner | Status | Resolution |
| --- | ---------------------------------------------------------------------------------------- | ----- | ------ | ---------- |
| 1   | Which specific DDS API function(s) to use for batch par calculation (CalcAllTablesPBN + Par, or CalcParPBN directly)? | Tech  | Open   |            |
| 2   | Should the DDS library be compiled and shipped with the application, or require the user to install it separately? | Tech  | Open   |            |

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
