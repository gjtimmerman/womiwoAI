# Impact Map: BridgeGameCalculator

A competitive analysis tool for serious duplicate Bridge players, focused on fast post-tournament double-dummy analysis.

---

## 1. Goals

| ID | Goal | Why | Success Criteria |
|----|------|-----|-----------------|
| G1 | Cut post-tournament session analysis time from 30+ min to under 5 min | Manual double-dummy analysis after tournament sessions is tedious, error-prone, and discourages regular review. Faster analysis means more time spent on actual improvement. | Full session (24-28 boards) imported, analyzed, and results displayed in under 5 minutes, measured from data import to actionable results. |
| G2 | Enable quick double-dummy analysis of a single interesting hand | Players sometimes want to explore one specific hand without importing a full session file. | Single hand entered and analyzed in under 30 seconds. |

---

## 2. Personas

| Persona | Description | Needs | Problems | Related Goals |
|---------|-------------|-------|----------|---------------|
| Post-Tournament Reviewer | Serious duplicate Bridge player (club and tournament level). Reviews sessions solo on a laptop at home after games. Plays 2-3 times per week. Not a beginner, but not a professional with custom tooling. | Quickly understand where they gained or lost against double-dummy optimal play across an entire session. Occasionally deep-dive into a single interesting hand. | Currently calculates double-dummy results manually after tournaments, a process that takes 30+ minutes per session. The tedium discourages regular review, which slows improvement. | G1, G2 |

---

## 3. Epics and Features

| Epic | Feature | Persona | Need/Problem Addressed | Related Goal |
|------|---------|---------|----------------------|--------------|
| Data Import | PBN file upload | Post-Tournament Reviewer | Get session hands into the tool quickly without manual entry | G1 |
| Data Import | Single-hand manual entry form | Post-Tournament Reviewer | Analyze one specific hand of interest without needing a full session file | G2 |
| DD Analysis Engine | Wrap Bo Haglund's DDS for par score calculation | Post-Tournament Reviewer | Calculate theoretically optimal contract and result per board | G1, G2 |
| DD Analysis Engine | Actual-vs-par delta calculation | Post-Tournament Reviewer | Show where user gained or lost IMPs/matchpoints vs optimal play | G1 |
| Results Display | Session summary dashboard with color-coded deltas | Post-Tournament Reviewer | Scan all boards at a glance, spot biggest swings instantly | G1 |
| Results Display | Board detail view with hand diagram | Post-Tournament Reviewer | Drill into a specific board to see the full hand, contract played, par contract, and delta | G1, G2 |

### Features Explicitly Cut (with rationale)

| Feature | Why It Was Cut |
|---------|---------------|
| BBO/LIN file import | PBN covers the primary use case for MVP. BBO/LIN broadens reach but is not required to validate the core goal. Deferred to 1.0. |
| Full 20-combination DD table | Par score + delta answers the core question ("where did I gain/lose?"). The full table is a power-user drill-down. Deferred to 1.0. |
| Session statistics (aggregates) | Vanity metrics that don't directly help users understand specific gains/losses per board. Deferred to 1.0. |
| Field result comparison (traveller data) | Useful context but requires additional data sources beyond PBN. Deferred to 1.0. |
| Advanced filtering and sorting | Power-user feature. Wait for feedback to learn what filters actually matter. Deferred to 1.0. |
| Export/print results | Convenience feature, not core value. Deferred to beyond 1.0. |
| User accounts and session history | Retention feature. Only matters once there are users to retain. Deferred to beyond 1.0. |
| Bidding analysis / suggested bidding | Enormously complex, requires opinionated bidding system modeling. Essentially a different product. Deferred to beyond 1.0. |
| Partner collaboration / sharing | User confirmed analysis is a solo activity. Not needed. |

---

## 4. Roadmap

### MVP

The absolute minimum to validate the core goals. Ship this first.

| Feature | Goal Served | Why It Cannot Be Cut |
|---------|------------|---------------------|
| PBN file upload | G1 | Without this, there is no way to get session data into the tool. G1 is dead without it. |
| Wrap Bo Haglund's DDS for par score calculation | G1, G2 | This is the analysis engine. Without it, there is nothing to calculate. |
| Actual-vs-par delta calculation | G1 | Without the delta, users still have to do mental math comparing their score to par. That is the problem being solved. |
| Session summary dashboard with color-coded deltas | G1 | Without this, users must scroll through boards one by one. The dashboard is what makes "under 5 minutes" achievable. |
| Board detail view with hand diagram | G1, G2 | Without drill-down, users see they lost IMPs but cannot see the hand or contract. The dashboard without detail is incomplete. |
| Single-hand manual entry form | G2 | Serves the secondary goal of quick single-hand exploration. Low build cost given the engine and display already exist. |

### 1.0

Features that complete the initial vision after MVP is validated.

| Feature | Rationale |
|---------|-----------|
| BBO/LIN file import | Removes friction for users whose clubs don't provide PBN files. Broadens the data import funnel. |
| Full 20-combination DD table (drill-down from board detail) | Power users want to see all strain/declarer combinations, not just par. Natural extension of the board detail view. |
| Session statistics (aggregates) | Total IMPs vs par, percentage beating par, average delta. Useful for tracking improvement over time. |
| Field result comparison (traveller data) | Shows how the rest of the field performed. Adds context beyond "vs par." Requires PBN files with traveller data or a separate data source. |
| Basic filtering and sorting | Sort by biggest loss, filter by vulnerability or strain. Makes large sessions easier to navigate. |

### Beyond 1.0

Ideas parked for future exploration. Build only with strong demand signals.

| Feature | Rationale for Deferral |
|---------|----------------------|
| Export/print results | Convenience, not core value. Low demand signal needed before investing. |
| User accounts and session history | Retention feature. Only matters once there are active users to retain. |
| Bidding analysis / suggested bidding | Enormously complex. Requires opinionated bidding system modeling. Essentially a different product. |
| Advanced filtering (multi-criteria, saved filters) | Power-user feature. Wait for user feedback to determine what filters actually matter. |

---

## Key Decisions and Assumptions

- **Platform:** Desktop/laptop web application. No mobile optimization required for MVP.
- **DD Solver:** Wrap Bo Haglund's open-source DDS library (C/C++). Do not build a custom solver.
- **User model:** No accounts, no authentication for MVP. The tool is stateless -- upload, analyze, view.
- **Collaboration:** Solo use only. No sharing, no partner review features.
- **Data format:** PBN is the sole import format for MVP.
- **Single persona:** The Post-Tournament Reviewer. Club directors, coaches, and beginners are out of scope.
