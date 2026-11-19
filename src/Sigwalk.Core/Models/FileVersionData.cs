namespace Sigwalk.Core.Models;

/// <summary>PE 文件版本资源信息（非 PE 文件可能为空）。</summary>
public sealed class FileVersionData
{
    public string CompanyName { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string FileDescription { get; init; } = string.Empty;
    public string FileVersion { get; init; } = string.Empty;
    public string ProductVersion { get; init; } = string.Empty;
    public string OriginalFilename { get; init; } = string.Empty;
    public string InternalName { get; init; } = string.Empty;
    public string Copyright { get; init; } = string.Empty;
    public string Comments { get; init; } = string.Empty;
}
