# Star.SqlData

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![MySQL](https://img.shields.io/badge/MySQL-5.7%2B-4479A1)](https://www.mysql.com/)

<!--
  CI 徽章：把下面的 OWNER 换成你的 GitHub 用户名/组织名后取消注释。
  [![CI](https://github.com/OWNER/StarDataBase/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/StarDataBase/actions/workflows/ci.yml)
-->

基于 MySQL 的轻量级 .NET 数据访问层：**白名单校验 + 参数化查询 + 敏感字段脱敏 + 统一响应格式**，并提供开箱即用的通用仓储基类。

> A small, security-first MySQL data-access layer for .NET: table/column whitelists, parameterized SQL, secret masking and a unified `ApiResponse` envelope, plus a reusable repository base class. [English overview ↓](#english-overview)

---

## 目录

- [为什么用它](#为什么用它)
- [安装](#安装)
- [快速开始](#快速开始)
- [核心概念](#核心概念)
- [安全模型](#安全模型)
- [项目结构](#项目结构)
- [构建与测试](#构建与测试)
- [文档](#文档)
- [贡献](#贡献)
- [许可证](#许可证)
- [English overview](#english-overview)

## 为什么用它

直接拼接 SQL 的数据访问代码有三个典型问题：字段名来自外部输入、SQL 注入、以及把密码等敏感字段顺手返回给前端。`Star.SqlData` 用一套很小的约定把这三点一起解决：

| 能力 | 说明 |
| --- | --- |
| 表名/字段名白名单 | 表名与字段名必须预先在 `DatabaseConstants` 中注册，非法值直接拒绝，不做字符串拼接兜底 |
| 参数化查询 | 所有查询值都通过 `MySqlParameter` 传递，永不进 SQL 文本 |
| 敏感字段脱敏 | `DatabaseConstants.MaskedColumns` 中的字段在返回前统一替换为 `***` |
| 特权字段保护 | `DatabaseConstants.PrivilegedColumns` 只能由专用方法写入，防止普通更新越权改密码 |
| 统一响应 | 所有仓储方法返回 `ApiResponse`，JSON 结构固定为 `{ code, msg, data }` |
| 连接/事务复用 | 每个方法都有可选的 `MySqlConnection` / `MySqlTransaction` 重载，方便组合事务 |

## 安装

**方式一：直接引用源码项目**

```bash
git clone https://github.com/OWNER/StarDataBase.git
dotnet add <你的项目>.csproj reference StarDataBase/src/Star.SqlData/Star.SqlData.csproj
```

**方式二：作为 NuGet 包引用**（执行 `dotnet pack` 后发布到本地源或私有源）

```bash
dotnet pack src/Star.SqlData/Star.SqlData.csproj -c Release -o ./artifacts
dotnet add <你的项目>.csproj package Star.SqlData --source ./artifacts
```

依赖：`.NET 8.0`、`MySql.Data`、`Newtonsoft.Json`、`System.Configuration.ConfigurationManager`。

## 快速开始

### 1. 配置连接字符串

推荐使用环境变量（不落盘）：

```bash
# Linux / macOS
export STAR_DB_CONNECTION="server=127.0.0.1;port=3306;database=star_db;user=star_app;password=secret;charset=utf8mb4"

# Windows PowerShell
$env:STAR_DB_CONNECTION = "server=127.0.0.1;port=3306;database=star_db;user=star_app;password=secret;charset=utf8mb4"
```

也可以用 `appSettings` 分字段配置（见 `samples/Star.SqlData.Sample/app.config`）：`DB_ServerIP`、`DB_Port`、`DB_Database`、`DB_User`、`DB_Password`、`DB_Charset`、`DB_MaxPoolSize`、`DB_MinPoolSize`、`DB_ConnectionTimeout`。连接字符串在首次使用时才解析，缺少配置不会影响程序集加载。

### 2. 声明你的仓储

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Star.SqlData.Core;
using Star.SqlData.Models;
using Star.SqlData.Repositories;

public sealed class PlayerRepository : RepositoryBase
{
    protected override string TableName => DatabaseConstants.TableA;

    protected override HashSet<string> AllowedColumns => DatabaseConstants.TableAAllowedColumns;

    // 按昵称查一名玩家，只返回需要的字段
    public Task<ApiResponse> GetByNameAsync(string name)
        => GetDataByColumnAsync("col_b", name, new List<string> { "col_a", "col_b", "col_c" });

    // 新建一名玩家
    public Task<ApiResponse> AddAsync(string id, string name, int score)
        => CreateDataAsync(new Dictionary<string, object>
        {
            ["col_a"] = id,
            ["col_b"] = name,
            ["col_c"] = score,
        });
}
```

### 3. 调用

```csharp
var repository = new PlayerRepository();

await repository.AddAsync("player-1", "张三", 3);
var response = await repository.GetByNameAsync("张三");

Console.WriteLine(response.Code);    // 200 / 400 / 404 / 500 ...
Console.WriteLine(response.ToJson()); // {"code":"200","msg":"...","data":{...}}
```

需要自己写 SQL 时，用公开的连接入口：

```csharp
using var connection = await StarDataConnection.OpenAsync();
using var command = connection.CreateCommand();
command.CommandText = "SELECT COUNT(1) FROM `table_a`";
var total = Convert.ToInt32(await command.ExecuteScalarAsync());
```

完整可运行示例见 [`samples/Star.SqlData.Sample`](samples/Star.SqlData.Sample)。

## 核心概念

| 类型 | 命名空间 | 作用 |
| --- | --- | --- |
| `DatabaseConstants` | `Star.SqlData.Core` | 表名常量、全局/分表字段白名单、特权字段与脱敏字段集合 |
| `StarDataConnection` | `Star.SqlData` | 公开的连接入口，解析连接字符串并返回已打开的 `MySqlConnection` |
| `DatabaseExecutor` | `Star.SqlData.Core` | 内部执行器：统一的命令执行、参数绑定、结果集转字典、脱敏 |
| `ApiResponse` | `Star.SqlData.Models` | 统一响应模型，`code` / `msg` / `data` 与常用工厂方法 |
| `RepositoryBase` | `Star.SqlData.Repositories` | 通用 CRUD 基类：查询、插入、更新、删除、计数，内置校验与错误映射 |

**响应码约定**

| code | 含义 |
| --- | --- |
| `200` | 成功 |
| `400` | 参数错误（表名/字段名不在白名单、数据为空等） |
| `404` | 未找到匹配数据 |
| `500` | 数据库或服务器错误 |

## 安全模型

1. **白名单即边界。** `DatabaseConstants.AllowedTables` 与字段白名单是唯一可信来源；表名/字段名不做任何"清洗"，只做"是否已注册"的判断。
2. **值永远参数化。** 只有白名单中的表名/字段名会进入 SQL 文本，其余一律走 `@参数`。
3. **写操作有两次过滤。** 插入/更新时，不在白名单的字段被丢弃；`PrivilegedColumns` 默认也被丢弃，只有显式传 `allowPrivilegedColumns: true` 的专用方法才能写入。
4. **WHERE 列不可被覆盖。** 更新时会跳过与条件列同名的字段，避免越权改写主键或归属列。
5. **敏感字段默认脱敏。** 查询结果中 `MaskedColumns` 的字段值统一为 `***`；确需读取原文请使用专用方法并显式传入 `maskSecrets: false`。

扩展白名单只需修改 `DatabaseConstants.cs` 并在 PR 中说明新增的表/字段用途。

## 项目结构

```
StarDataBase/
├── src/Star.SqlData/                 # 主库（net8.0）
│   ├── Configuration.cs              # 连接配置解析（环境变量 / appSettings）
│   ├── StarDataConnection.cs         # 公开连接入口
│   ├── Core/
│   │   ├── DatabaseConstants.cs      # 白名单定义
│   │   └── DatabaseExecutor.cs       # SQL 执行器（内部）
│   ├── Models/ApiResponse.cs         # 统一响应模型
│   └── Repositories/RepositoryBase.cs# 通用仓储基类
├── tests/Star.SqlData.Tests/         # xUnit 单元测试 + 可选 MySQL 集成测试
├── samples/Star.SqlData.Sample/      # 可运行控制台示例
│   └── sql/                          # 示例/历史建表脚本（非库代码）
├── docs/                             # 使用文档与架构说明
├── .github/workflows/ci.yml          # GitHub Actions：构建 + 测试 + 打包 + 示例冒烟
└── Star.SqlData.sln
```

## 构建与测试

```bash
dotnet restore Star.SqlData.sln
dotnet build   Star.SqlData.sln -c Release --no-restore
dotnet test    tests/Star.SqlData.Tests/Star.SqlData.Tests.csproj -c Release --no-build
```

单元测试不需要数据库。集成测试只有在设置 `STAR_DB_CONNECTION` 后才会真正连接 MySQL，否则自动跳过：

```bash
STAR_DB_CONNECTION="server=127.0.0.1;port=3306;database=star_db;user=root;password=root" \
  dotnet test tests/Star.SqlData.Tests/Star.SqlData.Tests.csproj -c Release
```

跑一遍完整示例：

```bash
STAR_DB_CONNECTION="server=127.0.0.1;port=3306;database=star_db;user=star_app;password=secret" \
  dotnet run --project samples/Star.SqlData.Sample
```

CI（`.github/workflows/ci.yml`）会在 PR 上执行：还原 → 构建 → 单元测试 → 带 MySQL 服务容器的集成测试 → `dotnet pack` 打包校验 → 运行示例。

## 文档

- [`docs/usage.md`](docs/usage.md)：完整 API 用法、事务、分页、错误处理
- [`docs/architecture.md`](docs/architecture.md)：分层设计、扩展点与白名单维护流程
- [`CHANGELOG.md`](CHANGELOG.md)：版本记录

## 贡献

欢迎 Issue 与 PR，请先阅读 [`CONTRIBUTING.md`](CONTRIBUTING.md)。安全问题请按 [`SECURITY.md`](SECURITY.md) 私下报告。

## 许可证

[MIT](LICENSE) © 2026 StarDataBase contributors

---

## English overview

`Star.SqlData` is a compact MySQL data-access layer for .NET 8 built around three ideas:

1. **Whitelists are the security boundary.** Table and column names must be registered in `DatabaseConstants`; anything else is rejected before SQL is built.
2. **Values are always parameterized.** Only whitelisted identifiers reach the SQL text.
3. **Every operation returns the same envelope.** `ApiResponse` serializes to `{ "code": "200", "msg": "...", "data": {} }`.

```csharp
public sealed class PlayerRepository : RepositoryBase
{
    protected override string TableName => DatabaseConstants.TableA;
    protected override HashSet<string> AllowedColumns => DatabaseConstants.TableAAllowedColumns;
}

var response = await new PlayerRepository()
    .GetDataByColumnAsync("col_a", "player-1", new List<string> { "col_a", "col_b" });
```

Highlights: connection/transaction reuse through optional parameters, automatic masking of secret columns, protected "privileged" columns that only dedicated methods may write, and optional MySQL integration tests that skip themselves when no database is configured.

Quick start: set `STAR_DB_CONNECTION` (or `appSettings` keys `DB_ServerIP`, `DB_Port`, `DB_Database`, `DB_User`, `DB_Password`, …), subclass `RepositoryBase`, call the CRUD helpers. See [`docs/usage.md`](docs/usage.md) and the runnable sample in [`samples/Star.SqlData.Sample`](samples/Star.SqlData.Sample).

Requires .NET 8 and MySQL 5.7+.
