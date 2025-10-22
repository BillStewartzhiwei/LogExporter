# LogExporter

**LogExporter** 是一个 Unity 运行时日志导出工具包，支持在项目运行时将日志输出到文件，功能灵活、可配置，适用于开发调试、VR/AR、移动端或打包发布项目。

## 主要功能

### 1. 运行时日志导出
- 自动捕获 Unity 的 `Debug.Log`、`Debug.LogWarning`、`Debug.LogError` 和异常信息
- 支持过滤日志等级（Info、Warning、Error、Exception）
- 支持在编辑器中打印日志到控制台

### 2. 日志文件管理
- 日志文件存放在项目根目录下的 `Logs` 文件夹，与 `Assets` 同级
- 自动创建 Logs 文件夹，如果已存在不会报错
- 日志文件可按 **日期** 或 **执行次数** 命名，便于版本管理

### 3. 可配置的 ScriptableObject
- 所有日志设置通过 `RuntimeLogConfig` ScriptableObject 管理
- 可通过 Inspector 编辑日志等级、导出路径、命名模式和控制台输出
- 支持 Runtime 和 Editor 模式统一配置

### 4. 简单易用
- 安装 UPM 包后即可使用，无需额外代码
- Play Mode 下自动初始化，开始记录日志
- 支持跨平台（Windows/macOS/Linux）

## 使用场景
- 调试运行时问题，捕获崩溃或异常
- VR/AR 项目或移动端项目，方便记录日志
- 远程或打包项目中收集日志文件

## 安装与使用
1. 通过 Package Manager 安装 LogExporter UPM 包
2. 在 `Runtime/Resources` 文件夹中创建或修改 `RuntimeLogConfig.asset` 配置文件
3. 在 Inspector 中设置日志等级、导出路径、命名模式等
4. Play Mode 下日志自动生成到 `Logs` 文件夹
