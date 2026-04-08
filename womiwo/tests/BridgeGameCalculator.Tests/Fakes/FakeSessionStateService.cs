using BridgeGameCalculator.Client.Services;
using BridgeGameCalculator.Shared.ViewModels;

namespace BridgeGameCalculator.Tests.Fakes;

public sealed class FakeSessionStateService : ISessionStateService
{
    private readonly Dictionary<int, BoardDetailViewModel> _boards = new();

    public bool HasSession { get; set; } = true;

    public IReadOnlyList<int> BoardNumbers =>
        _boards.Keys.OrderBy(k => k).ToList();

    public void SetBoard(BoardDetailViewModel vm) => _boards[vm.BoardNumber] = vm;

    public BoardDetailViewModel? GetBoardDetail(int boardNumber) =>
        _boards.TryGetValue(boardNumber, out var vm) ? vm : null;
}
