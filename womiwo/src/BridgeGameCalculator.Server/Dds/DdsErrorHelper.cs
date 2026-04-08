namespace BridgeGameCalculator.Server.Dds;

using System.Text;

/// <summary>Translates DDS return codes into human-readable messages.</summary>
internal static class DdsErrorHelper
{
    private const int DdsReturnNoFault = 1;

    public static bool IsSuccess(int returnCode) => returnCode == DdsReturnNoFault;

    public static string GetErrorMessage(int returnCode)
    {
        if (IsSuccess(returnCode)) return string.Empty;

        var sb = new StringBuilder(80);
        try
        {
            DdsInterop.ErrorMessage(returnCode, sb);
        }
        catch
        {
            return $"DDS error code: {returnCode}";
        }

        var message = sb.ToString().Trim();
        return string.IsNullOrEmpty(message)
            ? $"DDS error code: {returnCode}"
            : message;
    }
}
