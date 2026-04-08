# Implementation Plan: PBN File Upload

**Spec:** `docs/specs/FEAT-001-pbn-file-upload.md`
**ADR:** `docs/architecture/adr/001-technology-stack.md`
**Created:** 2026-04-08
**Status:** Draft

## Summary

This plan implements the PBN file upload feature -- the entry point of BridgeGameCalculator. It creates the entire solution scaffolding (ASP.NET Core 8 Minimal API + Blazor WebAssembly + Shared project), defines the shared domain model for bridge sessions and boards, builds a pure-C# PBN parser with robust error handling, exposes a single POST endpoint for file upload, wires up a Blazor upload page, and covers the parser thoroughly with xUnit tests. Since there is no existing code, every file is new.

## Key Design Decisions

1. **Solution scaffolding via `dotnet new` templates.** Use `blazorwasm --hosted` to generate the three-project structure (Server, Client, Shared) in one step. This gives us the correct project references, launch settings, and WASM hosting middleware out of the box. Rename the generated projects to match our naming convention (`BridgeGameCalculator.*`).

2. **Domain model lives entirely in `BridgeGameCalculator.Shared`.** `Session`, `Board`, `Hands`, `Contract`, and all enums (`Seat`, `Vulnerability`, `Strain`, `DoubleState`) go in the Shared project so both server-side parser and client-side display can use the same types without duplication. These are plain C# records/classes with no framework dependencies.

3. **`PbnParser` is a stateless service in the Server project.** The parser takes a `Stream` and `string fileName`, returns a `Result<Session, PbnParseError>` using a discriminated result pattern. It lives server-side because the client never parses PBN directly -- it uploads the file and receives JSON. The parser is a pure function with no I/O dependencies beyond the input stream, making it trivially testable.

4. **Use a `Result<T, E>` type instead of exceptions for parse errors.** Define a lightweight `Result<TValue, TError>` in the Shared project. Parse errors are structured data (`PbnParseError` with board number, line number, message), not exceptions. This makes the error path explicit and testable, and ensures NFR-002 (parser must not crash on malformed input).

5. **Single POST endpoint at `/api/sessions`.** Accepts `multipart/form-data` with one `IFormFile`. Returns `200 OK` with the serialized `Session` on success, or `422 Unprocessable Entity` with a `PbnParseError` body on failure. File size is capped at 1 MB via `IFormFile` length check (not Kestrel global limits, to keep it explicit). The endpoint is a single Minimal API `MapPost` call -- no controllers.

6. **Blazor upload page uses `InputFile` component.** Blazor's built-in `InputFile` component handles the browser file picker and streams the file to the server. The page shows a loading spinner during upload/parse and renders errors inline. No separate Blazor state store is needed for MVP -- the page holds its own state. The parsed `Session` is stored in a scoped `SessionState` service so downstream pages (FEAT-003+) can access it.

7. **xUnit test project with no mocking framework.** The parser is a pure function; tests pass in string content via `MemoryStream`. No mocks needed. Test data is embedded as string constants in the test class for readability. bUnit is added as a dependency for future Blazor component tests but is not exercised in this feature.

## Implementation Steps

### Phase 1: Solution Scaffolding

**Goal:** A buildable, runnable solution with the three-project structure and test project.

#### Step 1.1: Create the solution and projects

Run from the repository root (`/home/instructor/womiwo`):

```bash
# Create solution
dotnet new sln -n BridgeGameCalculator -o src

# Create the three projects
dotnet new classlib -n BridgeGameCalculator.Shared -o src/BridgeGameCalculator.Shared -f net8.0
dotnet new web -n BridgeGameCalculator.Server -o src/BridgeGameCalculator.Server -f net8.0
dotnet new blazorwasm -n BridgeGameCalculator.Client -o src/BridgeGameCalculator.Client -f net8.0 --empty

# Create test project
dotnet new xunit -n BridgeGameCalculator.Tests -o tests/BridgeGameCalculator.Tests -f net8.0

# Add projects to solution
dotnet sln src/BridgeGameCalculator.sln add \
  src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj \
  src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj \
  src/BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj \
  tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj
```

