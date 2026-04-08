using System.Text.Json.Serialization;
using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Server.Services;
using BridgeGameCalculator.Shared.Dtos;
using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Parsing;
using BridgeGameCalculator.Shared.Validation;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddSingleton<PbnParser>();
builder.Services.AddSingleton<IDdsAnalysisService, DdsAnalysisService>();
builder.Services.AddSingleton<DeltaCalculationService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// --- Middleware ---
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// --- API Endpoints ---
app.MapPost("/api/sessions", async (IFormFile file, PbnParser parser) =>
{
    if (file.Length == 0)
        return Results.UnprocessableEntity(
            new PbnParseError("The uploaded file is empty."));

    if (file.Length > 1_048_576)
        return Results.UnprocessableEntity(
            new PbnParseError("File exceeds the 1 MB size limit."));

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

// --- DD Analysis Endpoint ---
// Accepts the parsed Session from the client, runs DD analysis + delta calculation,
// and returns a fully assembled SessionAnalysisResult ready for the dashboard.
app.MapPost("/api/analyze", async (
    Session session,
    IDdsAnalysisService     dds,
    DeltaCalculationService deltaService,
    CancellationToken       cancellationToken) =>
{
    var analysisResults = await dds.AnalyzeSessionAsync(session.Boards, cancellationToken);

    var parResults = analysisResults
        .Where(r => r.IsSuccess && r.ParResult is not null)
        .Select(r => r.ParResult!)
        .ToList();

    var deltas = deltaService.CalculateDeltas(session.Boards, parResults);

    var result = SessionResultsAssembler.Assemble(session, analysisResults, deltas);
    return Results.Ok(result);
})
.Produces<SessionAnalysisResult>(200);

// --- Single-Hand Analysis Endpoint ---
app.MapPost("/api/hands/analyze", async (
    SingleHandRequest       request,
    IDdsAnalysisService     dds,
    DeltaCalculationService deltaService,
    CancellationToken       ct) =>
{
    // Parse all four hands
    var parseErrors = new Dictionary<string, string>();
    var parsed      = new Dictionary<string, HandParseResult>();

    foreach (var (seat, handStr) in new[]
    {
        ("North", request.NorthHand),
        ("East",  request.EastHand),
        ("South", request.SouthHand),
        ("West",  request.WestHand),
    })
    {
        var result = HandParser.Parse(handStr);
        if (!result.IsSuccess)
            parseErrors[seat] = result.Error!;
        else
            parsed[seat] = result;
    }

    if (parseErrors.Count > 0)
        return Results.BadRequest(new { errors = parseErrors });

    // Cross-hand + contract validation
    ContractInfo? contractInfo = null;
    if (request.ContractLevel.HasValue || request.ContractStrain.HasValue
        || request.Declarer.HasValue  || request.Result.HasValue)
    {
        contractInfo = new ContractInfo(
            request.ContractLevel, request.ContractStrain,
            request.Doubled, request.Declarer, request.Result);
    }

    var validation = HandValidator.Validate(
        parsed["North"].AllCards, parsed["East"].AllCards,
        parsed["South"].AllCards, parsed["West"].AllCards,
        contractInfo);

    if (!validation.IsValid)
        return Results.BadRequest(new { errors = validation.Errors });

    // Build Board
    var hands = new Hands(
        North: parsed["North"].PbnHand!,
        East:  parsed["East"].PbnHand!,
        South: parsed["South"].PbnHand!,
        West:  parsed["West"].PbnHand!);

    Contract? contract = request.ContractLevel.HasValue && request.ContractStrain.HasValue
        ? new Contract(
            request.ContractLevel.Value,
            request.ContractStrain.Value,
            request.Doubled ?? DoubleState.Undoubled)
        : null;

    var board = new Board
    {
        BoardNumber   = 1,
        Dealer        = request.Dealer,
        Vulnerability = request.Vulnerability,
        Hands         = hands,
        Contract      = contract,
        Declarer      = request.Declarer,
        Result        = request.Result,
    };

    // DD Analysis
    var analysisResult = await dds.AnalyzeBoardAsync(board, ct);

    var session = new Session { SourceFile = "Manual Entry", Boards = [board] };

    var parResults = analysisResult.ParResult is not null
        ? (IReadOnlyList<ParResult>)[analysisResult.ParResult]
        : [];

    var deltas  = deltaService.CalculateDeltas([board], parResults);
    var analysis = SessionResultsAssembler.Assemble(session, [analysisResult], deltas);

    return Results.Ok(new SingleHandAnalysisResult(session, analysis));
})
.Produces<SingleHandAnalysisResult>(200)
.Produces(400);

app.MapFallbackToFile("index.html");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
