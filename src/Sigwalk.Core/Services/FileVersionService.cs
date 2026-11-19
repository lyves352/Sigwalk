using System.Diagnostics;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services;

/// <summary>读取 PE 文件的版本资源信息。</summary>
public sealed class FileVersionService
{
    public FileVersionData? Read(string filePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(filePath);
            if (string.IsNullOrEmpty(info.CompanyName) &&
                string.IsNullOrEmpty(info.ProductName) &&
                string.IsNullOrEmpty(info.FileVersion) &&
                string.IsNullOrEmpty(info.ProductVersion) &&
                string.IsNullOrEmpty(info.FileDescription))
            {
                return null;
            }

            return new FileVersionData
            {
                CompanyName = info.CompanyName ?? string.Empty,
                ProductName = info.ProductName ?? string.Empty,
                FileDescription = info.FileDescription ?? string.Empty,
                FileVersion = info.FileVersion ?? string.Empty,
                ProductVersion = info.ProductVersion ?? string.Empty,
                OriginalFilename = info.OriginalFilename ?? string.Empty,
                InternalName = info.InternalName ?? string.Empty,
                Copyright = info.LegalCopyright ?? string.Empty,
                Comments = info.Comments ?? string.Empty,
            };
        }
        catch
        {
            // 版本资源不是扫描的关键路径，读取失败时静默返回空。
            return null;
        }
    }
}
