# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)，格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [Unreleased]

### 计划中

- 分页查询与排序白名单
- 结构化错误码（在 `ApiResponse` 之外提供机器可读的错误分类）

## [1.0.0] - 2026-10-26

首个面向开源的版本：把原有的数据访问层整理为可独立引用的 `Star.SqlData` 类库。

### 新增

- `src/Star.SqlData` 类库项目（`net8.0`），可 `dotnet pack` 为 NuGet 包
- `StarDataConnection` 公开连接入口：`GetConnectionString()`、`OpenAsync()`（支持显式连接字符串重载）
- `DatabaseConstants.PrivilegedColumns` / `MaskedColumns` 及 `IsPrivilegedColumn()` / `IsMaskedColumn()` 辅助方法
- `RepositoryBase.PrivilegedColumns` / `MaskedColumns` 作为兼容别名指向同一集合实例
- `tests/Star.SqlData.Tests`（xUnit）：白名单不变量、响应契约、校验与过滤、配置解析，以及可选的 MySQL 集成测试
- `samples/Star.SqlData.Sample`：可运行的控制台示例（自动建演示表并完成增查改删）
- `docs/usage.md`、`docs/architecture.md`
- GitHub 元文件：`.gitignore`、`.gitattributes`、`.editorconfig`、`LICENSE`（MIT）、Issue/PR 模板、`CONTRIBUTING.md`、`SECURITY.md`
- `.github/workflows/ci.yml`：Linux（带 MySQL 服务容器）+ Windows 双平台构建与测试，打包校验与示例冒烟

### 变更

- 非法表名/字段名不再返回 `500`，改为 `400` 参数错误（与响应码约定一致）
- `FilterValidFields` 会对每个字段名做 `Trim()`，`" col_a "` 这类带空格的输入不再被静默丢弃
- 连接字符串改为**惰性解析**：缺少配置不再影响程序集加载
- 脱敏判断与特权字段判断统一走 `DatabaseConstants`（忽略大小写）
- 写方法（`CreateDataAsync` / `UpdateDataAsync` / `GetDataByColumnAsync`）的参数顺序调整，`CancellationToken` 移到最后
- 更新/插入时对字段名做 `Trim()`，空白字段名直接跳过

### 移除

- `Microsoft.Extensions.Configuration` 的未使用引用
- 内部业务文档与包含内部表名/路径的历史文档（不随开源仓库发布）
