# 使用文档

本文档描述 `Star.SqlData` 的全部公开 API 与实际行为，示例均可在 [`samples/Star.SqlData.Sample`](../samples/Star.SqlData.Sample) 中找到对应的可运行代码。

- [1. 连接配置](#1-连接配置)
- [2. 定义仓储](#2-定义仓储)
- [3. 查询](#3-查询)
- [4. 插入](#4-插入)
- [5. 更新](#5-更新)
- [6. 删除](#6-删除)
- [7. 计数](#7-计数)
- [8. 事务](#8-事务)
- [9. 直接执行 SQL](#9-直接执行-sql)
- [10. 响应格式与错误处理](#10-响应格式与错误处理)
- [11. 白名单维护](#11-白名单维护)
- [12. 分页建议](#12-分页建议)
- [13. 从旧版门面 API 迁移](#13-从旧版门面-api-迁移)

## 1. 连接配置

连接字符串的解析顺序：

1. 环境变量 `STAR_DB_CONNECTION`（完整连接字符串，推荐生产环境使用）；
2. `appSettings` 分字段配置（本地开发）：

```xml
<appSettings>
  <add key="DB_ServerIP"          value="127.0.0.1"/>
  <add key="DB_Port"              value="3306"/>
  <add key="DB_Database"          value="star_db"/>
  <add key="DB_User"              value="star_app"/>
  <add key="DB_Password"          value="CHANGE_ME"/>
  <add key="DB_Charset"           value="utf8mb4"/>
  <add key="DB_MaxPoolSize"       value="100"/>
  <add key="DB_MinPoolSize"       value="5"/>
  <add key="DB_ConnectionTimeout" value="30"/>
</appSettings>
```

行为要点：

- 解析是**惰性**的：只有真正需要连接时才读取配置，因此程序集加载、单元测试都不会因为缺少配置而失败。
- 缺少必填项（`DB_ServerIP`、`DB_Port`、`DB_Database`、`DB_User`）时抛出 `InvalidOperationException`，消息中带缺失的 key 名。
- 想绕过配置文件，直接传连接字符串：

```csharp
using var connection = await StarDataConnection.OpenAsync(
    "server=127.0.0.1;port=3306;database=star_db;user=star_app;password=secret");
```

## 2. 定义仓储

`RepositoryBase` 是抽象类，子类只需回答两件事：操作哪张表、允许哪些字段。

```csharp
public sealed class PlayerRepository : RepositoryBase
{
    protected override string TableName => DatabaseConstants.TableA;

    protected override HashSet<string> AllowedColumns => DatabaseConstants.TableAAllowedColumns;
}
```

校验规则：

| 方法 | 规则 |
| --- | --- |
| `ValidateTable` | 表名必须出现在 `DatabaseConstants.AllowedTables` 中，**大小写敏感** |
| `ValidateColumn` | 字段名必须在子类 `AllowedColumns` 或 `DatabaseConstants.GlobalAllowedColumns` 中，**大小写敏感** |
| `FilterValidFields` | 对字段列表逐个 `Trim()`，丢弃非白名单项，按忽略大小写去重（保留第一次出现的写法） |
| `ParseFieldList` | 把 `"col_a, col_b;col_c"` 解析成列表，支持 `,` 与 `;` 分隔 |

两个方法都是 `protected virtual`，需要特殊规则（例如按租户分表）时可以重写。

> **下面的示例都在你的仓储子类内部调用。** `GetDataByColumnAsync`、`CreateDataAsync` 等成员是 `protected`，对外暴露成什么形状（`Task<ApiResponse> GetByNameAsync(string name)`）由你自己决定，参见 [`samples/Star.SqlData.Sample/DemoRepository.cs`](../samples/Star.SqlData.Sample/DemoRepository.cs)。

## 3. 查询

### 单行查询

```csharp
var response = await repository.GetDataByColumnAsync(
    column: "col_a",
    value: "player-1",
    requestedFields: new List<string> { "col_a", "col_b", "col_c" });
```

- 生成的 SQL：``SELECT `col_a`, `col_b`, `col_c` FROM `table_a` WHERE `col_a` = @QueryValue LIMIT 1``
- 字段列表为空或全部非法 → `400`，消息 `No valid fields: ...`
- 无匹配行 → `404`
- 命中 → `200`，`data` 为 `{ 字段名: 值 }` 的字典

### 多行查询

```csharp
var response = await repository.GetDataListByColumnAsync(
    "col_b", "张三", new List<string> { "col_a", "col_b" });
```

无匹配行时返回 `404`；有数据时 `data` 为字典列表。

### 敏感字段

`DatabaseConstants.MaskedColumns` 中的字段（默认 `sensitive_col`）在查询结果里始终是 `***`：

```csharp
// 默认 maskSecrets: true —— 上层永远拿不到原文
var masked = await repository.GetDataByColumnAsync("col_a", "player-1", fields,
    connection: null, transaction: null, maskSecrets: true);
```

确需读取原文（例如登录校验）时显式关闭脱敏，并只在内部方法中使用：

```csharp
var raw = await repository.GetDataByColumnAsync("col_a", "player-1", fields,
    connection: null, transaction: null, maskSecrets: false);
```

### 字段字符串入口

如果字段来自请求参数（例如 `"col_a,col_b"`），在子类里解析后再查询：

```csharp
public Task<ApiResponse> GetByIdAsync(string id, string requestedFieldsString)
    => GetDataByColumnAsync("col_a", id, ParseFieldList(requestedFieldsString));
```

## 4. 插入

```csharp
var response = await repository.CreateDataAsync(new Dictionary<string, object>
{
    ["col_a"] = "player-1",
    ["col_b"] = "张三",
    ["col_c"] = 3,
});
```

- 不在白名单的字段被**静默丢弃**；若全部被丢弃 → `400`，消息 `No valid fields: ...`
- `data` 为空 → `400`，消息 `Insert data cannot be empty`
- 成功 → `200`，`data` 为 `{ "AffectedRows": 1 }`
- 受影响行数为 0 → `500`

**特权字段**（`DatabaseConstants.PrivilegedColumns`）默认不可写入。只有专用方法可以显式放行：

```csharp
// 注册/改密等专用方法内部使用
await repository.CreateDataAsync(data, connection, transaction, allowPrivilegedColumns: true);
```

需要拿到自增主键时用 `CreateDataAndGetIdAsync`（内部 `SELECT LAST_INSERT_ID()`，失败返回 `0`）。

## 5. 更新

```csharp
var response = await repository.UpdateDataAsync(
    whereColumn: "col_a",
    whereValue: "player-1",
    updateData: new Dictionary<string, object> { ["col_b"] = "李四" });
```

保护规则（全部在生成 SQL 之前执行）：

1. `whereValue` 为空 → `400`
2. `updateData` 为空 → `400`
3. 条件列必须在白名单中，否则 `400`
4. 更新字段必须在白名单中，否则被丢弃
5. 与 `whereColumn` 同名的字段被丢弃（禁止改写条件列/主键）
6. 特权字段被丢弃，除非 `allowPrivilegedColumns: true`
7. 过滤后无有效字段 → `400`，消息 `No valid update fields`

SQL 形如：``UPDATE `table_a` SET `col_b` = @Update_col_b WHERE `col_a` = @Where_Value LIMIT 1``（单行更新）。

## 6. 删除

```csharp
var response = await repository.DeleteDataAsync("col_a", "player-1");
```

``DELETE FROM `table_a` WHERE `col_a` = @Where_Value LIMIT 1``，影响 0 行返回 `404`。

> 批量删除不在库的职责范围内：需要按业务条件删除时，请实现专用方法并显式使用事务。

## 7. 计数

```csharp
int total = await repository.CountAsync();
int filtered = await repository.CountAsync("col_b", "张三");
```

条件列为空时统计全表。**注意**：计数方法内部吞掉所有异常并返回 `0`，因此 `0` 既可能是"无数据"也可能是"查询失败"；需要区分时请改用 `GetDataListByColumnAsync`。

## 8. 事务

所有写方法都接受可选 `MySqlConnection` / `MySqlTransaction`，组合多个操作时复用同一个连接与事务：

```csharp
using var connection = await StarDataConnection.OpenAsync();
using var transaction = await connection.BeginTransactionAsync();
try
{
    await repository.CreateDataAsync(
        new Dictionary<string, object> { ["col_a"] = "player-1", ["col_b"] = "张三" },
        connection, transaction);

    await repository.UpdateDataAsync(
        "col_a", "player-1",
        new Dictionary<string, object> { ["col_c"] = 1 },
        connection, transaction);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

传入 `connection` 时调用方负责释放；不传时方法内部自行打开并释放连接。

## 9. 直接执行 SQL

`DatabaseExecutor` 是 `internal`，库外的自定义 SQL 请通过公开入口自行执行：

```csharp
using var connection = await StarDataConnection.OpenAsync();
using var command = connection.CreateCommand();
command.CommandText = "SELECT `col_a` FROM `table_a` ORDER BY `col_a` LIMIT @offset, @size";
command.Parameters.AddWithValue("@offset", 0);
command.Parameters.AddWithValue("@size", 20);

using var reader = await command.ExecuteReaderAsync();
while (await reader.ReadAsync())
    Console.WriteLine(reader.GetString(0));
```

> 自定义 SQL 不受白名单保护，请自行保证标识符来自可信来源（例如用 `switch` 映射到常量，而不是直接拼接用户输入）。

## 10. 响应格式与错误处理

所有仓储方法返回 `ApiResponse`，JSON 固定为：

```json
{
  "code": "200",
  "msg": "Operation succeeded.",
  "data": { }
}
```

| code | 触发条件 |
| --- | --- |
| `200` | 操作成功 |
| `400` | 参数错误：表名/字段名不在白名单、数据为空、条件值为空、过滤后无有效字段 |
| `404` | 未找到匹配数据 |
| `500` | 数据库连接/执行失败，或插入受影响行数为 0 |

工厂方法：`ApiResponse.Success`、`BadRequest`、`NotFound`、`Unauthorized`、`Forbidden`、`ServerError`、`Error(code, msg, data)`；序列化用 `ToJson()`（缩进格式）。

`ValidateTable` / `ValidateColumn` 在仓储方法内部抛出 `ArgumentException`，会被捕获并映射为 `400`；如果你直接调用这两个受保护方法，需要自己处理异常。

## 11. 白名单维护

新增一张表或一个字段的完整流程：

1. 在 `src/Star.SqlData/Core/DatabaseConstants.cs` 中：
   - 增加表名常量并加入 `AllowedTables`；
   - 增加分表字段集合（如 `TableXAllowedColumns`）；
   - 确认字段是否也需要加入 `GlobalAllowedColumns`（跨表通用字段）；
   - 敏感字段加入 `MaskedColumns`，特权字段加入 `PrivilegedColumns`。
2. 在 `tests/Star.SqlData.Tests/DatabaseConstantsTests.cs` 的清单里补上对应断言（测试会校验"分表字段 ⊆ 全局字段"）。
3. PR 描述中说明新增表/字段的业务用途与敏感级别。

只改代码里的白名单不会自动改数据库结构，建表脚本请放在你自己的迁移体系里（`samples/sql/` 仅作示例）。

## 12. 分页建议

库内没有内建分页方法（不同业务的分页语义差异太大）。推荐两种做法：

1. **业务条件分页**：用 `CountAsync(whereColumn, whereValue)` 取总数，再用自定义 SQL（第 9 节）做 `LIMIT/OFFSET`；
2. **专用仓储方法**：在子类中实现 `GetPageAsync(int page, int pageSize)`，内部走 `StarDataConnection` 或复用基类的单条查询能力。

页码约定从 `1` 开始，`pageSize` 建议由调用方限制上限（例如 ≤ 100）。

## 13. 从旧版门面 API 迁移

旧版文档中的门面类 `StarSqlData` 及其业务方法（注册账号、帖子审核、点赞等）属于**业务层**，不在本库范围内。本库只提供基础设施：

| 旧写法 | 本库写法 |
| --- | --- |
| `StarSqlData.GetOpenConnectionAsync()` | `StarDataConnection.OpenAsync()` |
| `StarSqlData.QueryAsync(table, column, value)` | 子类中调用 `GetDataByColumnAsync(column, value, fields)` |
| `StarSqlData.CreateTableDataAsync(...)` | 子类中调用 `CreateDataAsync(data, ...)` |
| `StarSqlData.UpdateTableDataAsync(...)` | 子类中调用 `UpdateDataAsync(whereColumn, whereValue, data, ...)` |
| 业务方法（注册/审核/点赞） | 在你的仓储子类中组合基类方法实现，必要时用事务 |

迁移时要特别注意：旧版允许按任意表名 + 任意字段名查询，本库要求先把这些表/字段注册进 `DatabaseConstants`，否则返回 `400`。
