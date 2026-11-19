using System.Runtime.InteropServices;

namespace Sigwalk.Core.Interop;

/// <summary>crypt32.dll 中用于探测嵌入式签名的函数。</summary>
internal static class Crypt32Native
{
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptQueryObject(
        uint dwObjectType,
        string pvObject,
        uint dwExpectedContentTypeFlags,
        uint dwExpectedFormatTypeFlags,
        uint dwFlags,
        out uint pdwMsgAndCertEncodingType,
        out uint pdwContentType,
        out uint pdwFormatType,
        out IntPtr phCertStore,
        out IntPtr phMsg,
        out IntPtr ppvContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CertCloseStore(IntPtr hCertStore, uint dwFlags);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptMsgClose(IntPtr hCryptMsg);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CertFreeCertificateContext(IntPtr pCertContext);
}
