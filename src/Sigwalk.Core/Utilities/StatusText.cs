using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Utilities;

/// <summary>枚举值到当前语言显示文本的集中转换。</summary>
public static class StatusText
{
    public static string DescribeSignatureStatus(SignatureStatus status) => status switch
    {
        SignatureStatus.Valid => Loc.T("Enum.Signature.Valid"),
        SignatureStatus.Unsigned => Loc.T("Enum.Signature.Unsigned"),
        SignatureStatus.Invalid => Loc.T("Enum.Signature.Invalid"),
        _ => Loc.T("Enum.Signature.Unknown"),
    };

    public static string DescribeScanStatus(ScanStatus status) => status switch
    {
        ScanStatus.Pending => Loc.T("Enum.Scan.Pending"),
        ScanStatus.Scanning => Loc.T("Enum.Scan.Scanning"),
        ScanStatus.Completed => Loc.T("Enum.Scan.Completed"),
        ScanStatus.Failed => Loc.T("Enum.Scan.Failed"),
        _ => Loc.T("Common.Unknown"),
    };

    public static string DescribeSignatureFilter(SignatureFilter filter) => filter switch
    {
        SignatureFilter.All => Loc.T("Enum.Filter.All"),
        SignatureFilter.Valid => Loc.T("Enum.Filter.Valid"),
        SignatureFilter.Unsigned => Loc.T("Enum.Filter.Unsigned"),
        SignatureFilter.Invalid => Loc.T("Enum.Filter.Invalid"),
        SignatureFilter.Unknown => Loc.T("Enum.Filter.Unknown"),
        SignatureFilter.ScanFailed => Loc.T("Enum.Filter.ScanFailed"),
        _ => Loc.T("Enum.Filter.All"),
    };

    public static string DescribeScanMode(ScanMode mode) => mode switch
    {
        ScanMode.Full => Loc.T("Enum.Mode.Full"),
        ScanMode.SignatureOnly => Loc.T("Enum.Mode.SignatureOnly"),
        ScanMode.HashOnly => Loc.T("Enum.Mode.HashOnly"),
        _ => Loc.T("Enum.Mode.Full"),
    };

    public static string DescribeRevocationMode(RevocationMode mode) => mode switch
    {
        RevocationMode.None => Loc.T("Enum.Revocation.None"),
        RevocationMode.CacheOnly => Loc.T("Enum.Revocation.CacheOnly"),
        RevocationMode.Online => Loc.T("Enum.Revocation.Online"),
        _ => Loc.T("Enum.Revocation.None"),
    };

    public static string DescribeRevocationStatus(RevocationStatus status) => status switch
    {
        RevocationStatus.NotChecked => Loc.T("Enum.RevocationStatus.NotChecked"),
        RevocationStatus.Good => Loc.T("Enum.RevocationStatus.Good"),
        RevocationStatus.Unavailable => Loc.T("Enum.RevocationStatus.Unavailable"),
        RevocationStatus.Revoked => Loc.T("Enum.RevocationStatus.Revoked"),
        _ => Loc.T("Enum.RevocationStatus.Unknown"),
    };

    public static string DescribeThemeMode(ThemeMode mode) => mode switch
    {
        ThemeMode.Auto => Loc.T("Enum.Theme.Auto"),
        ThemeMode.Dark => Loc.T("Enum.Theme.Dark"),
        _ => Loc.T("Enum.Theme.Light"),
    };

    public static string DescribeLanguage(AppLanguage language) => language switch
    {
        AppLanguage.English => Loc.T("Enum.Language.English"),
        _ => Loc.T("Enum.Language.Chinese"),
    };

    public static string DescribeExportFormat(ExportFormat format) => format switch
    {
        ExportFormat.Json => Loc.T("Enum.ExportFormat.Json"),
        ExportFormat.Xlsx => Loc.T("Enum.ExportFormat.Xlsx"),
        _ => Loc.T("Enum.ExportFormat.Csv"),
    };

    public static string DescribeExportContent(ExportContentScope scope) => scope switch
    {
        ExportContentScope.All => Loc.T("Enum.ExportContent.All"),
        _ => Loc.T("Enum.ExportContent.Compact"),
    };
}
