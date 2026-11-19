# CLAUDE.md — Sigwalk 项目说明

## 项目概览

Sigwalk 是一个 Windows 文件数字签名检测工具（C# / WPF / .NET 8），用于批量扫描 EXE、DLL、SYS、MSI 等文件的嵌入式 Authenticode 签名，计算 SHA1/SHA256，并展示证书、时间戳、证书链和多重签名信息。

- 解决方案：`Sigwalk.sln`
- 应用：`src/Sigwalk`（WPF，`net8.0-windows`）
- 核心库：`src/Sigwalk.Core`（无 WPF 依赖，含 P/Invoke / 签名 / 哈希 / 扫描 / 导出）
- 测试：`tests/Sigwalk.Tests`（xUnit，仅引用 Core）
- 一键发布：`scripts/build.bat`

## 环境与常用命令

本机 SDK：`C:\Program Files\dotnet\dotnet.exe`（8.0.428）。部分 shell 的 PATH 不包含 dotnet，使用完整路径：

```powershell
& "C:\Program Files\dotnet\dotnet.exe" restore Sigwalk.sln
& "C:\Program Files\dotnet\dotnet.exe" build Sigwalk.sln -c Release
& "C:\Program Files\dotnet\dotnet.exe" test tests\Sigwalk.Tests\Sigwalk.Tests.csproj -c Release
& "C:\Program Files\dotnet\dotnet.exe" run --project src\Sigwalk\Sigwalk.csproj -c Release
```

发布：

```bat
scripts\build.bat            :: win-x64
scripts\build.bat x86        :: win-x86
set SKIP_TESTS=1             :: 跳过测试
set SINGLE_FILE=0            :: 关闭单文件
set NO_ZIP=1                 :: 不生成 ZIP
```

发布产物：`release\win\x64\Sigwalk.exe`（Self-contained、单文件、未 Trim）。

## 依赖方向与分层

```text
Sigwalk (WPF) ──> Sigwalk.Core
Sigwalk.Tests ──> Sigwalk.Core
```

- **Core 不得引用 WPF / WinForms / `System.Windows`**。Core 通过 `FrameworkReference Microsoft.WindowsDesktop.App` 仅使用 `SignedCms` / `Rfc3161TimestampToken`。
- View 只做展示与视图行为；业务逻辑在 ViewModel 与 Core Service。
- ViewModel 不直接创建 Window；通过 `DialogService` 打开设置/详情窗口。
- 文件扫描、签名验证、哈希计算、导出分别独立实现，避免 UI 直接调用 P/Invoke。

## 关键实现约定

### 签名验证

- 权威结论只来自 `WinVerifyTrust`；不能因为“能读取到证书”就判定签名有效。
- 必须区分 `Unsigned` 与 `Invalid`：无签名码需再用 `CryptQueryObject` 确认是否存在嵌入式 PKCS#7。
- 多处签名：`WSS_GET_SECONDARY_SIG_COUNT` 取数量，`WSS_VERIFY_SPECIFIC` 逐索引验证；任一索引通过 → 文件 `Valid`，其余失败生成警告（与 Windows“至少一个受信签名”语义一致）。
- 状态数据必须在 `WTD_STATEACTION_CLOSE` 前读取并复制为托管对象；`finally` 中确保 CLOSE。
- `FILETIME` 用两个 `uint`；不要使用 `WTD_SAFER_FLAG`；`fdwRevocationChecks = 0`。
- 默认吊销检查为离线（不联网）；链信任仍由系统策略判定，不能忽略链错误。
- 嵌套签名（OID `1.3.6.1.4.1.311.2.4.1`）仅展示，`IsVerified=false`，不参与整体状态聚合。
- **目录签名（Catalog）不在当前范围**；README 与界面已说明。
- 修改 Interop 结构后必须更新并通过 `InteropLayoutTests` 的尺寸/偏移断言。

### 扫描与 UI 线程

