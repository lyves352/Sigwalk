using System.Runtime.InteropServices;
using Sigwalk.Core.Interop;

namespace Sigwalk.Tests;

public class InteropLayoutTests
{
    [Fact]
    public void WintrustDataLayoutMatchesX64()
    {
        Assert.Equal(88, Marshal.SizeOf<WINTRUST_DATA>());
        Assert.Equal(40, Marshal.OffsetOf<WINTRUST_DATA>(nameof(WINTRUST_DATA.pChoice)).ToInt32());
        Assert.Equal(80, Marshal.OffsetOf<WINTRUST_DATA>(nameof(WINTRUST_DATA.pSignatureSettings)).ToInt32());
    }

    [Fact]
    public void WintrustFileInfoAndSettingsLayoutMatchX64()
    {
        Assert.Equal(32, Marshal.SizeOf<WINTRUST_FILE_INFO>());
        Assert.Equal(32, Marshal.SizeOf<WINTRUST_SIGNATURE_SETTINGS>());
    }

    [Fact]
    public void ProviderStructuresLayoutMatchX64()
    {
        Assert.Equal(64, Marshal.SizeOf<CRYPT_PROVIDER_SGNR>());
        Assert.Equal(4, Marshal.OffsetOf<CRYPT_PROVIDER_SGNR>(nameof(CRYPT_PROVIDER_SGNR.sftVerifyAsOf)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<CRYPT_PROVIDER_SGNR>(nameof(CRYPT_PROVIDER_SGNR.pasCounterSigners)).ToInt32());

        Assert.Equal(88, Marshal.SizeOf<CRYPT_PROVIDER_CERT>());
        Assert.Equal(40, Marshal.OffsetOf<CRYPT_PROVIDER_CERT>(nameof(CRYPT_PROVIDER_CERT.dwError)).ToInt32());
    }

    [Fact]
    public void CmsSignerInfoLayoutMatchesX64()
    {
        Assert.Equal(136, Marshal.SizeOf<CMSG_SIGNER_INFO>());
        Assert.Equal(120, Marshal.OffsetOf<CMSG_SIGNER_INFO>(nameof(CMSG_SIGNER_INFO.UnauthAttrs)).ToInt32());
    }
}
