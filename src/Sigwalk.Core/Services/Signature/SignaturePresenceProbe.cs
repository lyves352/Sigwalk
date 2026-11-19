using Sigwalk.Core.Interop;

namespace Sigwalk.Core.Services.Signature;

/// <summary>探测文件是否包含嵌入式 PKCS#7 签名（不包含目录签名）。</summary>
internal sealed class SignaturePresenceProbe
{
    public bool HasEmbeddedSignature(string filePath)
    {
        IntPtr store = IntPtr.Zero;
        IntPtr message = IntPtr.Zero;
        IntPtr context = IntPtr.Zero;

        try
        {
            var ok = Crypt32Native.CryptQueryObject(
                WinTrustConstants.CERT_QUERY_OBJECT_FILE,
                filePath,
                WinTrustConstants.CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                WinTrustConstants.CERT_QUERY_FORMAT_FLAG_BINARY,
                0,
                out _,
                out var contentType,
                out _,
                out store,
                out message,
                out context);

            return ok && contentType == WinTrustConstants.CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (context != IntPtr.Zero)
            {
                Crypt32Native.CertFreeCertificateContext(context);
            }

            if (message != IntPtr.Zero)
            {
                Crypt32Native.CryptMsgClose(message);
            }

            if (store != IntPtr.Zero)
            {
                Crypt32Native.CertCloseStore(store, 0);
            }
        }
    }
}
