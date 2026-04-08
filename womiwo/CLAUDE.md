# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

BridgeGameCalculator is a competitive analysis tool for serious duplicate Bridge players. The core goal is cutting post-tournament double-dummy (DD) analysis time from 30+ minutes to under 5 minutes. The secondary goal is single-hand DD analysis in under 30 seconds.

The project is in the **pre-implementation planning phase**. There is no application code yet.

## Key Decisions Already Made

These are locked — do not re-open them without strong justification:

- **DD solver:** Wrap [Bo Haglund's open-source DDS](https://github.com/dds-bridge/dds) (C/C++). Do not build a custom solver.
- **Platform:** Desktop/laptop web application. No mobile required for MVP.
- **Authentication:** None. The tool is stateless — upload, analyze, view.
- **Data import:** PBN file upload only for MVP. BBO/LIN deferred to 1.0.
- **Analysis scope:** Par score + actual-vs-par delta per board. Full 20-combination DD table is deferred to 1.0.
- **Collaboration:** None. Solo use only.

The full MVP feature list and cut list are in `docs/product/impact-map.md`.

## Documentation Structure

```
docs/
  product/impact-map.md        # Goals, personas, MVP scope, cut list
  architecture/                # arc42 template (populate as decisions are made)
    00-table-of-contents.md
    09-architecture-decisions.md  # Links to ADRs
    adr/                       # Architecture Decision Records (ADRs)
  specs/                       # Feature specs (FEAT-NNN-<slug>.md)
  plans/                       # Implementation plans (FEAT-NNN-<slug>.md)
```

## Workflow: Planning Before Building

Use these skills and agents before writing implementation code:

| Task | How |
|------|-----|
| Write a feature spec | `/spec <feature name>` — interactive, produces `docs/specs/FEAT-NNN-<slug>.md` |
| Create an implementation plan from a spec | Use the `implementation-planner` agent |
| Record an architecture decision | `/record-adr` — stores ADRs in `docs/architecture/adr/001-*.md` and links them in `09-architecture-decisions.md` |
| Explore or redefine product scope | `/impact-mapping <topic>` |
| Review architectural decisions or proposals | Use the `architecture-reviewer` agent |
| Write or update documentation | Use the `docs-writer` agent |
| Build or review C# console application code | Use the `csharp-console-dev` agent |
| Test implementation code | Use the `code-tester` agent — invoke proactively after writing code |

Feature IDs follow `FEAT-NNN` (three-digit, zero-padded). ADRs follow `NNN-title.md` in the same scheme.

## Available Agents

| Agent | When to use |
|-------|-------------|
| `implementation-planner` | Analyze a feature spec and produce a step-by-step implementation plan in `docs/plans/` |
| `architecture-reviewer` | Review ADRs, feature specs, or architectural proposals for quality and consistency |
| `docs-writer` | Create or update feature specs, ADRs, implementation plans, and other project documentation |
| `csharp-console-dev` | Expert guidance on C# console app architecture, patterns, DI, CLI parsing, and testing |
| `code-tester` | Run tests and verify correctness after implementing a feature or fixing a bug |
