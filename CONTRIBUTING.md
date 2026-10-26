# 贡献指南

感谢你愿意帮忙改进 `Star.DataBase`！本文说明本地开发流程、代码约定与评审关注点。

## 开始之前

- 需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)（`dotnet --version` 应 ≥ 8.0）
- MySQL 5.7+ 可选：只有集成测试与示例需要它
- 首次克隆后执行：

```bash
dotnet restore Star.SqlData.sln
dotnet build   Star.SqlData.sln -c Release
dotnet test    tests/Star.SqlData.Tests/Star.SqlData.Tests.csproj -c Release
```

单元测试**不需要**数据库；设置 `STAR_DB_CONNECTION` 后集成测试才会真正连接 MySQL。

## 目录约定

| 目录 | 内容 | 是否可打包进 NuGet |
| --- | --- | --- |
| `src/Star.SqlData` | 类库本体 | 是 |
| `tests/Star.SqlData.Tests` | 单元测试 / 集成测试 | 否 |
| `samples/Star.SqlData.Sample` | 可运行示例 | 否 |
| `samples/sql` | 示例/历史建表脚本（非库代码） | 否 |
| `docs` | 使用与架构文档 | 否 |

## 代码约定

- 标识符、代码注释、提交信息用**英文**；`README`、`docs`、Issue 用**中文**（可以附英文）。
- 四个空格缩进，文件编码 UTF-8，行尾遵循 `.gitattributes`（仓库内统一 LF）。
- 关注点分离：`Core` 只做执行与常量，`Repositories` 只做校验 + SQL 组装，业务语义留在调用方的仓储子类。
- 新增公共 API 必须有 XML 文档注释；新增行为必须有测试。
- 分析器警告应为 0（`EnableNETAnalyzers` 已开启）。确实需要抑制时，写清原因。

## 提交信息

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/)：

```
<type>(<scope>): <subject>

type: feat | fix | docs | test | refactor | chore | ci
scope: 例如 repository、constants、ci、docs
```

示例：

```
fix(repository): 更新时忽略与条件列同名的字段
feat(constants): 增加 table_h 及其字段白名单
```

## 修改白名单（重要）

表名/字段名白名单是安全边界，改动请务必：

1. 在 PR 描述中说明新增表/字段的业务用途与数据敏感级别；
2. 敏感字段同时加入 `DatabaseConstants.MaskedColumns`；
3. 可能被越权改写的字段加入 `DatabaseConstants.PrivilegedColumns`；
4. 同步更新 `tests/Star.SqlData.Tests/DatabaseConstantsTests.cs` 的清单与不变量断言；
5. 确认没有硬编码的旧字段名残留在仓储子类里。

## 测试要求

- 安全关键分支（白名单拒绝、字段过滤、脱敏、特权字段、错误码映射）必须有不依赖数据库的测试；
- 涉及真实 SQL 的行为，尽量放进"有 `STAR_DB_CONNECTION` 才执行"的集成测试，不要写成强依赖；
- 本地跑通全部测试后再提 PR。

## 提交 PR

1. Fork 并从 `main` 拉分支：`feat/xxx`、`fix/xxx`；
2. 保持改动聚焦，一个 PR 只做一件事；
3. 填写 PR 模板（变更说明、影响面、验证方式）；
4. 确保 CI 全绿：构建、单元测试、集成测试（Linux + MySQL）、打包校验、示例冒烟。

## 行为准则

参与本项目的讨论与贡献即表示同意：对事不对人、尊重不同经验水平、不发布他人隐私或凭据。维护者有权删除不当内容并拒绝违反准则的贡献。

## 维护者清单：首次推送到 GitHub

仓库里所有外部链接使用 `OWNER` 占位符。推送到 GitHub 前请全局替换（把 `OWNER` 换成用户名/组织名）：

| 文件 | 需要替换的内容 |
| --- | --- |
| `README.md` | CI 徽章（第 9 行，替换后取消注释）、`git clone` 地址 |
| `.github/ISSUE_TEMPLATE/config.yml` | 安全策略与 Discussions 地址 |
| `.github/ISSUE_TEMPLATE/bug_report.yml` | SECURITY.md 链接 |
| `src/Star.SqlData/Star.SqlData.csproj` | 发布 NuGet 前取消注释 `RepositoryUrl` |

另外建议：

1. 在 GitHub 仓库设置中启用 **Private vulnerability reporting**（`SECURITY.md` 依赖它）；
2. 确认默认分支为 `main`（CI 触发条件与徽章都基于它）；
3. 发布 NuGet 包前，用 `dotnet pack src/Star.SqlData/Star.SqlData.csproj -c Release` 检查 `README.md` 是否被打进包里。
