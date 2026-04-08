using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Parsing;
using BridgeGameCalculator.Shared.Validation;

namespace BridgeGameCalculator.Tests.Validation;

public sealed class HandValidatorTests
{
    private static readonly string[] ValidHands =
    [
        "AKQ2.32.AKQ2.AK3",
        "JT98.QJT9.J543.2",
        "7654.A876.T97.T9",
        "3.K54.86.QJ87654"
    ];

    private static IReadOnlyList<Card> ParseHand(string pbn)
    {
        var r = HandParser.Parse(pbn);
        return r.IsSuccess ? r.AllCards! : throw new InvalidOperationException(r.Error);
    }

    private static (IReadOnlyList<Card> N, IReadOnlyList<Card> E,
                    IReadOnlyList<Card> S, IReadOnlyList<Card> W) ValidDeck()
    {
        return (ParseHand(ValidHands[0]), ParseHand(ValidHands[1]),
                ParseHand(ValidHands[2]), ParseHand(ValidHands[3]));
    }

    [Fact]
    public void ValidDeck_NoContract_IsValid()
    {
        var (n, e, s, w) = ValidDeck();
        var result = HandValidator.Validate(n, e, s, w);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidDeck_FullContract_IsValid()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(4, Strain.Spades, DoubleState.Undoubled, Seat.South, 10);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NullHand_Fails_WithNamedError()
    {
        var (_, e, s, w) = ValidDeck();
        var result = HandValidator.Validate(null, e, s, w);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("North"));
        Assert.Contains("required", result.Errors["North"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateCardAcrossHands_Fails()
    {
        // North and East both have the Ace of Spades
        var northWithAceSpades = ParseHand("AKQ2.32.AKQ2.AK3");  // North has AS
        var eastWithAceSpades  = ParseHand("AT98.QJT9.J543.2");   // East also has AS

        var s = ParseHand(ValidHands[2]);
        var w = ParseHand(ValidHands[3]);

        var result = HandValidator.Validate(northWithAceSpades, eastWithAceSpades, s, w);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Cards"));
        Assert.Contains("A of Spades", result.Errors["Cards"]);
    }

    [Fact]
    public void ContractWithoutDeclarer_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(4, Strain.Spades, null, null, 10);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Declarer"));
    }

    [Fact]
    public void ContractWithoutResult_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(4, Strain.Spades, null, Seat.South, null);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Result"));
    }

    [Fact]
    public void ResultWithoutContract_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(null, null, null, null, 10);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("ContractLevel"));
    }

    [Fact]
    public void DeclarerWithoutContract_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(null, null, null, Seat.South, null);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("ContractLevel"));
    }

    [Fact]
    public void ResultOutOfRange_High_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(4, Strain.Spades, null, Seat.South, 14);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Result"));
    }

    [Fact]
    public void ResultOutOfRange_Low_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(4, Strain.Spades, null, Seat.South, -1);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Result"));
    }

    [Fact]
    public void LevelOutOfRange_Zero_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(0, Strain.Spades, null, Seat.South, 6);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("ContractLevel"));
    }

    [Fact]
    public void LevelOutOfRange_Eight_Fails()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(8, Strain.Spades, null, Seat.South, 14);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("ContractLevel"));
    }

    [Fact]
    public void AllContractFieldsAbsent_IsValid()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(null, null, null, null, null);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ResultZero_WithFullContract_IsValid()
    {
        var (n, e, s, w) = ValidDeck();
        var contract = new ContractInfo(7, Strain.NoTrump, DoubleState.Undoubled, Seat.North, 0);

        var result = HandValidator.Validate(n, e, s, w, contract);

        Assert.True(result.IsValid);
    }
}
