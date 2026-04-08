---
name: BridgeGameCalculator Impact Map
description: Impact map for BridgeGameCalculator - a competitive Bridge analysis tool focused on post-tournament DD analysis. Covers goals, persona, MVP scope, and cut decisions.
type: project
---

BridgeGameCalculator impact map completed on 2026-04-08.

**Goals:** Two goals defined. G1: Cut post-tournament DD analysis from 30+ min to under 5 min. G2: Quick single-hand DD analysis in under 30 seconds.

**Persona:** Single persona — Post-Tournament Reviewer (serious club/tournament duplicate Bridge player, solo analysis, laptop at home). Club directors, coaches, and beginners explicitly out of scope.

**MVP (6 features):** PBN file upload, DDS wrapper (Bo Haglund's), actual-vs-par delta, session dashboard with color-coded deltas, board detail view with hand diagram, single-hand manual entry.

**Key cuts and rationale:**
- Manual hand entry was initially cut (contradicts G1 speed goal for full sessions) but user overrode the cut for single-hand use case, which led to adding G2.
- BBO/LIN import deferred to 1.0 (PBN covers primary use case).
- Full 20-combo DD table deferred to 1.0 (par + delta answers the core question).
- User accounts deferred to beyond 1.0 (retention feature, not productivity).
- Bidding analysis deferred to beyond 1.0 (essentially a different product).

**Key technical decisions:** Wrap Bo Haglund's DDS (don't build custom solver). Desktop web app. Stateless (no accounts for MVP). PBN-only import for MVP.

**Why:** The driving pain point is that manual DD calculation after tournaments is tedious and discourages regular review.

**How to apply:** All feature decisions for this project should trace back to G1 or G2. Features that don't serve either goal are scope creep.