#### Step 1.2: Add project references

```bash
# Server references Shared
dotnet add src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj \
  reference src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj

# Client references Shared
dotnet add src/BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj \
  reference src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj

# Server references Client (to serve WASM files)
dotnet add src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj \
  reference src/BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj

# Tests reference Shared and Server
dotnet add tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj \
  reference src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj
dotnet add tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj \
  reference src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj
```

#### Step 1.3: Add NuGet packages

```bash
# Server needs WebAssembly hosting
dotnet add src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj \
  package Microsoft.AspNetCore.Components.WebAssembly.Server

# Tests need bUnit for future Blazor tests
dotnet add tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj \
  package bunit
```

#### Step 1.4: Configure the Server `Program.cs`

Replace the generated `src/BridgeGameCalculator.Server/Program.cs` with the Blazor WASM hosting boilerplate:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode();
app.MapFallbackToFile("index.html");

app.Run();
```

This will be refined in Phase 2 when the API endpoint is added. The exact hosting configuration depends on whether we use Blazor WASM standalone or ASP.NET Core hosted mode. Use the **hosted model** where the Server project serves the Client WASM app and also hosts API endpoints. Configure `Program.cs` accordingly:

```csharp
using BridgeGameCalculator.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PbnParser>();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

// API endpoints registered here in Phase 2

app.MapFallbackToFile("index.html");
app.Run();
```

#### Step 1.5: Verify the solution builds

```bash
dotnet build src/BridgeGameCalculator.sln
```

---

### Phase 2: Shared Domain Model

**Goal:** All domain types defined in the Shared project, ready for both parser and UI.

#### Step 2.1: Create enums

**File:** `src/BridgeGameCalculator.Shared/Models/Seat.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public enum Seat
{
    North,
    East,
    South,
    West
}
```

**File:** `src/BridgeGameCalculator.Shared/Models/Vulnerability.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public enum Vulnerability
{
    None,
    NorthSouth,
    EastWest,
    Both
}
```

**File:** `src/BridgeGameCalculator.Shared/Models/Strain.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public enum Strain
{
    Clubs,
    Diamonds,
    Hearts,
    Spades,
    NoTrump
}
```

**File:** `src/BridgeGameCalculator.Shared/Models/DoubleState.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

public enum DoubleState
{
    Undoubled,
    Doubled,
    Redoubled
}
```

#### Step 2.2: Create value objects

**File:** `src/BridgeGameCalculator.Shared/Models/Hands.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// The four hands of a bridge deal. Each hand is stored as a PBN suit string
/// (e.g., "AK32.QJ.T987.654") with suits separated by dots in S.H.D.C order.
/// </summary>
public sealed record Hands(string North, string East, string South, string West);
```

**File:** `src/BridgeGameCalculator.Shared/Models/Contract.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// A bridge contract. Level is 1-7, Strain is the trump suit or NT,
/// DoubleState indicates whether the contract was doubled/redoubled.
/// </summary>
public sealed record Contract(int Level, Strain Strain, DoubleState DoubleState);
```

#### Step 2.3: Create entities

**File:** `src/BridgeGameCalculator.Shared/Models/Board.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// A single bridge deal as extracted from a PBN file.
/// Contract, Declarer, and Result are null for passed-out boards.
/// </summary>
public sealed class Board
{
    public required int BoardNumber { get; init; }
    public required Seat Dealer { get; init; }
    public required Vulnerability Vulnerability { get; init; }
    public required Hands Hands { get; init; }
    public Contract? Contract { get; init; }
    public Seat? Declarer { get; init; }
    public int? Result { get; init; }
}
```

**File:** `src/BridgeGameCalculator.Shared/Models/Session.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// A collection of boards from a single PBN file upload.
/// </summary>
public sealed class Session
{
    public required string SourceFile { get; init; }
    public required IReadOnlyList<Board> Boards { get; init; }
}
```

#### Step 2.4: Create the Result type and error model

**File:** `src/BridgeGameCalculator.Shared/Result.cs`

```csharp
namespace BridgeGameCalculator.Shared;

