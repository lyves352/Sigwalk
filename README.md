# Sigwalk

Sigwalk 是一个 Windows 文件数字签名检测工具，用于批量检查 EXE、DLL、SYS、MSI 等文件的 Authenticode 数字签名，并计算 SHA1 / SHA256 哈希值。

![主界面（浅色）](docs/screenshots/main-light.png)

## 功能特性

- 支持单文件、单目录、多目录、拖放导入，以及 Ctrl+V 粘贴多行路径。
- 可配置递归扫描、文件类型过滤和并发数。
- 基于 Windows `WinVerifyTrust` 验证嵌入式 Authenticode 签名，区分 **签名有效 / 未签名 / 签名无效 / 未知**。
- 展示签名者、证书颁发者、序列号、指纹、有效期、签名算法、时间戳签名者与时间、证书链状态。
- 支持多重签名与嵌套签名展示；任一签名通过系统信任即判定文件有效，并提示部分签名无效。
- 单次流式读取同时计算 SHA1 和 SHA256，结果分别展示，统一大写十六进制。
- DataGrid 支持排序、列宽调整、列显隐、复制、双击打开、右键菜单与资源管理器定位。
- 过滤栏支持签名状态、扩展名、文件名/路径/签名者/SHA1/SHA256 搜索，多条件组合。
- 扫描支持开始、暂停、恢复、取消、重新检测、动态追加；扫描中 UI 保持响应。
- 导出 CSV / Excel（XLSX）/ JSON；支持保存和加载扫描结果（JSON）。
- 文件详情窗口展示完整证书、时间戳、证书链、PE 版本资源和哈希。
- 浅色/深色主题、快捷键、配置记忆、日志记录。

![主界面（深色）](docs/screenshots/main-dark.png)

## 环境要求

- Windows 10 / Windows 11（x64；脚本也支持 x86 发布）
- 开发与构建：.NET SDK 8.0 或更高版本
- 运行发布产物：无需安装 .NET Runtime（Self-contained 单文件发布）

## 快速开始

```bat
cd Sigwalk
scripts\build.bat
release\win\x64\Sigwalk.exe
```

也可以直接使用 SDK 运行：

```powershell
& "C:\Program Files\dotnet\dotnet.exe" run --project src\Sigwalk\Sigwalk.csproj -c Release
```

> 本机环境中的 `dotnet` 可能不在 PATH 中；脚本会自动回退到 `%ProgramFiles%\dotnet\dotnet.exe`。

## 使用说明

### 导入文件

- 菜单：文件 → 打开文件 / 打开目录 / 添加文件 / 添加目录。
- 工具栏提供同样的快捷入口。
- 从资源管理器拖放文件或目录到窗口。
- 复制路径后按 **Ctrl+V**（每行一个路径，支持空格和引号）。
- “打开”会清空当前列表后导入，“添加”会追加；重复路径会自动忽略。

### 扫描

- **开始扫描（F5）**：扫描所有待扫描或上次失败的文件。
- **暂停（F6）/ 继续**：暂停后当前文件会完成，新的文件不再开始。
- **取消（Esc）**：在文件边界生效；`WinVerifyTrust` 本身不可中断，正在验证的文件会先返回。
- **重新检测选中**：对选中行重新执行当前扫描配置。
- 扫描过程中可以继续添加文件，新文件会自动加入当前扫描队列。
- 扫描结果分批合并到表格，避免大量文件时界面卡顿。

### 过滤与搜索

- 签名状态：全部文件 / 签名成功 / 未签名 / 签名无效 / 未知状态 / 扫描失败。
- 扩展名：例如 `exe;dll;sys`，留空表示全部。
- 搜索：同时匹配文件名、完整路径、签名者、证书颁发者、SHA1、SHA256。
- 多个条件为 AND 关系；清除过滤不会修改原始结果。

### 右键菜单

- 打开文件、打开文件所在目录、在资源管理器中定位（`explorer /select,`）。
- 复制选中行、路径、名称、SHA1、SHA256、签名信息。
- 查看完整证书信息、重新检测选中、移除选中。
- 列标题右键可显示/隐藏列。

### 详情窗口

选择文件后点击工具栏“查看详情”或右键“查看完整证书信息”：

