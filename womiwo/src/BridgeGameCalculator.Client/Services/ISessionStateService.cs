namespace BridgeGameCalculator.Client.Services;

using BridgeGameCalculator.Shared.ViewModels;

public interface ISessionStateService
{
    bool HasSession { get; }
    IReadOnlyList<int> BoardNumbers { get; }
    BoardDetailViewModel? GetBoardDetail(int boardNumber);
}
