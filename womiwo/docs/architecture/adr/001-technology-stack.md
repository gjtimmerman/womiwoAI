# ADR-001: Technology Stack

| Field    | Value               |
|----------|---------------------|
| ID       | ADR-001             |
| Status   | Accepted            |
| Date     | 2026-04-08          |
| Deciders | Team                |

## Context

BridgeGameCalculator is a desktop/laptop web application that wraps Bo Haglund's DDS C/C++ library. It must:
- Run locally (no server deployment required)
- Call a native C/C++ shared library server-side
- Provide a browser-based UI for file upload, results display, and navigation
- Require no installation beyond a single executable (ideally)

The CLAUDE.md references a `csharp-console-dev` agent, indicating C# is the language of choice.

## Decision

**Backend:** ASP.NET Core 8 (Minimal API), self-hosted on localhost. Serves as the HTTP layer and the host for native DDS library calls.

**Frontend:** Blazor WebAssembly (WASM), single-page application served from the ASP.NET Core host. All UI code in C# — no separate JavaScript framework required.

**Project structure:** Single solution with two projects:
- `BridgeGameCalculator.Server` — ASP.NET Core host + API endpoints + DDS wrapper
- `BridgeGameCalculator.Client` — Blazor WASM frontend (references shared domain types)
- `BridgeGameCalculator.Shared` — Shared domain models (Board, ParResult, BoardDelta, etc.)

**DDS integration:** P/Invoke to call the compiled DDS shared library (`.dll` on Windows, `.so` on Linux/macOS). The DDS library is compiled separately and placed in the application's output directory.

**Testing:** xUnit for unit and integration tests. bUnit for Blazor component tests.

## Rationale

- **ASP.NET Core + Blazor WASM** keeps the entire codebase in C#, matching the team's established tooling (`csharp-console-dev` agent).
- **Blazor WASM** runs in the browser without a server round-trip per interaction; navigation between boards is instant once data is loaded.
- **Self-hosted on localhost** means no cloud infrastructure is needed — the user runs a single executable and opens a browser.
- **P/Invoke** is the standard .NET mechanism for calling native C/C++ libraries. The DDS library already ships as a shared library, making P/Invoke straightforward.
- **Shared project** lets domain models (Board, Hands, ParResult, etc.) be used in both server and client without duplication.

## Consequences

- The DDS library must be compiled for the target OS and architecture. The team must manage binaries for Windows x64 (primary target) and optionally Linux/macOS.
- Blazor WASM has a first-load download cost (~5–10 MB). Acceptable for a desktop/laptop tool — users are not on mobile.
- P/Invoke interop requires careful marshaling between C# and C structs. This is localized to the DDS wrapper service.
- The application is launched via `dotnet run` or a published self-contained executable.