- **基本信息**：路径、大小、类型、时间、扫描/签名状态、PE 版本资源。
- **签名信息**：每个签名的证书全部字段、时间戳、证书链、验证方式和错误原因。
- **哈希**：SHA1 / SHA256 完整值及复制按钮。

### 导出与保存

- 文件 → 导出全部结果 / 导出当前过滤结果：支持 CSV、XLSX、JSON。
- 文件 → 保存结果：保存为 JSON；加载结果可恢复查看，无需重新扫描。
- CSV 使用 UTF-8 BOM + CRLF，便于 Excel 打开；XLSX 为原生 OOXML，无第三方依赖。

### 快捷键

| 快捷键 | 功能 |
|---|---|
| Ctrl+O | 打开文件 |
| Ctrl+Shift+O | 打开目录 |
| Ctrl+V | 粘贴路径 |
| F5 | 开始扫描 |
| F6 | 暂停/继续 |
| Esc | 取消扫描 |
| Ctrl+F | 聚焦搜索框 |
| Delete | 移除选中 |
| Ctrl+C | 复制选中行 |
| Ctrl+S | 保存结果 |

### 配置与日志

- 配置：`%APPDATA%\Sigwalk\settings.json`
- 日志：`%APPDATA%\Sigwalk\logs\sigwalk-yyyyMMdd.log`
- 菜单“帮助 → 打开日志目录”可直接打开日志文件夹。

## 签名状态说明

| 状态 | 含义 |
|---|---|
| 签名有效（Valid） | 存在嵌入式签名，且 WinVerifyTrust 使用系统信任策略验证成功 |
| 未签名（Unsigned） | 未检测到嵌入式 Authenticode 签名 |
| 签名无效（Invalid） | 存在签名，但摘要不匹配、证书过期/不受信/被吊销/用途错误等 |
| 未知（Unknown） | 验证过程发生异常或无法归类，无法确定状态 |

扫描状态独立于签名状态：

| 状态 | 含义 |
|---|---|
| 待扫描（Pending） | 已加入列表，尚未扫描 |
| 扫描中（Scanning） | 正在队列中或正在处理 |
| 已完成（Completed） | 扫描完成（签名状态见上表） |
| 失败（Failed） | 文件不存在、无权限、被占用、读取失败、扫描期间变化等 |

## 扫描原理

1. 使用 `WinVerifyTrust(WINTRUST_ACTION_GENERIC_VERIFY_V2, WTD_CHOICE_FILE)` 获取系统信任策略下的权威结果。
2. 使用 `WINTRUST_SIGNATURE_SETTINGS` 的 `WSS_GET_SECONDARY_SIG_COUNT` / `WSS_VERIFY_SPECIFIC` 逐个验证主签名和附加签名。
3. 在关闭 state data 之前，通过 `WTHelperProvDataFromStateData` / `WTHelperGetProvSignerFromChain` 复制签名者证书、证书链、时间戳签名者和时间信息。
4. 通过 `CryptQueryObject` 探测嵌入式 PKCS#7 签名，用于区分“未签名”和“签名存在但验证失败”。
5. 通过 PE 安全目录和 `SignedCms` 尽力解析嵌套签名（OID `1.3.6.1.4.1.311.2.4.1`），仅用于展示，不替代 WinVerifyTrust 结论。
6. 哈希使用 `IncrementalHash` 单次流式读取同时计算 SHA1 / SHA256，1 MB 缓冲区，不会把整个文件加载到内存。
7. 扫描前后比对文件大小和最后修改时间，文件变化时丢弃结果并标记失败。

### 吊销检查

设置 → 证书吊销检查提供三种模式：

- **不检查吊销（默认，离线）**：不联网，速度快；吊销状态显示“未检查”。
- **缓存吊销信息**：仅使用本地缓存的 CRL/OCSP。
- **在线检查吊销**：允许网络访问，可能明显变慢；建议降低并发数。

链信任始终由系统策略判断，不会忽略证书链错误。吊销信息暂时不可用时会显示“签名有效 + 吊销状态未知”的警告，而不是直接判定签名无效。

## 项目结构

