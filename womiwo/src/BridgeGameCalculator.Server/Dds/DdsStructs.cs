namespace BridgeGameCalculator.Server.Dds;

using System.Runtime.InteropServices;

/// <summary>DDS library constants.</summary>
internal static class DdsConstants
{
    /// <summary>Maximum boards per batch call. Matches MAXNOOFBOARDS in dds.h.</summary>
    public const int MaxNoOfBoards = 200;

    /// <summary>Number of strains (S, H, D, C, NT).</summary>
    public const int DdsSuits = 5;

    /// <summary>Number of seats (N, E, S, W).</summary>
    public const int DdsHands = 4;
}

/// <summary>
/// A single PBN deal for DD table calculation.
/// Maps to DDS struct ddTableDealPBN.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct DdTableDealPbn
{
    /// <summary>
    /// PBN deal string: "N:s.h.d.c s.h.d.c s.h.d.c s.h.d.c" (80-char buffer).
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string Cards;
}

/// <summary>
/// Batch of PBN deals for CalcAllTablesPBN.
/// Maps to DDS struct ddTableDealsPBN.
/// Array size uses MAXNOOFBOARDS * 5 per the dds.h definition.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTableDealsPbn
{
    public int NoOfTables;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.MaxNoOfBoards * 5)]
    public DdTableDealPbn[] Deals;
}

/// <summary>
/// DD trick results for a single deal: 5 strains × 4 declarers = 20 ints.
/// Maps to DDS struct ddTableResults.
/// Layout: ResTable[strain * 4 + hand], where strain 0=S,1=H,2=D,3=C,4=NT
/// and hand 0=N,1=E,2=S,3=W.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTableResults
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.DdsSuits * DdsConstants.DdsHands)]
    public int[] ResTable;
}

/// <summary>
/// Batch of DD table results from CalcAllTablesPBN.
/// Maps to DDS struct ddTablesRes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DdTablesRes
{
    public int NoOfBoards;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.MaxNoOfBoards * 5)]
    public DdTableResults[] Results;
}

/// <summary>
/// Par score results from DealerPar for one board.
/// Maps to DDS struct parResultsDealer.
/// Score[0] = NS par score, Score[1] = EW par score.
/// Contracts0 = NS par contract string, Contracts1 = EW.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct ParResultsDealer
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] Score;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Contracts0;   // NS side

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Contracts1;   // EW side
}

/// <summary>
/// Batch par results from CalcAllTablesPBN.
/// Maps to DDS struct allParResults.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AllParResults
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = DdsConstants.MaxNoOfBoards * 5)]
    public ParResultsDealer[] PresResults;
}
