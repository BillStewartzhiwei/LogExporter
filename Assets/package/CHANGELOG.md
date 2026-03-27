# CHANGELOG

本文件记录 `LogExporter` 包的重要变更。

## [1.0.2] - 2026-03-27
### Added
- 新增日志归档配置：`enableArchive`、`archiveFolderName`。
- 启用归档后，启动时会自动将当前日志文件之外的历史日志移动到归档目录。
- 启用大小滚动时，被滚动出的旧日志会自动进入归档目录。

## [1.0.1] - 2026-03-27
### Added
- 新增打包前自动清理项目根目录 `Logs` 文件夹的功能（Editor 构建流程）。
- 新增 `PreBuildLogsCleaner` 构建前钩子，避免旧日志影响打包与排查。

### Changed
- 更新文档中的最近更新说明，补充日志清理行为。

## [1.0.0] - 2026-03-26
### Added
- 初始版本：运行时日志导出。
- 支持日志等级过滤（Info/Warning/Error/Exception）。
- 支持日志文件命名模式与滚动策略配置。
- 支持 `RuntimeLogConfig` ScriptableObject 配置。

