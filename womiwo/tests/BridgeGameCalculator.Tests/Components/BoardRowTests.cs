using Bunit;
using BridgeGameCalculator.Client.Components;
using BridgeGameCalculator.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace BridgeGameCalculator.Tests.Components;

public sealed class BoardRowTests : TestContext
{
    private static BoardResult MakeBoard(int? impDelta = null, int? actualScore = null) =>
        new(
            BoardNumber:      1,
            VulnerabilityLabel: "None",
            ContractPlayed:   "3NT by N",
            TricksResult:     "=",
            ActualScore:      actualScore,
            ParContractLabel: "3NT by N",
            ParScore:         400,
            ImpDelta:         impDelta);

    [Fact]
    public void Renders_all_columns()
    {
        var board = MakeBoard(impDelta: 3, actualScore: 400);
        var cut = RenderComponent<BoardRow>(p => p.Add(x => x.Board, board));

        var cells = cut.FindAll("td");
        Assert.Equal(8, cells.Count);
    }

    [Fact]
    public void Shows_pass_when_contract_null()
    {
        var board = new BoardResult(1, "None", null, null, null, null, 0, null);
        var cut = RenderComponent<BoardRow>(p => p.Add(x => x.Board, board));

        var cells = cut.FindAll("td");
        Assert.Equal("Pass", cells[2].TextContent);  // ContractPlayed column
        Assert.Equal("N/A", cells[3].TextContent);   // TricksResult column
        Assert.Equal("N/A", cells[4].TextContent);   // ActualScore column
        Assert.Equal("Pass", cells[5].TextContent);  // ParContractLabel column
        Assert.Equal("N/A", cells[7].TextContent);   // ImpDelta column
    }

    [Theory]
    [InlineData(5,  "delta-positive", "+5")]
    [InlineData(-3, "delta-negative", "-3")]
    [InlineData(0,  "delta-neutral",  "0")]
    public void Delta_cell_has_correct_css_and_text(int impDelta, string expectedCss, string expectedText)
    {
        var board = MakeBoard(impDelta: impDelta, actualScore: 400);
        var cut = RenderComponent<BoardRow>(p => p.Add(x => x.Board, board));

        var deltaCell = cut.FindAll("td")[7];
        Assert.Contains(expectedCss, deltaCell.ClassName ?? "");
        Assert.Equal(expectedText, deltaCell.TextContent);
    }

    [Fact]
    public void Click_invokes_callback_with_board_number()
    {
        var board = MakeBoard(impDelta: 0, actualScore: 400);
        int? clicked = null;

        var cut = RenderComponent<BoardRow>(p =>
        {
            p.Add(x => x.Board, board);
            p.Add(x => x.OnBoardClicked, EventCallback.Factory.Create<int>(this, n => clicked = n));
        });

        cut.Find("tr").Click();

        Assert.Equal(1, clicked);
    }
}
