using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Sigwalk.Core.Interop;

/// <summary>把本机 PCCERT_CONTEXT 复制为托管证书对象，避免持有原生句柄。</summary>
internal static class CertificateContextHelper
{
    public static X509Certificate2? ToCertificate(IntPtr pCertContext)
    {
        if (pCertContext == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var context = Marshal.PtrToStructure<CERT_CONTEXT>(pCertContext);
            if (context.pbCertEncoded == IntPtr.Zero || context.cbCertEncoded == 0)
            {
                return null;
            }

            var data = new byte[context.cbCertEncoded];
            Marshal.Copy(context.pbCertEncoded, data, 0, data.Length);
            return new X509Certificate2(data);
        }
        catch
        {
            return null;
        }
    }
}
