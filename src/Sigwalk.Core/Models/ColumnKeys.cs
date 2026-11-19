namespace Sigwalk.Core.Models;

/// <summary>表格列的稳定标识，用于持久化列显隐设置。</summary>
public static class ColumnKeys
{
    public const string Index = "Index";
    public const string FileName = "FileName";
    public const string FilePath = "FilePath";
    public const string Size = "Size";
    public const string FileType = "FileType";
    public const string LastWriteTime = "LastWriteTime";
    public const string SignatureStatus = "SignatureStatus";
    public const string Signer = "Signer";
    public const string Issuer = "Issuer";
    public const string Validity = "Validity";
    public const string Sha1 = "Sha1";
    public const string Sha256 = "Sha256";
    public const string ScanStatus = "ScanStatus";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Index,
        FileName,
        FilePath,
        Size,
        FileType,
        LastWriteTime,
        SignatureStatus,
        Signer,
        Issuer,
        Validity,
        Sha1,
        Sha256,
        ScanStatus,
    };
}
