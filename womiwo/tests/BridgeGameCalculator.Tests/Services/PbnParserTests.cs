namespace BridgeGameCalculator.Tests.Services;

using System.Text;
using BridgeGameCalculator.Server.Services;
using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Tests.TestData;

public sealed class PbnParserTests
{
    private readonly PbnParser _parser = new();

    private static Stream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    // --- Happy path ---

    [Fact]
    public void Parse_ValidSingleBoard_ReturnsSessionWithOneBoard()
    {
        var result = _parser.Parse(ToStream(PbnTestData.ValidSingleBoard), "test.pbn");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Boards);

        var board = result.Value.Boards[0];
        Assert.Equal(1,                    board.BoardNumber);
        Assert.Equal(Seat.North,           board.Dealer);
        Assert.Equal(Vulnerability.None,   board.Vulnerability);
        Assert.NotNull(board.Contract);
        Assert.Equal(3,                    board.Contract.Level);
        Assert.Equal(Strain.NoTrump,       board.Contract.Strain);
        Assert.Equal(DoubleState.Undoubled, board.Contract.DoubleState);
        Assert.Equal(Seat.North,           board.Declarer);
        Assert.Equal(9,                    board.Result);
    }

    [Fact]
    public void Parse_ValidTwoBoards_ReturnsSessionWithTwoBoards()
    {
        var result = _parser.Parse(ToStream(PbnTestData.ValidTwoBoards), "two.pbn");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Boards.Count);
        Assert.Equal(1, result.Value.Boards[0].BoardNumber);
        Assert.Equal(2, result.Value.Boards[1].BoardNumber);
    }

    [Fact]
    public void Parse_SetsSourceFileName()
    {
        var result = _parser.Parse(ToStream(PbnTestData.ValidSingleBoard), "mysession.pbn");

        Assert.True(result.IsSuccess);
        Assert.Equal("mysession.pbn", result.Value.SourceFile);
    }

    // --- Passed-out boards ---

    [Fact]
    public void Parse_PassedOutBoard_ReturnsNullContractDeclarerResult()
    {
        var result = _parser.Parse(ToStream(PbnTestData.PassedOutBoard), "test.pbn");

        Assert.True(result.IsSuccess);
        var board = result.Value.Boards[0];
        Assert.Null(board.Contract);
        Assert.Null(board.Declarer);
        Assert.Null(board.Result);
        Assert.True(board.IsPassedOut);
    }

    [Fact]
    public void Parse_PassedOutBoardWithResult_IgnoresResultAndReturnsSuccess()
    {
        var result = _parser.Parse(ToStream(PbnTestData.PassedOutBoardWithResult), "test.pbn");

        Assert.True(result.IsSuccess);
        var board = result.Value.Boards[0];
        Assert.Null(board.Contract);
        Assert.Null(board.Result);
    }

    // --- Error cases ---

    [Fact]
    public void Parse_MissingDealTag_ReturnsError()
    {
        var result = _parser.Parse(ToStream(PbnTestData.MissingDealTag), "test.pbn");

        Assert.True(result.IsError);
        Assert.Contains("Deal", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_DuplicateCard_ReturnsError()
    {
        var result = _parser.Parse(ToStream(PbnTestData.DuplicateCard), "test.pbn");

        Assert.True(result.IsError);
        Assert.Contains("more than once", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_HandNot13Cards_ReturnsError()
    {
        var result = _parser.Parse(ToStream(PbnTestData.Hand13CardViolation), "test.pbn");

        Assert.True(result.IsError);
        Assert.Contains("13 cards", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsError()
    {
        var result = _parser.Parse(ToStream(PbnTestData.EmptyFile), "empty.pbn");

        Assert.True(result.IsError);
        Assert.Contains("no boards", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NotPbnContent_ReturnsError()
    {
        var result = _parser.Parse(ToStream(PbnTestData.NotPbnContent), "text.pbn");

        Assert.True(result.IsError);
    }

    // --- Edge cases ---

    [Fact]
    public void Parse_UnrecognizedTags_AreIgnoredAndBoardParsedSuccessfully()
    {
        var result = _parser.Parse(ToStream(PbnTestData.UnrecognizedTags), "test.pbn");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Boards);
    }

    [Fact]
    public void Parse_MissingContractAndResult_ImportsBoardWithNullContractAndResult()
    {
        var result = _parser.Parse(ToStream(PbnTestData.MissingContractAndResult), "test.pbn");

        Assert.True(result.IsSuccess);
        var board = result.Value.Boards[0];
        Assert.Null(board.Contract);
        Assert.Null(board.Declarer);
        Assert.Null(board.Result);
    }

    // --- Contract parsing ---

    [Fact]
    public void Parse_DoubledContract_ParsesDoubleState()
    {
        var result = _parser.Parse(ToStream(PbnTestData.DoubledContract), "test.pbn");

        Assert.True(result.IsSuccess);
        Assert.Equal(DoubleState.Doubled, result.Value.Boards[0].Contract!.DoubleState);
    }

    [Fact]
    public void Parse_RedoubledContract_ParsesDoubleState()
    {
        var result = _parser.Parse(ToStream(PbnTestData.RedoubledContract), "test.pbn");

        Assert.True(result.IsSuccess);
        Assert.Equal(DoubleState.Redoubled, result.Value.Boards[0].Contract!.DoubleState);
    }

    [Fact]
    public void Parse_NoTrumpContract_ParsesStrain()
    {
        var result = _parser.Parse(ToStream(PbnTestData.NoTrumpContract), "test.pbn");

        Assert.True(result.IsSuccess);
        Assert.Equal(Strain.NoTrump, result.Value.Boards[0].Contract!.Strain);
    }
}
