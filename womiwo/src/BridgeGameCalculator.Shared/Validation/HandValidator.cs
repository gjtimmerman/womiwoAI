namespace BridgeGameCalculator.Shared.Validation;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Validates four bridge hands and an optional contract against the rules of bridge:
/// 13 cards per hand, 52 unique cards across hands, and contract field completeness.
/// Static — no dependencies.
/// </summary>
public static class HandValidator
{
    public static HandValidationResult Validate(
        IReadOnlyList<Card>? north,
        IReadOnlyList<Card>? east,
        IReadOnlyList<Card>? south,
        IReadOnlyList<Card>? west,
        ContractInfo?        contract = null)
    {
        var errors = new Dictionary<string, string>();

        // Each hand must be present and contain exactly 13 cards
        CheckHand(north, "North", errors);
        CheckHand(east,  "East",  errors);
        CheckHand(south, "South", errors);
        CheckHand(west,  "West",  errors);

        // Cross-hand uniqueness — only if all four hands parsed successfully
        if (errors.Count == 0)
        {
            var seen = new HashSet<(Suit, Rank)>();
            foreach (var card in north!.Concat(east!).Concat(south!).Concat(west!))
            {
                if (!seen.Add((card.Suit, card.Rank)))
                {
                    errors["Cards"] =
                        $"{RankChar(card.Rank)} of {card.Suit} appears in more than one hand.";
                    break;
                }
            }
        }

        // Contract completeness
        if (contract is not null)
            ValidateContract(contract, errors);

        return new HandValidationResult(errors);
    }

    // -------------------------------------------------------------------------

    private static void CheckHand(
        IReadOnlyList<Card>? hand, string seat, Dictionary<string, string> errors)
    {
        if (hand is null)
            errors[seat] = $"{seat} hand is required.";
        else if (hand.Count != 13)
            errors[seat] = $"{seat} hand must have exactly 13 cards; found {hand.Count}.";
    }

    private static void ValidateContract(ContractInfo contract, Dictionary<string, string> errors)
    {
        bool hasLevel    = contract.Level.HasValue;
        bool hasStrain   = contract.Strain.HasValue;
        bool hasDeclarer = contract.Declarer.HasValue;
        bool hasResult   = contract.Result.HasValue;

        // If nothing is set, the "no contract" case is valid
        if (!hasLevel && !hasStrain && !hasDeclarer && !hasResult)
            return;

        if (!hasLevel && hasResult)
            errors["ContractLevel"] = "Contract is required when a result is entered.";
        if (!hasLevel && hasDeclarer)
            errors.TryAdd("ContractLevel", "Contract level is required when a declarer is entered.");
        if (hasLevel && !hasStrain)
            errors["ContractStrain"] = "Contract strain is required when a level is entered.";
        if (hasLevel && !hasDeclarer)
            errors["Declarer"] = "Declarer is required when a contract is entered.";
        if (hasLevel && !hasResult)
            errors["Result"] = "Result (tricks made) is required when a contract is entered.";

        if (hasLevel && (contract.Level < 1 || contract.Level > 7))
            errors.TryAdd("ContractLevel", "Contract level must be between 1 and 7.");
        if (hasResult && (contract.Result < 0 || contract.Result > 13))
            errors.TryAdd("Result", "Result must be between 0 and 13.");
    }

    private static string RankChar(Rank rank) => rank switch
    {
        Rank.Ace   => "A",
        Rank.King  => "K",
        Rank.Queen => "Q",
        Rank.Jack  => "J",
        Rank.Ten   => "T",
        _          => ((int)rank).ToString()
    };
}
