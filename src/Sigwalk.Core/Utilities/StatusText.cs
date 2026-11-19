using Sigwalk.Core.Models;

namespace Sigwalk.Core.Utilities;

/// <summary>枚举值到中文显示文本的集中转换。</summary>
public static class StatusText
{
    public static string DescribeSignatureStatus(SignatureStatus status) => status switch
    {
        SignatureStatus.Valid => "签名有效",
        SignatureStatus.Unsigned => "未签名",
        SignatureStatus.Invalid => "签名无效",
        _ => "未知",
    };

    public static string DescribeScanStatus(ScanStatus status) => status switch
    {
        ScanStatus.Pending => "待扫描",
        ScanStatus.Scanning => "扫描中",
        ScanStatus.Completed => "已完成",
        ScanStatus.Failed => "失败",
        _ => "未知",
    };

    public static string DescribeSignatureFilter(SignatureFilter filter) => filter switch
    {
        SignatureFilter.All => "全部文件",
        SignatureFilter.Valid => "签名成功",
        SignatureFilter.Unsigned => "未签名",
        SignatureFilter.Invalid => "签名无效",
        SignatureFilter.Unknown => "未知状态",
        SignatureFilter.ScanFailed => "扫描失败",
        _ => "全部文件",
    };

    public static string DescribeScanMode(ScanMode mode) => mode switch
    {
        ScanMode.Full => "哈希 + 签名",
        ScanMode.SignatureOnly => "仅签名",
        ScanMode.HashOnly => "仅哈希",
        _ => "哈希 + 签名",
    };

    public static string DescribeRevocationMode(RevocationMode mode) => mode switch
    {
        RevocationMode.None => "不检查吊销（离线）",
        RevocationMode.CacheOnly => "缓存吊销信息",
        RevocationMode.Online => "在线检查吊销",
        _ => "不检查吊销（离线）",
    };

    public static string DescribeRevocationStatus(RevocationStatus status) => status switch
    {
        RevocationStatus.NotChecked => "未检查",
        RevocationStatus.Good => "正常",
        RevocationStatus.Unavailable => "无法获取",
        RevocationStatus.Revoked => "已吊销",
        _ => "未知",
    };

    public static string DescribeThemeMode(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => "深色",
        _ => "浅色",
    };
}
