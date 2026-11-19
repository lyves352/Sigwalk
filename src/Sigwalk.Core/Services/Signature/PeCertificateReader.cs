using System.Reflection.PortableExecutable;
using Sigwalk.Core.Interop;

namespace Sigwalk.Core.Services.Signature;

/// <summary>读取 PE 安全目录中的 WIN_CERTIFICATE，提取 PKCS#7 数据。</summary>
internal static class PeCertificateReader
{
    public static List<byte[]> ReadSignedDataBlobs(string filePath)
    {
        var result = new List<byte[]>();

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var peHeader = peReader.PEHeaders.PEHeader;
            if (peHeader is null)
            {
                return result;
            }

            var directory = peHeader.CertificateTableDirectory;
            if (directory.RelativeVirtualAddress <= 0 || directory.Size <= 0)
            {
                return result;
            }

            // 证书表数据目录记录的是文件偏移（不是 RVA）。
            if (directory.RelativeVirtualAddress + (long)directory.Size > stream.Length)
            {
                return result;
            }

            stream.Position = directory.RelativeVirtualAddress;
            var buffer = new byte[directory.Size];
            stream.ReadExactly(buffer);

            var offset = 0;
            while (offset + 8 <= buffer.Length && result.Count < 16)
            {
                var length = BitConverter.ToUInt32(buffer, offset);
                var certificateType = BitConverter.ToUInt16(buffer, offset + 6);
                if (length < 8 || offset + (long)length > buffer.Length)
                {
                    break;
                }

                if (certificateType == WinTrustConstants.WIN_CERT_TYPE_PKCS_SIGNED_DATA)
                {
                    var data = new byte[length - 8];
                    Buffer.BlockCopy(buffer, offset + 8, data, 0, (int)(length - 8));
                    result.Add(data);
                }

                var step = (int)((length + 7) & ~7u);
                if (step <= 0)
                {
                    break;
                }

                offset += step;
            }
        }
        catch (BadImageFormatException)
        {
            // 非 PE 文件没有安全目录。
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return result;
    }
}
