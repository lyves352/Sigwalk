---
name: build-project
description: 编译 Sigwalk 解决方案并修复常见 C# / WPF 编译错误
---

# 构建 Sigwalk

## 命令

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet restore "d:\Projects\tools\Sigwalk\Sigwalk.sln"
& $dotnet build "d:\Projects\tools\Sigwalk\Sigwalk.sln" -c Release --no-restore
```

若 PATH 中有 dotnet，可直接使用 `dotnet`。不要假设 PATH 一定包含 SDK。

## 常见问题

1. **`File` / `Directory` / `Path` 不存在**
   - WPF 项目的隐式 using 不包含 `System.IO`，需要显式 `using System.IO;`。
2. **只读属性 TwoWay 绑定异常**
   - `ProgressBar.Value`、`TextBox.Text` 默认 TwoWay；绑定只读 ViewModel 属性时必须显式 `Mode=OneWay`。
3. **XAML 找不到资源**
   - 确认 `Light.xaml` 与 `Dark.xaml` 定义了相同键；`Styles.xaml` 在 `App.xaml` 中合并。
   - `DataGridColumnHeaderBaseStyle`、`CaptionTextStyle`、`AccentButtonStyle` 等位于 `Resources/Styles.xaml`。
4. **自定义 ICommand 按钮一直禁用**
   - `RelayCommand` / `AsyncRelayCommand` 的 `CanExecuteChanged` 由 `CommandManager.RequerySuggested` 驱动；状态变化后调用 `CommandManager.InvalidateRequerySuggested()`。
5. **Interop 编译或运行异常**
   - 检查 `unsafe` 是否启用（`AllowUnsafeBlocks`）。
   - 修改结构体后必须运行布局测试。
6. **Core 引用了 WPF**
   - 这是分层违规；Core 只允许 `FrameworkReference Microsoft.WindowsDesktop.App` 以使用 `SignedCms`。
7. **XAML 事件处理器缺失**
   - 检查 `x:Class`、`Click`/`SelectionChanged` 等事件在 code-behind 中存在且签名正确。

## 完成标准

- Release 编译 0 错误；尽量 0 警告。
- 运行相关单元测试。
- 不修改 `release/` 之外的构建产物。
