namespace Sigwalk.Core.Models;

/// <summary>文件数字签名状态。</summary>
public enum SignatureStatus
{
    /// <summary>无法确定签名状态（验证异常等）。</summary>
    Unknown = 0,

    /// <summary>签名存在且通过系统信任策略验证。</summary>
    Valid = 1,

    /// <summary>文件没有嵌入式数字签名。</summary>
    Unsigned = 2,

    /// <summary>存在签名，但验证失败（篡改、证书不受信、过期、吊销等）。</summary>
    Invalid = 3,
}

/// <summary>单个文件的扫描阶段状态。</summary>
public enum ScanStatus
{
    Pending = 0,
    Scanning = 1,
    Completed = 2,
    Failed = 3,
}

/// <summary>扫描模式。</summary>
public enum ScanMode
{
    /// <summary>同时计算哈希并验证签名。</summary>
    Full = 0,

    /// <summary>仅验证签名。</summary>
    SignatureOnly = 1,

    /// <summary>仅计算哈希。</summary>
    HashOnly = 2,
}

/// <summary>证书吊销检查模式。</summary>
public enum RevocationMode
{
    /// <summary>不检查吊销（默认，不联网）。</summary>
    None = 0,

    /// <summary>只使用本地缓存的吊销信息。</summary>
    CacheOnly = 1,

    /// <summary>允许联网检查吊销（可能较慢）。</summary>
    Online = 2,
}

/// <summary>证书吊销状态。</summary>
public enum RevocationStatus
{
    NotChecked = 0,
    Good = 1,
    Unavailable = 2,
    Revoked = 3,
    Unknown = 4,
}

/// <summary>列表过滤条件中的签名状态筛选。</summary>
public enum SignatureFilter
{
    All = 0,
    Valid = 1,
    Unsigned = 2,
    Invalid = 3,
    Unknown = 4,
    ScanFailed = 5,
}

/// <summary>界面主题。</summary>
public enum ThemeMode
{
    Light = 0,
    Dark = 1,

    /// <summary>跟随操作系统主题（默认）。</summary>
    Auto = 2,
}

/// <summary>界面语言。</summary>
public enum AppLanguage
{
    Chinese = 0,
    English = 1,
}

/// <summary>导出文件格式。</summary>
public enum ExportFormat
{
    Csv = 0,
    Xlsx = 1,
    Json = 2,
}

/// <summary>导出内容范围。</summary>
public enum ExportContentScope
{
    /// <summary>全部结果（含签名有效文件）。</summary>
    All = 0,

    /// <summary>仅需关注的文件（未签名、无效、未知、失败）。</summary>
    Compact = 1,
}

/// <summary>导出明细分组。</summary>
public enum ExportGroupKind
{
    Valid,
    Unsigned,
    Invalid,
    Unknown,
    Failed,
}

/// <summary>导出概况中的总体结论。</summary>
public enum ExportVerdict
{
    Ok,
    IssuesFound,
}
