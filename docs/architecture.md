# 架构说明

## 目标与非目标

**目标**

- 用尽量少的代码为一组已知表提供安全的读写能力；
- 让"非法标识符"和"敏感字段外泄"在网络/序列化边界之前就被拦住；
- 让调用方拿到统一、可序列化的结果，而不是裸 `DataTable` 或异常。

**非目标**

- 不实现 ORM（无实体映射、无变更跟踪、无 LINQ）；
- 不实现业务语义（注册、审核、点赞等留在业务仓储里）；
- 不实现数据库迁移/建表（`samples/sql/` 只是示例脚本）；
- 不追求多数据库方言兼容（当前只支持 MySQL）。

## 分层

```
        调用方（Controller / Service / 你的仓储子类）
                          │
                          ▼
      RepositoryBase（Star.SqlData.Repositories）
        校验 · 字段过滤 · SQL 拼装 · 错误 → ApiResponse
                          │
                          ▼
      DatabaseExecutor（Star.SqlData.Core，internal）
        连接获取 · 命令执行 · 参数绑定 · 结果转字典 · 脱敏
                          │
                          ▼
                MySql.Data.MySqlClient
                          │
                          ▼
                       MySQL
```

横切关注点：

- `DatabaseConstants`：白名单与敏感字段的**唯一**来源，被两层共同引用；
- `StarDataConnection`：公开的连接入口，也是 `DatabaseExecutor` 打开连接的实现；
- `ApiResponse`：跨层的统一返回契约。

## 关键设计决策

| 决策 | 原因 | 代价 |
| --- | --- | --- |
| 白名单放在常量类，而不是配置文件 | 编译期可见、代码评审可审计、不会被运行时环境意外放开 | 新增表/字段需要改代码并发版 |
| 执行器 `internal` | 避免调用方绕过校验直接执行任意 SQL | 自定义 SQL 需走 `StarDataConnection` 自建命令 |
| 值全部参数化，标识符全部白名单 | 白名单之外的字符串永远进不了 SQL 文本，注入面收敛 | 不支持动态列名/动态排序（需显式扩展） |
| 脱敏放在**执行器**层 | 无论上层怎么写，敏感字段都不会离开数据层 | 需要读原文时必须显式 `maskSecrets: false` |
| 连接字符串惰性解析 | 程序集加载、单元测试不需要数据库 | 配置错误会推迟到首次连接才暴露（但消息里带缺失 key） |
| 异常 → `ApiResponse` 而非抛出 | 上层统一按 `code` 分支，减少 try/catch 样板 | 需要区分"异常类型"时信息只剩 `msg` 文本 |
| 写操作二次过滤 + 条件列保护 | 让"误传字段"降级为丢弃而不是报错，同时阻止越权改写主键 | 静默丢弃可能掩盖拼写错误（由 `400 No valid fields` 兜底） |
| 单行语义（`LIMIT 1`） | 更新/删除默认只作用于一行，降低误伤范围 | 批量操作必须显式实现 |

## 扩展点

| 扩展点 | 方式 | 典型用途 |
| --- | --- | --- |
| `TableName` | 重写抽象属性 | 绑定具体表 |
| `AllowedColumns` | 重写抽象属性 | 表级字段白名单 |
| `ValidateTable` / `ValidateColumn` | 重写 `protected virtual` | 多租户分表名、动态白名单 |
| `allowPrivilegedColumns` | 专用方法传参 | 注册、改密、令牌更新 |
| `maskSecrets` | 专用方法传参 | 登录校验需要读敏感字段原文 |
| 事务 | 传入 `connection` + `transaction` | 跨表写入一致性 |
| 新的白名单 | 改 `DatabaseConstants` | 新增表/字段 |

## 错误映射

| 出错位置 | 结果 |
| --- | --- |
| 白名单校验抛 `ArgumentException` | `400`（`msg` 为原始异常消息） |
| 输入为空/过滤后为空 | `400`（`msg` 说明缺少什么） |
| 无匹配行 | `404` |
| 连接失败、SQL 执行失败 | `500`（`msg` 前缀区分 Query/creation/update/deletion） |
| `CountAsync` 内部任意异常 | 返回 `0`，不抛 |

## 测试策略

| 层次 | 位置 | 说明 |
| --- | --- | --- |
| 白名单不变量 | `DatabaseConstantsTests` | 表常量 ∈ 表白名单；分表字段 ⊆ 全局字段；脱敏/特权字段忽略大小写命中 |
| 响应契约 | `ApiResponseTests` | 工厂方法状态码、JSON 字段名 `code`/`msg`/`data`、`null` 序列化 |
| 校验与参数过滤 | `RepositoryBaseTests` | 非法表/列 → `400`；空数据 → `400`；条件列与特权字段被丢弃；**不需要数据库** |
| 配置解析 | `ConfigurationTests` | 环境变量优先、空白环境变量回退、缺配置抛 `InvalidOperationException` |
| 集成（可选） | `MySqlIntegrationTests` | 仅在设置 `STAR_DB_CONNECTION` 时执行，否则静默跳过 |

设计原则：**所有安全关键分支都能在无数据库的情况下断言**，这样 CI 不会因为环境依赖而失去防护网。

## 白名单治理

白名单是安全边界，因此改动流程比普通代码更严格：

1. 新增项必须在 PR 描述中说明业务用途、数据敏感级别；
2. 敏感字段必须同时进入 `MaskedColumns`；可被越权改写的字段进入 `PrivilegedColumns`；
3. `DatabaseConstantsTests` 的不变量断言必须同步更新；
4. 删除白名单项前先确认没有存量调用方（`grep` 仓储子类）。

## 演进方向（未实现）

- 分页方法（`GetPageAsync`）与排序白名单；
- 结构化日志钩子（当前错误只体现在 `ApiResponse.msg`）；
- 多方言支持（`DbProviderFactory` 抽象）——当前代码与 `MySql.Data` 强耦合；
- 白名单从代码常量迁移到可热加载的配置（需配合审计与签名校验）。
