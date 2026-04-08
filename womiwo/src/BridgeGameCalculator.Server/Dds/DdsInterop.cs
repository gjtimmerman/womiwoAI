namespace BridgeGameCalculator.Server.Dds;

using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// P/Invoke declarations for Bo Haglund's DDS library.
/// Uses classic DllImport because the DDS structs contain fixed-size arrays
/// with custom MarshalAs attributes, which are not supported by the LibraryImport
/// source generator.
/// Return code 1 = success; negative values are errors.
/// </summary>
internal static class DdsInterop
{
    private const string DdsLibrary = "dds";

    /// <summary>
    /// Set maximum threads the DDS library may use internally.
    /// Pass 0 to let DDS auto-detect based on CPU cores.
    /// Also used as a startup health check — throws DllNotFoundException if
    /// the native library is absent.
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall, EntryPoint = "SetMaxThreads")]
    public static extern void SetMaxThreads(int userThreads);

    /// <summary>
    /// Batch DD table calculation for multiple PBN deals.
    /// Returns 1 on success.
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall, EntryPoint = "CalcAllTablesPBN")]
    public static extern int CalcAllTablesPBN(
        ref DdTableDealsPbn dealsp,
        int                 mode,
        [In] int[]          trumpFilter,
        ref DdTablesRes     resp,
        ref AllParResults   presp);

    /// <summary>
    /// Calculate par score for a single DD table result.
    /// dealer: 0=N, 1=E, 2=S, 3=W.
    /// vulnerable: 0=None, 1=Both, 2=NS, 3=EW.
    /// Returns 1 on success.
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall, EntryPoint = "DealerPar")]
    public static extern int DealerPar(
        ref DdTableResults   tablep,
        ref ParResultsDealer presp,
        int                  dealer,
        int                  vulnerable);

    /// <summary>
    /// Calculate DD table for a single PBN deal (non-batch).
    /// Used for single-hand analysis (FEAT-006).
    /// Returns 1 on success.
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall, EntryPoint = "CalcDDtablePBN")]
    public static extern int CalcDDtablePBN(
        DdTableDealPbn      tableDeal,
        ref DdTableResults  tablep);

    /// <summary>
    /// Translate a DDS error code into a human-readable string (max 80 chars).
    /// </summary>
    [DllImport(DdsLibrary, CallingConvention = CallingConvention.StdCall, EntryPoint = "ErrorMessage")]
    public static extern void ErrorMessage(
        int returnCode,
        [MarshalAs(UnmanagedType.LPStr)] StringBuilder message);
}
