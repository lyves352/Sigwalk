# WPF / UI 规则（Sigwalk）

## MVVM 边界

- View 不直接调用签名、哈希、扫描、导出服务；通过 ViewModel / Command。
- ViewModel 不直接 `new Window`；使用 `DialogService.ShowSettings` / `ShowFileDetail`。
- Code-behind 只允许纯视图行为：
  - 拖放、双击打开；
  - 选中项同步；
  - 列显隐；
  - 右键行选中；
  - Ctrl+F 聚焦；
  - ContextMenu DataContext 设置。
- 不得在 code-behind 写扫描/签名/导出业务逻辑。

## 绑定

- 只读属性绑定到默认 TwoWay 的依赖属性时必须 `Mode=OneWay`：
  - `ProgressBar.Value`
  - 只读 `TextBox.Text`
- 枚举显示统一通过 `StatusText` 转换，不在 XAML 重复硬编码中文。
- 主题资源使用 `DynamicResource`；`Light.xaml` 与 `Dark.xaml` 必须定义相同键。
- `ContextMenu` 不在可视树中，必须显式设置 `DataContext`（`Window_Loaded` / `ContextMenuOpening`）。

## 命令

- 自定义 `ICommand` 的 `CanExecuteChanged` 由 `CommandManager.RequerySuggested` 驱动。
- 任何影响 CanExecute 的状态变化后调用 `CommandManager.InvalidateRequerySuggested()`：
  - 文件列表变化；
  - 选中项变化；
  - `IsScanning` / `IsPaused` 变化；
  - 扫描结束。

## 线程

- 所有 `ObservableCollection`、`ICollectionView`、`DispatcherTimer`、Window 操作只在 UI 线程。
- 后台 worker 只写入并发队列；UI 用 150 ms `DispatcherTimer` 批量合并。
- 不在后台线程触发 `PropertyChanged`（除非确认绑定引擎会调度）。
- 大选择量使用普通 `List` 存储选中项，避免逐项集合通知。

## DataGrid 性能

- 开启行虚拟化与 `Recycling`。
- 扫描结果分批应用；使用 `DeferRefresh()` 包裹批量更新。
- 过滤/搜索使用 300 ms 防抖；无过滤时不要每个批次刷新视图。
- 扫描中不要频繁重新排序；完成后按设置自动排序。
- 列宽固定或 `*` 需谨慎：固定列过多时不要给路径列使用 `*`，否则会被压缩。当前路径列固定 320，并冻结前 3 列。

## 主题与可读性

- 只对签名状态使用轻量颜色（有效绿、无效红、未知黄）。
- 深色主题下保持背景、前景、边框、选中色一致。
- 不得在控件模板中硬编码浅色背景，使用主题画刷。

## 窗口

- 默认窗口尺寸需适配 1080p + 125% 缩放（当前 1500×820 DIP）。
- 文件详情/设置窗口居中于 Owner，`ShowInTaskbar=False`。
- 长路径/长证书字段使用 `TextTrimming` + ToolTip 或换行。