/// <summary>
/// A discriminated union representing either a success value or an error.
/// </summary>
public sealed class Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsSuccess { get; }
    public bool IsError => !IsSuccess;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on an error result.");

    public TError Error => IsError
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a success result.");

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(TError error, bool _)
    {
        _error = error;
        IsSuccess = false;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error, false);
}
```

**File:** `src/BridgeGameCalculator.Shared/Models/PbnParseError.cs`

```csharp
namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Structured error returned when PBN parsing fails.
/// </summary>
public sealed record PbnParseError(
    string Message,
    int? BoardNumber = null,
    int? LineNumber = null
);
```

#### Step 2.5: Delete auto-generated placeholder files

Remove `Class1.cs` from the Shared project (generated by `dotnet new classlib`).

---

### Phase 3: PBN Parser Service

**Goal:** A complete, tested PBN parser that handles all spec requirements and edge cases.

#### Step 3.1: Create `PbnParser`

**File:** `src/BridgeGameCalculator.Server/Services/PbnParser.cs`

**Namespace:** `BridgeGameCalculator.Server.Services`

**Class:** `public sealed class PbnParser`

**Public method signature:**

```csharp
public Result<Session, PbnParseError> Parse(Stream content, string fileName)
```

**Internal parsing strategy:**

1. Read the stream line-by-line using `StreamReader`.
2. PBN tags have the format `[TagName "Value"]`. Use a regex: `^\[(\w+)\s+"(.*)"\]\s*$`.
3. Track "current board" state. A new `[Board "N"]` tag starts a new board context.
4. For each board, collect tags into a `Dictionary<string, string>`.
5. When the next `[Board` tag is encountered (or end-of-file), finalize the current board:
   - Extract required tags: `Board`, `Dealer`, `Vulnerable`, `Deal`.
   - Extract optional tags: `Contract`, `Declarer`, `Result`.
   - Parse and validate each tag value (details below).
   - If a required tag is missing, return `PbnParseError` with board number and message.
6. After all boards are parsed, if the board list is empty, return error EC-2.
7. Return `Result.Success(new Session { ... })`.

**Tag parsing details:**

- **`Dealer`**: Map `"N"` / `"E"` / `"S"` / `"W"` to `Seat` enum. Case-insensitive.
- **`Vulnerable`**: Map `"None"` to `Vulnerability.None`, `"NS"` to `NorthSouth`, `"EW"` to `EastWest`, `"All"` / `"Both"` to `Both`. Case-insensitive.
- **`Deal`**: Format is `"<first_seat>:<hand> <hand> <hand> <hand>"` where `<first_seat>` is `N/E/S/W` indicating which hand is listed first, and each hand is `S.H.D.C` with dots separating suits. Cards within a suit are concatenated (e.g., `AKQ2`). Void suits are empty between dots. Parse into `Hands` record. Validate: each hand has exactly 13 cards; all 52 cards appear exactly once.
- **`Contract`**: `"Pass"` means passed out (return null). Otherwise parse level (1-7), strain (`S`/`H`/`D`/`C`/`NT`), and optional `X` or `XX` suffix for doubles.
- **`Declarer`**: Same mapping as `Dealer`. Null if passed out.
- **`Result`**: Integer string, parse to `int`. Null if passed out. Validate 0-13 range. Per EC-7: if contract is `"Pass"` but result is present, ignore result with a warning (still return success).

**Card validation helper:**

Create a private method `ValidateDeck(Hands hands, int boardNumber)` that:
- Enumerates all cards across all four hands.
- Checks each hand has exactly 13 cards (EC-4).
- Checks for duplicates across hands (EC-5).

**Error handling:**

- Unrecognized tags are silently ignored (FR-007).
- Missing optional tags (`Contract`, `Declarer`, `Result`) produce a board with null values (EC-8).
- File-level errors (not PBN content at all) detected by: no tags parsed after full read.

#### Step 3.2: Create card utility constants

**File:** `src/BridgeGameCalculator.Server/Services/CardConstants.cs`

**Namespace:** `BridgeGameCalculator.Server.Services`

```csharp
public static class CardConstants
{
    public static readonly IReadOnlySet<char> ValidRanks =
        new HashSet<char> { 'A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2' };

    public static readonly string[] SuitNames = { "Spades", "Hearts", "Diamonds", "Clubs" };
}
```

---

### Phase 4: API Endpoint

**Goal:** A working POST endpoint that accepts a PBN file and returns parsed JSON.

#### Step 4.1: Register services and define the endpoint

**File:** `src/BridgeGameCalculator.Server/Program.cs`

Add the endpoint after the existing middleware setup:

```csharp
app.MapPost("/api/sessions", async (IFormFile file, PbnParser parser) =>
{
    if (file.Length == 0)
        return Results.UnprocessableEntity(new PbnParseError("The uploaded file is empty."));

    if (file.Length > 1_048_576) // 1 MB
        return Results.UnprocessableEntity(new PbnParseError("File exceeds the 1 MB size limit."));

    using var stream = file.OpenReadStream();
    var result = parser.Parse(stream, file.FileName);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.UnprocessableEntity(result.Error);
})
.DisableAntiforgery()
.Accepts<IFormFile>("multipart/form-data")
.Produces<Session>(200)
.Produces<PbnParseError>(422);
```

Key details:
- `DisableAntiforgery()` is needed because the Blazor WASM client sends a plain HTTP request, not a form post with anti-forgery tokens.
- The endpoint is synchronous internally (stream parsing is CPU-bound, not I/O-bound), but the `async` wrapper lets ASP.NET Core handle the `IFormFile` read asynchronously.

#### Step 4.2: Configure JSON serialization

In `Program.cs`, configure `System.Text.Json` to use camelCase and serialize enums as strings:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
```

---

### Phase 5: Blazor Upload Page

**Goal:** A functional upload page that lets the user select a PBN file, sends it to the API, and shows results or errors.

#### Step 5.1: Create `SessionState` service

**File:** `src/BridgeGameCalculator.Client/Services/SessionState.cs`

```csharp
namespace BridgeGameCalculator.Client.Services;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Holds the currently-loaded session in memory. Scoped to the browser tab lifetime.
/// </summary>
public sealed class SessionState
{
    public Session? CurrentSession { get; set; }
}
```

Register in `Program.cs` of the Client project as a singleton (in WASM, singleton = per-tab):

```csharp
builder.Services.AddSingleton<SessionState>();
```

#### Step 5.2: Create the upload page

**File:** `src/BridgeGameCalculator.Client/Pages/Upload.razor`

```razor
@page "/"
@page "/upload"
@using BridgeGameCalculator.Shared.Models
@using BridgeGameCalculator.Client.Services
@inject HttpClient Http
@inject SessionState SessionState
@inject NavigationManager Navigation

<h1>Upload PBN File</h1>

<div class="upload-area">
    <InputFile OnChange="HandleFileSelected" accept=".pbn" />
</div>

@if (_isLoading)
{
    <div class="loading-indicator">
        <p>Parsing boards...</p>
    </div>
}

@if (_errorMessage is not null)
{
    <div class="error-message" role="alert">
        <h3>Upload Error</h3>
        <p>@_errorMessage</p>
    </div>
}

@if (_session is not null)
{
    <div class="upload-success">
        <h3>Success</h3>
        <p>Loaded @_session.Boards.Count boards from @_session.SourceFile</p>
    </div>
}

@code {
    private bool _isLoading;
    private string? _errorMessage;
    private Session? _session;

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        _errorMessage = null;
        _session = null;
        _isLoading = true;

        try
        {
            var file = e.File;

            if (file.Size > 1_048_576)
            {
                _errorMessage = "File exceeds the 1 MB size limit.";
                return;
            }

            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream(maxAllowedSize: 1_048_576);
            using var streamContent = new StreamContent(stream);
            content.Add(streamContent, "file", file.Name);

            var response = await Http.PostAsync("/api/sessions", content);

            if (response.IsSuccessStatusCode)
            {
                _session = await response.Content.ReadFromJsonAsync<Session>();
                SessionState.CurrentSession = _session;
                // Future: Navigation.NavigateTo("/results");
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<PbnParseError>();
                _errorMessage = error?.Message ?? "An unknown error occurred.";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to upload file: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
```

#### Step 5.3: Configure HttpClient in Client `Program.cs`

**File:** `src/BridgeGameCalculator.Client/Program.cs`

Ensure the `HttpClient` base address points to the server host:

```csharp
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
```

This is typically already configured by the Blazor WASM template, but verify it is present.

#### Step 5.4: Configure JSON deserialization on the client

In the Client `Program.cs`, configure the `JsonSerializerOptions` to match the server:

```csharp
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.Converters.Add(new JsonStringEnumConverter());
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
```

Alternatively, use a shared `JsonSerializerOptions` constant in the Shared project. Since `ReadFromJsonAsync` uses default options, create a shared helper:

**File:** `src/BridgeGameCalculator.Shared/Json/JsonDefaults.cs`

```csharp
namespace BridgeGameCalculator.Shared.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
```

Use `JsonDefaults.Options` in both the `Upload.razor` `ReadFromJsonAsync` calls and on the server.

---

### Phase 6: Tests

**Goal:** Comprehensive xUnit test coverage for the PBN parser.

#### Step 6.1: Create test data constants

**File:** `tests/BridgeGameCalculator.Tests/TestData/PbnTestData.cs`

**Namespace:** `BridgeGameCalculator.Tests.TestData`

A static class containing string constants for PBN test content. Each constant is a complete (or intentionally malformed) PBN snippet.

- `ValidSingleBoard` -- a single board with all required and optional tags.
- `ValidTwoBoards` -- two complete boards.
- `Valid28Boards` -- 28 boards for performance/scale (can generate programmatically in a helper method).
- `PassedOutBoard` -- a board with `[Contract "Pass"]` and no declarer/result.
- `PassedOutBoardWithResult` -- EC-7: passed-out board that still has a `[Result "0"]` tag.
- `MissingDealTag` -- a board missing `[Deal ...]`.
- `DuplicateCard` -- a deal where the same card appears in two hands.
- `Hand13CardViolation` -- a hand with 12 cards.
- `UnrecognizedTags` -- a valid board with extra non-standard tags like `[Score "NS 600"]`.
- `EmptyFile` -- empty string.
- `NotPbnContent` -- plain English text, not PBN.
- `MissingContractAndResult` -- EC-8: board with deal but no contract/result tags.

#### Step 6.2: Create parser unit tests

**File:** `tests/BridgeGameCalculator.Tests/Services/PbnParserTests.cs`

**Namespace:** `BridgeGameCalculator.Tests.Services`

**Class:** `public sealed class PbnParserTests`

Each test method instantiates `PbnParser`, creates a `MemoryStream` from the test data string, calls `Parse()`, and asserts on the `Result`.

**Test cases:**

| Test Method | Data | Assertion |
|---|---|---|
| `Parse_ValidSingleBoard_ReturnsSessionWithOneBoard` | `ValidSingleBoard` | `result.IsSuccess`, 1 board, correct board number/dealer/vulnerability/hands/contract/declarer/result |
| `Parse_ValidTwoBoards_ReturnsSessionWithTwoBoards` | `ValidTwoBoards` | `result.IsSuccess`, 2 boards, boards are ordered by board number |
| `Parse_PassedOutBoard_ReturnsNullContractDeclarerResult` | `PassedOutBoard` | `result.IsSuccess`, `board.Contract` is null, `board.Declarer` is null, `board.Result` is null |
| `Parse_PassedOutBoardWithResult_IgnoresResultReturnsSuccess` | `PassedOutBoardWithResult` | `result.IsSuccess`, contract is null, result is null (ignored per EC-7) |
| `Parse_MissingDealTag_ReturnsError` | `MissingDealTag` | `result.IsError`, error message mentions board number and "Deal" |
| `Parse_DuplicateCard_ReturnsError` | `DuplicateCard` | `result.IsError`, error message mentions "appears more than once" |
| `Parse_HandNot13Cards_ReturnsError` | `Hand13CardViolation` | `result.IsError`, error message mentions "does not have 13 cards" |
| `Parse_UnrecognizedTags_AreIgnored` | `UnrecognizedTags` | `result.IsSuccess`, board parsed correctly despite extra tags |
| `Parse_EmptyFile_ReturnsError` | `EmptyFile` | `result.IsError`, message: "contains no boards" |
| `Parse_NotPbnContent_ReturnsError` | `NotPbnContent` | `result.IsError`, message: "does not appear to be a valid PBN file" |
| `Parse_MissingContractAndResult_ImportsBoardWithNulls` | `MissingContractAndResult` | `result.IsSuccess`, contract/declarer/result are all null |
| `Parse_SetsSourceFileName` | `ValidSingleBoard` | `result.Value.SourceFile == "test.pbn"` |
| `Parse_DoubledContract_ParsesDoubleState` | Custom data with `"4HX"` | `contract.DoubleState == DoubleState.Doubled` |
| `Parse_RedoubledContract_ParsesDoubleState` | Custom data with `"4HXX"` | `contract.DoubleState == DoubleState.Redoubled` |
| `Parse_NoTrumpContract_ParsesStrain` | Custom data with `"3NT"` | `contract.Strain == Strain.NoTrump` |

#### Step 6.3: Create domain model unit tests

**File:** `tests/BridgeGameCalculator.Tests/Models/BoardTests.cs`

Verify that the `Board` record properties are correctly settable and that the nullable fields behave as expected. These are lightweight construction tests.

| Test Method | Assertion |
|---|---|
| `Board_WithContract_AllPropertiesSet` | All properties have expected values |
| `Board_PassedOut_NullablePropertiesAreNull` | `Contract`, `Declarer`, `Result` are null |

#### Step 6.4: Create API integration test

**File:** `tests/BridgeGameCalculator.Tests/Api/SessionsEndpointTests.cs`

Use `WebApplicationFactory<Program>` to test the endpoint end-to-end.

```csharp
public sealed class SessionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
```

Requires making `Program` accessible from the test project. Add to the Server project:

**File:** `src/BridgeGameCalculator.Server/Properties/InternalsVisibleTo.cs` (or an assembly attribute in `Program.cs`):

```csharp
[assembly: InternalsVisibleTo("BridgeGameCalculator.Tests")]
```

Also add a partial `Program` class at the bottom of `Program.cs` to make the top-level statements class accessible:

```csharp
// Required for WebApplicationFactory in integration tests
public partial class Program { }
```

**Test cases:**

| Test Method | Assertion |
|---|---|
| `PostSession_ValidPbn_Returns200WithSession` | Status 200, body deserializes to `Session` with correct board count |
| `PostSession_MalformedPbn_Returns422WithError` | Status 422, body deserializes to `PbnParseError` |
| `PostSession_EmptyFile_Returns422` | Status 422 |
| `PostSession_FileTooLarge_Returns422` | Send >1MB file, status 422, message mentions size limit |

---

## File Inventory

### New Files

**Solution and project files:**
- `src/BridgeGameCalculator.sln` -- solution file
- `src/BridgeGameCalculator.Shared/BridgeGameCalculator.Shared.csproj` -- class library
- `src/BridgeGameCalculator.Server/BridgeGameCalculator.Server.csproj` -- ASP.NET Core web project
- `src/BridgeGameCalculator.Client/BridgeGameCalculator.Client.csproj` -- Blazor WASM project
- `tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj` -- xUnit test project

**Shared domain model:**
- `src/BridgeGameCalculator.Shared/Models/Seat.cs` -- Seat enum (N/E/S/W)
- `src/BridgeGameCalculator.Shared/Models/Vulnerability.cs` -- Vulnerability enum
- `src/BridgeGameCalculator.Shared/Models/Strain.cs` -- Strain enum (C/D/H/S/NT)
- `src/BridgeGameCalculator.Shared/Models/DoubleState.cs` -- DoubleState enum (Undoubled/Doubled/Redoubled)
- `src/BridgeGameCalculator.Shared/Models/Hands.cs` -- Hands record (N/E/S/W hand strings)
- `src/BridgeGameCalculator.Shared/Models/Contract.cs` -- Contract record (level/strain/double)
- `src/BridgeGameCalculator.Shared/Models/Board.cs` -- Board entity
- `src/BridgeGameCalculator.Shared/Models/Session.cs` -- Session entity
- `src/BridgeGameCalculator.Shared/Models/PbnParseError.cs` -- Structured parse error
- `src/BridgeGameCalculator.Shared/Result.cs` -- Generic Result<T,E> type
- `src/BridgeGameCalculator.Shared/Json/JsonDefaults.cs` -- Shared JSON serialization options

**Server:**
- `src/BridgeGameCalculator.Server/Program.cs` -- Application entry point, DI, endpoint registration
- `src/BridgeGameCalculator.Server/Services/PbnParser.cs` -- PBN file parser
- `src/BridgeGameCalculator.Server/Services/CardConstants.cs` -- Valid card rank constants

**Client:**
- `src/BridgeGameCalculator.Client/Program.cs` -- Blazor WASM entry point
- `src/BridgeGameCalculator.Client/Pages/Upload.razor` -- Upload page component
- `src/BridgeGameCalculator.Client/Services/SessionState.cs` -- In-memory session holder

**Tests:**
- `tests/BridgeGameCalculator.Tests/TestData/PbnTestData.cs` -- PBN string constants for tests
- `tests/BridgeGameCalculator.Tests/Services/PbnParserTests.cs` -- Parser unit tests (15 test cases)
- `tests/BridgeGameCalculator.Tests/Models/BoardTests.cs` -- Domain model construction tests
- `tests/BridgeGameCalculator.Tests/Api/SessionsEndpointTests.cs` -- API integration tests (4 test cases)

### Modified Files

None -- this is a greenfield project.

---

## Testing Strategy

**Unit tests (PbnParserTests):** The parser is the core logic of this feature. Test it exhaustively by passing PBN content as `MemoryStream` strings. No mocking, no I/O, no HTTP. Covers:
- Happy path: single board, multiple boards, all tag types.
- Passed-out boards: null contract/declarer/result.
- Edge cases: EC-1 through EC-8 from the spec.
- Contract parsing: all strains, doubled, redoubled, notrump.
- Validation: 13-card rule, 52-card deck, duplicate detection.

**Integration tests (SessionsEndpointTests):** Use `WebApplicationFactory<Program>` to spin up the real server in-process. Send `MultipartFormDataContent` with PBN file bytes. Assert on HTTP status codes and response body deserialization. Validates the full pipeline from HTTP to parser to JSON response.

**Model tests (BoardTests):** Lightweight construction tests verifying that the domain model objects are correctly initialized. Ensures records and nullable properties behave as expected.

**Not tested in this feature (deferred):** Blazor component tests for `Upload.razor`. These require bUnit and are better addressed once the UI stabilizes. The dependency is added in Phase 1 but the tests are out of scope for FEAT-001.

**Test runner:** `dotnet test tests/BridgeGameCalculator.Tests/BridgeGameCalculator.Tests.csproj`

---

## Migration Notes

- **No database.** The application is stateless (per CLAUDE.md and spec constraint 9.2). No migrations.
- **No backwards compatibility concerns.** This is the first feature in a greenfield project.
- **No feature flags needed.** The upload page is the entry point of the application.
- **PBN format reference.** The parser should handle the PBN 2.1 standard tag set. The authoritative format reference is: `[TagName "Value"]` on a single line. The `Deal` tag format is `<seat>:<hand> <hand> <hand> <hand>` where hands are in clockwise order from the named seat. Suits within a hand are separated by `.` in the order Spades.Hearts.Diamonds.Clubs.
- **Future extensibility.** The `Session` and `Board` models are designed to be extended by FEAT-002 (DD analysis) which will add `ParScore` and `Delta` properties. These will likely be added as separate result types that wrap a `Board` rather than modifying `Board` itself, keeping the import model clean.
