# Feature Specification: PBN File Upload

<!--
  TEMPLATE INSTRUCTIONS
  =====================
  This spec covers the primary data import path: uploading a PBN file containing a
  tournament session. It is technology-agnostic — implementation details belong in
  the technical plan, not here.
-->

## 1. Overview

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| Feature ID      | FEAT-001                                       |
| Status          | Draft                                          |
| Author          | Team                                           |
| Created         | 2026-04-08                                     |
| Last updated    | 2026-04-08                                     |
| Epic / Parent   | Data Import                                    |
| Arc42 reference | 3. System Scope & Context, 5. Building Blocks  |

### 1.1 Problem Statement

Serious duplicate Bridge players want to review their tournament sessions using double-dummy analysis, but manually entering hand data is tedious and error-prone. PBN (Portable Bridge Notation) files are the standard export format provided by most duplicate scoring software, and uploading them is the only practical way to get full session data into the tool quickly.

### 1.2 Goal

Allow the user to upload a PBN file containing a tournament session (typically 24–28 boards) so that all boards are parsed, validated, and made available for DD analysis — completing the import in under 2 seconds.

### 1.3 Non-Goals

- BBO/LIN file import (deferred to 1.0)
- Manual hand entry (separate feature FEAT-006)
- Persisting or saving uploaded data between sessions (the tool is stateless)
- Validating bridge logic beyond correct file format (e.g., whether the contract was legal)
- Batch upload of multiple PBN files

---

## 2. User Stories

### US-001: Upload a session PBN file

**As a** Post-Tournament Reviewer,
**I want** to upload a PBN file from my computer,
**so that** I can get all boards from last night's session into the tool without manual entry.

### US-002: See feedback on upload errors

**As a** Post-Tournament Reviewer,
**I want** to receive a clear error message when my PBN file is malformed or unreadable,
**so that** I understand what went wrong and can try a corrected file.

---

## 3. Functional Requirements

| ID     | Requirement                                                                                                          | Priority | User Story |
| ------ | -------------------------------------------------------------------------------------------------------------------- | -------- | ---------- |
| FR-001 | The system shall present a file upload control that accepts `.pbn` files.                                            | Must     | US-001     |
| FR-002 | The system shall parse the uploaded PBN file and extract all boards.                                                 | Must     | US-001     |
| FR-003 | The system shall extract the following per board: board number, dealer, vulnerability, all four hands, contract played (or Pass), declarer, and result (tricks taken). | Must     | US-001     |
| FR-004 | The system shall complete parsing of a 28-board PBN file within 2 seconds of upload.                                | Must     | US-001     |
| FR-005 | The system shall display a clear, human-readable error message when the PBN file cannot be parsed.                   | Must     | US-002     |
| FR-006 | The system shall accept PBN files where one or more boards have `Contract "Pass"` (passed-out boards).               | Must     | US-001     |
| FR-007 | The system shall ignore unrecognized PBN tags gracefully without failing.                                            | Should   | US-001     |
| FR-008 | The system shall display a progress indicator while parsing.                                                         | Should   | US-001     |

---

## 4. Acceptance Scenarios

### SC-001: Successful upload of a valid PBN file (FR-001, FR-002, FR-003)

```gherkin
Given the user is on the home/upload page
When the user selects a valid PBN file containing 24 boards
Then the system parses all 24 boards successfully
  And the system transitions to the analysis phase with all 24 boards available
```

### SC-002: Upload completes within 2 seconds (FR-004)

```gherkin
Given a valid PBN file containing 28 boards
When the user uploads the file
Then parsing completes and the system is ready for DD analysis within 2 seconds
```

### SC-003: Passed-out board handled correctly (FR-006)

```gherkin
Given a valid PBN file where board 7 has Contract "Pass"
When the user uploads the file
Then board 7 is imported with contract = Pass and result = 0 tricks
  And all other boards are imported normally
```

### SC-004: Malformed PBN file shows error (FR-005)

```gherkin
Given a file that is not valid PBN (e.g., a plain text file or corrupted PBN)
When the user uploads the file
Then the system displays a clear error message describing the problem
  And the system remains on the upload page, ready for a new upload attempt
```

### SC-005: PBN file with unrecognized tags (FR-007)

```gherkin
Given a PBN file containing non-standard tags (e.g., [Score "NS 600"])
When the user uploads the file
Then the system parses the file successfully, ignoring the unrecognized tags
  And all boards with standard tags are imported correctly
```

---

## 5. Domain Model

### 5.1 Entities

#### Session

A collection of boards from a single tournament session, derived from one PBN file upload.

| Attribute   | Type        | Constraints              | Description                              |
| ----------- | ----------- | ------------------------ | ---------------------------------------- |
| boards      | Board[]     | required, min 1          | Ordered list of boards in the session    |
| sourceFile  | string      | required                 | Original filename of the uploaded PBN    |

#### Board

A single bridge deal as extracted from the PBN file.

| Attribute     | Type         | Constraints                      | Description                                      |
| ------------- | ------------ | -------------------------------- | ------------------------------------------------ |
| boardNumber   | integer      | required, 1–999                  | Board number as given in the PBN                 |
| dealer        | Seat         | required                         | Which seat dealt (N/E/S/W)                       |
| vulnerability | Vulnerability| required                         | Which side is vulnerable                          |
| hands         | Hands        | required                         | All four hands (N/E/S/W) with card distributions |
| contract      | Contract?    | nullable                         | Contract played, null if passed out               |
| declarer      | Seat?        | nullable                         | Declarer seat, null if passed out                |
| result        | integer?     | nullable, 0–13                   | Tricks taken by declarer, null if passed out     |

