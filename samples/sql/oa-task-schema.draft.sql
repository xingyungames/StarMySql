-- OA / 员工管理与任务调度草案
-- 仅用于说明数据结构，现有业务未强制依赖本文件；实际实现为文件存储以避免破坏原数据库逻辑。

CREATE TABLE IF NOT EXISTS oa_employee_applicants (
    id VARCHAR(64) PRIMARY KEY,
    employee_id VARCHAR(32) DEFAULT 'PENDING',
    name VARCHAR(128) NOT NULL,
    position VARCHAR(128) NOT NULL,
    skills TEXT NOT NULL,
    contact VARCHAR(256) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password VARCHAR(255) NOT NULL,
    status ENUM('pending','approved','rejected') DEFAULT 'pending',
    reviewer VARCHAR(128) DEFAULT '',
    created_at DATETIME NOT NULL,
    reviewed_at DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS oa_employees (
    employee_id VARCHAR(32) PRIMARY KEY,
    name VARCHAR(128) NOT NULL,
    position VARCHAR(128) NOT NULL,
    skills TEXT NOT NULL,
    contact VARCHAR(256) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password VARCHAR(255) NOT NULL,
    status ENUM('active','disabled') DEFAULT 'active',
    reviewer VARCHAR(128) DEFAULT 'management',
    source_applicant_id VARCHAR(64) NULL,
    created_at DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS oa_tasks (
    id VARCHAR(64) PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    task_type VARCHAR(128) NOT NULL,
    description TEXT NOT NULL,
    status ENUM('todo','claimed','done') DEFAULT 'todo',
    assignee VARCHAR(64) DEFAULT '',
    assigner VARCHAR(128) DEFAULT 'OA',
    created_at DATETIME NOT NULL,
    claimed_at DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS oa_db_operation_logs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    operator VARCHAR(128) NOT NULL,
    action VARCHAR(32) NOT NULL,
    sql_text TEXT NOT NULL,
    affected_rows INT DEFAULT 0,
    error_message TEXT NULL,
    created_at DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 说明：数据库调度功能仅允许 SELECT / INSERT，且必须提供账号和密码验证；禁止 UPDATE / DELETE / DROP / ALTER 等危险操作。
