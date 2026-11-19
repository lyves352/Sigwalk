using System.Runtime.InteropServices;

namespace Sigwalk.Core.Interop;

/// <summary>wintrust.dll 原生函数。</summary>
internal static unsafe class WinTrustNative
{
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    public static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionId, ref WINTRUST_DATA pWvtData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern IntPtr WTHelperProvDataFromStateData(IntPtr hStateData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern IntPtr WTHelperGetProvSignerFromChain(
        IntPtr pProvData,
        uint idxSigner,
        [MarshalAs(UnmanagedType.Bool)] bool fCounterSigner,
        uint idxCounterSigner);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern IntPtr WTHelperGetProvCertFromChain(IntPtr pSgnr, uint idxCert);
}
