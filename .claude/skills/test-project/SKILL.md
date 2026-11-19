---
name: test-project
description: 运行 Sigwalk 的 xUnit 测试并解读签名/扫描/导出测试结果
---

# 测试 Sigwalk

## 运行

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test "d:\Projects\tools\Sigwalk\tests\Sigwalk.Tests\Sigwalk.Tests.csproj" -c Release
```

按名称过滤：

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Sigwalk.Tests\Sigwalk.Tests.csproj -c Release --filter "FullyQualifiedName~HashServiceTests"
```

## 测试约定

- 测试项目只引用 `Sigwalk.Core`，**不要引入 WPF**，也不设置 `UseWPF`。
- 临时文件使用 `TestHelpers/TempDirectory`，测试结束自动清理。
- 真实签名样本：
  - `dotnet.exe` 不存在或不是嵌入签名时，测试输出 SKIP 后直接返回，不能失败。
  - 篡改测试复制已签名 PE 并翻转 `.text` 字节，期望 `Invalid`。
  - 目录签名文件（如 notepad.exe 目录签名）不能断言为 Invalid。
- 嵌套签名测试使用 `CertificateRequest.CreateSelfSigned` 构造 PKCS#7，不依赖外部文件。
- 布局测试：x64 结构尺寸/偏移必须保持（88/32/32/64/88/136 等）。
- 扫描协调器测试注入 `FakeProcessor`，不要依赖真实磁盘延迟。

## 新增测试

- 签名错误码映射：`SignatureErrorMapperTests`。
- 导出：检查 CSV BOM/引号、JSON 往返、XLSX zip 部件与 XML 良构。
- 过滤/目录枚举/设置：纯 Core 逻辑。
- 修改 P/Invoke 结构或签名聚合逻辑时，必须补充/更新对应测试。

## 完成标准

- 0 失败。允许个别依赖系统样本的测试输出 SKIP，但测试本身必须“通过”。
- 不修改真实系统文件；只读访问。
