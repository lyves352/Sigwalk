# C# 编码规则（Sigwalk）

## 命名与结构

- 类型、方法、属性使用 PascalCase；参数/局部变量 camelCase；私有字段 `_camelCase`。
- 使用文件作用域命名空间；每个文件职责单一。
- 优先使用 `sealed`；不可变结果模型使用 `init` 属性。
- 可空引用类型开启；公共 API 明确标注可空性。

## 注释

- 关键业务逻辑、复杂流程、P/Invoke 结构、状态映射必须写中文注释。
- 注释解释“为什么/边界/风险”，不要重复代码表面含义。

## 异步

- I/O 使用 async/await；不要 `.Result` / `.Wait()`。
- Core 中的库代码使用 `ConfigureAwait(false)`；UI 层的 `await` 保持 UI 上下文。
- 传递 `CancellationToken`；可取消点放在文件边界、每个索引、每次读取。
- `async void` 仅限事件处理器，并在内部捕获异常。

## 异常

- 不允许空 catch；至少记录日志或转换为明确的失败结果。
- 单文件扫描异常不得中断整个任务。
- `OperationCanceledException` 向上传播，不要包装成普通失败。
- 扫描失败使用 `ScanStatus.Failed`；签名无法确定才使用 `SignatureStatus.Unknown`。

## 资源

- `FileStream`、`IncrementalHash`、`X509Certificate2`、`ZipArchive`、`CancellationTokenSource`、`ScanSession` 使用 `using` / `await using` / `DisposeAsync`。
- 原生 state data 必须在 `finally` 中执行 `WTD_STATEACTION_CLOSE`。
- 从 native 复制数据后再释放上下文，禁止持有失效指针。

## P/Invoke

- 所有 DllImport 集中在 `Core/Interop`；业务代码不直接调用原生函数。
- 返回 `IntPtr` 的 WTHelper API 必须先判空。
- 修改结构体后更新 `InteropLayoutTests` 的 `SizeOf` / `OffsetOf` 断言。
- `WinVerifyTrust` 返回 LONG，只有 `0` 代表成功。

## 依赖

- 运行时零第三方包；Excel 使用手写 OOXML。
- 测试仅使用 xUnit / Microsoft.NET.Test.Sdk。
- 不把 WPF 引入 Core 或测试。

## 代码质量

- 避免超大类；`MainViewModel` 的职责是 UI 编排，具体算法放在 Service。
- 重复逻辑抽取到 Helpers/Utilities（如 `FileSizeFormatter`、`StatusText`、`OidNames`）。
- 不创建空类、占位接口或 TODO 实现。
