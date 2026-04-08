namespace BridgeGameCalculator.Tests.Dds;

using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Shared.Models;

public sealed class DdsParContractParserTests
{
    [Fact]
    public void Parse_Null_ReturnsEmpty()
    {
        Assert.Empty(DdsParContractParser.Parse(null));
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(DdsParContractParser.Parse(""));
    }

    [Theory]
    [InlineData("pass")]
    [InlineData("Pass")]
    [InlineData("PASS")]
    public void Parse_PassString_ReturnsEmpty(string input)
    {
        Assert.Empty(DdsParContractParser.Parse(input));
    }

    [Fact]
    public void Parse_Simple4S_ReturnsOneContract()
    {
        var result = DdsParContractParser.Parse("4S-N");
        Assert.Single(result);
        Assert.Equal(4,              result[0].Level);
        Assert.Equal(Strain.Spades,  result[0].Strain);
        Assert.Equal(Seat.North,     result[0].Declarer);
        Assert.Equal(DoubleState.Undoubled, result[0].DoubleState);
    }

    [Fact]
    public void Parse_Doubled_ParsesDoubleState()
    {
        var result = DdsParContractParser.Parse("4S*-N");
        Assert.Single(result);
        Assert.Equal(DoubleState.Doubled, result[0].DoubleState);
        Assert.Equal(4, result[0].Level);
    }

    [Fact]
    public void Parse_NoTrump_ParsesStrain()
    {
        var result = DdsParContractParser.Parse("3NT-E");
        Assert.Single(result);
        Assert.Equal(Strain.NoTrump, result[0].Strain);
        Assert.Equal(Seat.East,      result[0].Declarer);
    }

    [Fact]
    public void Parse_MultipleDeclarers_ReturnsOneContractPerDeclarer()
    {
        var result = DdsParContractParser.Parse("4S-NS");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Declarer == Seat.North);
        Assert.Contains(result, r => r.Declarer == Seat.South);
        Assert.All(result, r => Assert.Equal(4, r.Level));
        Assert.All(result, r => Assert.Equal(Strain.Spades, r.Strain));
    }

    [Fact]
    public void Parse_CommaSeparated_ReturnsAllContracts()
    {
        var result = DdsParContractParser.Parse("4S-N,3NT-E");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Strain == Strain.Spades  && r.Declarer == Seat.North);
        Assert.Contains(result, r => r.Strain == Strain.NoTrump && r.Declarer == Seat.East);
    }

    [Theory]
    [InlineData("1H-N")]
    [InlineData("7NT-S")]
    [InlineData("2C-W")]
    [InlineData("6D-E")]
    public void Parse_AllStrains_ParsesCorrectly(string input)
    {
        var result = DdsParContractParser.Parse(input);
        Assert.Single(result);
    }

    [Fact]
    public void Parse_InvalidInput_ReturnsEmpty_NotThrows()
    {
        // Completely malformed — should not throw
        var result = DdsParContractParser.Parse("BOGUS");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EWDeclarers_ReturnsBothSeats()
    {
        var result = DdsParContractParser.Parse("3NT-EW");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Declarer == Seat.East);
        Assert.Contains(result, r => r.Declarer == Seat.West);
    }
}
