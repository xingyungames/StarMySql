# SQL 示例脚本

本目录的脚本**不是** `Star.SqlData` 库的一部分，库本身从不执行 DDL。

- `oa-task-schema.draft.sql`：一份早期的 OA（员工 / 任务调度）表结构**草案**，仅用于说明数据结构思路，与库中的 `DatabaseConstants` 白名单没有对应关系。

如果你要为本库准备演示数据：

1. 按 `src/Star.SqlData/Core/DatabaseConstants.cs` 中定义的表名/字段名建表；
2. 只授予业务账号 `SELECT`/`INSERT`/`UPDATE`/`DELETE` 权限；
3. `samples/Star.SqlData.Sample` 会在启动时自行创建 `table_a`（`col_a`/`col_b`/`col_c` 三列），可直接作为最小示例参考。
