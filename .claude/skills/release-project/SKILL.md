---
name: release-project
description: 一键发布 Sigwalk（Self-contained 单文件）并验证发布产物
---

# 发布 Sigwalk

## 一键发布

```bat
scripts\build.bat
```

可选：

```bat
scripts\build.bat x86
set SKIP_TESTS=1
set SINGLE_FILE=0
set NO_ZIP=1
```

## 脚本行为

1. 查找 dotnet（PATH → `%ProgramFiles%\dotnet\dotnet.exe` → `%ProgramFiles(x86)%`）。
2. 检查 SDK 主版本 ≥ 8。
3. restore → Release build → test。
4. 只清理 `release\win\<arch>`。
5. `dotnet publish`：
   - `-r win-x64` / `win-x86`
   - `--self-contained true`
   - `PublishSingleFile=true`
   - `IncludeNativeLibrariesForSelfExtract=true`
   - `EnableCompressionInSingleFile=true`
   - `PublishTrimmed=false`（WPF 不支持裁剪）
   - `DebugType=None`
6. 校验 `Sigwalk.exe`，输出版本与大小。
7. 默认生成 `release\Sigwalk-win-<arch>-<version>.zip`。

## 产物检查

```text
release\
├── Sigwalk-win-x64-1.0.0.zip
└── win\
    └── x64\
        └── Sigwalk.exe
```

- 单文件模式下 `release\win\x64` 应只有 `Sigwalk.exe`。
- 启动 exe，确认窗口出现、进程 Responding、可正常关闭。
- 检查 `%APPDATA%\Sigwalk\logs\sigwalk-*.log` 是否有未处理异常。

## 安全与清理

- 不得删除用户配置、日志、源码或其他目录。
- 发布失败必须返回非零退出码并输出明确错误。
- 不修改 `%APPDATA%\Sigwalk\settings.json`；若为测试修改，结束后恢复。

## 已知发布限制

- 单文件首次启动会解压原生库，启动稍慢。
- 不能启用 Trim。
- Catalog 签名不支持；扫描结果与 `Get-AuthenticodeSignature` 可能因此不同。