### 5.2 Relationships

- A **Session** contains one or more **Boards** (one-to-many)
- A **Board** belongs to exactly one **Session**

### 5.3 Value Objects

#### Hands

| Attribute | Type    | Constraints         | Description                          |
| --------- | ------- | ------------------- | ------------------------------------ |
| north     | string  | exactly 13 cards    | PBN deal string for North's hand     |
| east      | string  | exactly 13 cards    | PBN deal string for East's hand      |
| south     | string  | exactly 13 cards    | PBN deal string for South's hand     |
| west      | string  | exactly 13 cards    | PBN deal string for West's hand      |

#### Contract

| Attribute  | Type   | Constraints                      | Description                          |
| ---------- | ------ | -------------------------------- | ------------------------------------ |
| level      | integer| 1–7                              | Contract level                       |
| strain     | Strain | S/H/D/C/NT                      | Trump suit or notrump                |
| doubled    | enum   | Undoubled/Doubled/Redoubled      | Doubling state                       |

#### Seat

Enum: `N`, `E`, `S`, `W`

#### Vulnerability

Enum: `None`, `NS`, `EW`, `Both`

#### Strain

Enum: `Spades`, `Hearts`, `Diamonds`, `Clubs`, `NoTrump`

### 5.4 Domain Rules and Invariants

- **Complete deck**: Across all four hands of a Board, all 52 cards must be present exactly once.
- **Passed-out board**: If `Contract` is `Pass`, then `declarer` and `result` must both be null/absent.
- **Result range**: If a contract is present, `result` must be between 0 and 13 inclusive.

---

## 6. Non-Functional Requirements

| ID      | Category    | Requirement                                                           |
| ------- | ----------- | --------------------------------------------------------------------- |
| NFR-001 | Performance | Parsing a 28-board PBN file must complete in under 2 seconds.         |
| NFR-002 | Reliability | The parser must not crash on malformed input; it must return a structured error instead. |
| NFR-003 | Usability   | Error messages must identify the board number and tag where parsing failed (where possible). |

---

## 7. Edge Cases and Error Scenarios

| ID   | Scenario                                              | Expected Behavior                                                               |
| ---- | ----------------------------------------------------- | ------------------------------------------------------------------------------- |
| EC-1 | File is not a PBN file (wrong extension or content)   | Show error: "This file does not appear to be a valid PBN file."                 |
| EC-2 | PBN file has 0 boards                                  | Show error: "The PBN file contains no boards."                                  |
| EC-3 | A board is missing the Deal tag                        | Show error identifying the board number and the missing required tag.            |
| EC-4 | A hand has fewer or more than 13 cards                 | Show error: "Board N: hand [seat] does not have 13 cards."                      |
| EC-5 | Duplicate card across two hands                        | Show error: "Board N: card [card] appears more than once."                      |
| EC-6 | File too large (> 1 MB)                               | Show error: "File exceeds the 1 MB size limit." (PBN session files are small)  |
| EC-7 | Board with `Contract "Pass"` has a non-null Result     | Treat as passed out; ignore the Result tag with a warning.                      |
| EC-8 | Board missing Contract or Result tag                   | Import the board with contract/result as null; analysis can still run for par.  |

---

## 8. Success Criteria

| ID     | Criterion                                                                           |
| ------ | ----------------------------------------------------------------------------------- |
| SC-001 | All acceptance scenarios pass.                                                       |
| SC-002 | A valid 28-board PBN file is parsed and available for analysis within 2 seconds.    |
| SC-003 | Invalid or malformed PBN files produce a human-readable error; the app does not crash. |
| SC-004 | Passed-out boards are imported without error and represented correctly in the domain model. |

---

## 9. Dependencies and Constraints

### 9.1 Dependencies

- No upstream feature dependencies — this is the entry point of the application.
- FEAT-002 (DD Analysis Engine) depends on the Board domain model defined here.
- FEAT-003, FEAT-004, FEAT-005 depend on the Session and Board objects produced here.

### 9.2 Constraints

- The tool is stateless: the parsed session exists only in memory for the duration of the current analysis session. No database persistence.
- PBN is the only supported import format for MVP.

### 9.3 Architecture References

| Arc42 Section                    | Relevance to This Feature                                  |
| -------------------------------- | ---------------------------------------------------------- |
| 3. System Scope & Context        | File upload is the system boundary / external interface    |
| 5. Building Block View           | Data Import component                                      |
| 8. Crosscutting Concepts         | Error handling patterns for malformed input                |

---

## 10. Open Questions

| #   | Question                                                              | Owner | Status   | Resolution |
| --- | --------------------------------------------------------------------- | ----- | -------- | ---------- |
| 1   | Should the system accept `.txt` files that contain valid PBN content, or enforce `.pbn` extension only? | Team  | Open     |            |

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
  - [x] No more than 3 [NEEDS CLARIFICATION] markers remain
  - [x] Open questions are assigned and have a resolution path
-->