- `ScanCoordinator` 使用 `Channel<ScanWorkItem>` + N 个 worker；`PauseTokenSource` 在文件边界暂停；`CancellationToken` 取消。
- 结果通过 `ConcurrentQueue` 交付；UI 的 `DispatcherTimer`（150 ms）批量合并，禁止在后台线程直接修改 `ObservableCollection` / `ICollectionView`。
- 所有集合、`ICollectionView`、`DispatcherTimer` 操作仅在 UI 线程。
- 文件进入扫描前后比对大小/`LastWriteTimeUtc`；变化则 `ScanStatus.Failed`。
- 单文件异常只能影响该文件；`OperationCanceledException` 向上抛出。
- 大量结果更新时使用 `FilesView.DeferRefresh()`；扫描完成后才自动排序。
- 选中项保存在普通 `List` 中（`_selectedFiles`），由 View 的 `SelectionChanged` 同步，避免大选择量下逐项触发 UI 通知。

### 命令与绑定

- 自定义 `RelayCommand` / `AsyncRelayCommand` 的 `CanExecuteChanged` 由 `CommandManager.RequerySuggested` 驱动；状态变化后调用 `CommandManager.InvalidateRequerySuggested()`（见 `Views/Commands.cs`）。
- 对默认 TwoWay 的依赖属性，只读 ViewModel 属性必须显式 `Mode=OneWay`（例如 `ProgressBar.Value`、只读 `TextBox.Text`）。曾因 `ProgressBar.Value` 触发启动异常。
- `ContextMenu` 不在可视树中：在 `Window_Loaded` / `ContextMenuOpening` 中显式设置 `FilesGrid.ContextMenu.DataContext = ViewModel`，MenuItems 命令才能绑定。
- Code-behind 仅允许视图行为：拖放、双击打开、列显隐、右键选中、焦点快捷键、选中同步；不得写扫描/签名业务逻辑。

### 导出

- CSV：UTF-8 BOM、CRLF、RFC 4180 引号转义。
- XLSX：手写最小 OOXML（`ZipArchive` + `XmlWriter` + inlineStr），禁止引入第三方 Excel 库；清洗 XML 非法字符、限制单元格长度。
- JSON：`schemaVersion` + 枚举字符串 + ISO 时间；`ResultSerializer` 同时用于导出、保存、加载。
- 不将 SHA1/SHA256 合并为一个字段。

### 资源与异常

- 文件流、`IncrementalHash`、`X509Certificate2`、`IncrementalHash`、原生 state data 必须释放。
- 不吞异常：单文件失败写入错误信息与日志；UI 全局异常写入 `%APPDATA%\Sigwalk\logs`。
- 日志不得影响主流程；`FileLogService` 内部吞掉写入失败。

## 测试

- `dotnet test` 必须全部通过；当前 70 个测试。
- 不把 WPF 引入测试项目；过滤、扫描、导出、签名映射都在 Core 测试。
- 真实签名样本测试必须优雅跳过（文件不存在或环境不满足时直接返回并输出 SKIP）。
- 篡改测试：复制已签名 PE，修改 `.text` 段后应得到 `Invalid`。
- 嵌套签名测试用自签证书构造 PKCS#7，不依赖外部签名文件。

## 发布检查

1. `scripts\build.bat` 退出码为 0。
2. `release\win\x64\Sigwalk.exe` 存在且目录中只有单文件（另有 ZIP 在 `release\`）。
3. 启动发布 exe，确认窗口出现、可响应、可关闭；检查日志无异常。
4. 不得删除用户数据；清理只针对 `release\win\<arch>`。
5. 文档/界面必须明确 Catalog、吊销默认策略、取消边界等限制。

## 文档

- 用户文档：`README.md`
- 架构/审查/构建/签名专家代理：`.claude/agents/`
- 构建/测试/发布流程：`.claude/skills/`
- 编码/UI/安全规则：`.claude/rules/`