```text
Sigwalk/
├── CLAUDE.md                 # Claude Code 项目说明
├── README.md
├── Sigwalk.sln
├── global.json
├── Directory.Build.props
├── .claude/                  # agents / skills / rules
├── src/
│   ├── Sigwalk.Core/         # 模型、签名验证、哈希、扫描、导出、配置（无 WPF 依赖）
│   │   ├── Models/
│   │   ├── Interop/          # WinTrust / Crypt32 P/Invoke
│   │   ├── Services/
│   │   │   ├── Signature/    # Authenticode、WTHelper、嵌套签名
│   │   │   ├── Scanning/     # ScanCoordinator、PauseToken
│   │   │   └── Export/       # CSV / XLSX / JSON
│   │   ├── Logging/
│   │   └── Utilities/
│   └── Sigwalk/              # WPF 应用（MVVM）
│       ├── Views/
│       ├── ViewModels/
│       ├── Services/
│       ├── Converters/
│       ├── Helpers/
│       └── Resources/Themes/
├── tests/Sigwalk.Tests/      # xUnit，仅引用 Core
├── scripts/build.bat         # 一键构建与发布
└── docs/screenshots/
```

## 构建与发布

一键发布：

```bat
scripts\build.bat
```

脚本会依次执行：

1. 检查 .NET SDK（PATH 或默认安装目录）。
2. `dotnet restore`。
3. Release 编译。
4. 运行单元测试。
5. 清理 `release\win\x64`（仅发布目录）。
6. Self-contained、单文件、win-x64 发布。
7. 校验 `Sigwalk.exe`，生成 `release\Sigwalk-win-x64-<版本>.zip`。

可选参数/环境变量：

| 用法 | 说明 |
|---|---|
| `scripts\build.bat x86` | 发布 win-x86 |
| `set SKIP_TESTS=1` | 跳过测试 |
| `set SINGLE_FILE=0` | 关闭单文件发布 |
| `set NO_ZIP=1` | 不生成 ZIP |

输出：

```text
release/
├── Sigwalk-win-x64-1.0.0.zip
└── win/
    └── x64/
        └── Sigwalk.exe
```

发布参数包含 `PublishSingleFile`、`IncludeNativeLibrariesForSelfExtract`、`EnableCompressionInSingleFile`，并禁用 Trim（WPF 不支持裁剪）。

## 测试

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Sigwalk.Tests\Sigwalk.Tests.csproj -c Release
```

当前测试覆盖：

- P/Invoke 结构体大小与关键偏移（x64）。
- SHA1/SHA256 已知向量、空文件、大文件、取消。
- 目录递归/非递归、扩展名过滤、去重、粘贴路径解析。
- 过滤/搜索组合、配置读写、日志。
- CSV / JSON / XLSX 结构与往返。
- 暂停门闩、扫描协调器（并发、暂停、取消、动态追加、单文件失败）。
- 签名错误码映射、嵌套签名递归解析（自签证书构造）。
- 真实签名冒烟测试：未签名文件 → Unsigned；`dotnet.exe` → Valid + 证书/时间戳；篡改副本 → Invalid。系统样本不存在时优雅跳过。

## 已知限制

1. **仅检测嵌入式 Authenticode 签名，不检测 Windows 目录签名（Catalog）。** 部分仅由系统目录签名的文件会显示为“未签名”。这是与 `Get-AuthenticodeSignature` 结果可能不同的主要原因。
2. 嵌套签名仅作展示，不单独参与文件整体状态判定；主结论以 WinVerifyTrust 对各签名索引的验证结果为准。
3. `WinVerifyTrust` 无法中断，取消扫描在文件边界生效。
4. 默认不联网检查吊销；需要严格吊销校验时请在设置中切换为在线模式。
5. 单文件发布首次启动需要解压原生库，启动时间可能略长，部分杀毒软件可能对自解压行为敏感。
6. 当前版本以 Windows x64 为主要目标；x86 发布可用但未作为默认验证目标。

## 常见问题

**为什么系统文件显示未签名？**

它们可能只有 Catalog 目录签名，而本版本只验证嵌入式签名。可使用 `Get-AuthenticodeSignature` 对照；若返回 `CatalogSigned`，即属于该情况。

**为什么扫描某些文件很慢？**

在线吊销检查、网络证书链下载或大量小文件都会影响速度。可切换为默认离线模式，或调整并发数（设置 → 并发数）。

**扫描被占用/无权限的文件会怎样？**

该文件标记为“失败”并记录错误，其他文件继续扫描。详细原因可在详情窗口、状态列提示和日志中查看。

**为什么暂停后还有文件在跑？**

暂停只在文件边界生效；正在验证签名的文件会先完成，随后不再开始新文件。
