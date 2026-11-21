# 个人记账 API

使用 C#、ASP.NET Core Minimal API 和 MySQL 实现的学习项目，支持账单增删改查、按月筛选与月度收支汇总。账单持久化保存在 MySQL 中，重启 API 后仍可查询。🧾

## 技术栈

- .NET 10、C#、ASP.NET Core Minimal API。🛠️
- MySQL，开发环境使用 MySQL Community Server 9.4.0。💾
- MySql.Data，通过参数化 SQL 访问数据库。🔗

## 环境准备

安装 .NET 10 SDK 和 MySQL，启动 MySQL 服务，并准备可登录的数据库账号。下面的命令按 Windows PowerShell 编写，在包含 `TodoApi.csproj` 的项目目录执行 .NET 命令。📁

```powershell
dotnet --info
dotnet restore
```

在 MySQL 客户端执行下面的 SQL，创建数据库和账单表。金额使用 `DECIMAL(12, 2)`，编号由数据库自动生成。🛠️

```sql
CREATE DATABASE IF NOT EXISTS expense_tracker
CHARACTER SET utf8mb4;

USE expense_tracker;

CREATE TABLE IF NOT EXISTS transactions (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    type VARCHAR(10) NOT NULL,
    amount DECIMAL(12, 2) NOT NULL,
    category VARCHAR(100) NOT NULL,
    date DATE NOT NULL
);
```

## 配置数据库连接

项目已配置 `UserSecretsId`，在项目目录执行下面的命令，将用户名和密码替换为本机数据库账号。连接信息保存在本地开发密钥中。🔑

```powershell
dotnet user-secrets set "ConnectionStrings:ExpenseDb" "Server=localhost;Port=3306;Database=expense_tracker;User ID=你的用户名;Password=你的密码;"
```

## 启动

在第一个 PowerShell 窗口执行以下命令。`Development` 环境会加载前面保存的开发密钥，监听地址固定为 `http://localhost:5080`。▶️

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-launch-profile -- --urls http://localhost:5080
```

看到 `MySQL connected successfully.` 和 `Now listening on: http://localhost:5080` 后，保持窗口运行，在第二个 PowerShell 窗口测试接口。修改代码后，先保存，再在第一个窗口按 `Ctrl+C` 停止服务并重新启动。🔄

## 接口

| 方法 | 路径 | 功能 | 成功状态码 |
| --- | --- | --- | --- |
| POST | `/transactions` | 新增账单 | 200 |
| GET | `/transactions` | 查询全部账单 | 200 |
| GET | `/transactions?year=2026&month=11` | 按月查询账单 | 200 |
| GET | `/transactions/summary?year=2026&month=11` | 查询月度汇总 | 200 |
| PUT | `/transactions/{id}` | 更新指定账单的完整内容 | 200 |
| DELETE | `/transactions/{id}` | 删除指定账单 | 204 |

POST 和 PUT 使用相同的请求字段，PUT 的账单编号从路径读取。📋

| 字段 | 示例 | 规则 |
| --- | --- | --- |
| `type` | `expense` | `income` 为收入，`expense` 为支出，区分大小写 |
| `amount` | `25.00` | 大于 0，最大 9999999999.99，最多两位小数 |
| `category` | `food` | 非空白，C# 字符串长度不超过 100 |
| `date` | `2026-11-18` | 使用 `YYYY-MM-DD` 格式，年份至少 1000 |

列表查询可省略年月以返回全部账单，筛选时必须同时提供年月。月度汇总必须提供年月，两个接口的年份范围都是 1000–9998，月份为 1–12。余额等于当月收入减当月支出。📅

## 请求示例

新增一笔 25 元支出，记录响应中的 `id`。后续示例假设这个编号为 `1`，执行时换成实际编号。🧾

```powershell
Invoke-RestMethod -Method Post -Uri 'http://localhost:5080/transactions' -ContentType 'application/json' -Body '{"type":"expense","amount":25,"category":"food","date":"2026-11-18"}'
```

```json
{
  "id": 1,
  "type": "expense",
  "amount": 25,
  "category": "food",
  "date": "2026-11-18"
}
```

查询全部账单、11 月账单和月报。列表查询返回 JSON 数组，没有记录时返回 `[]`，空月份汇总的收入、支出和余额均为 0。🔍

```powershell
Invoke-RestMethod -Uri 'http://localhost:5080/transactions'
Invoke-RestMethod -Uri 'http://localhost:5080/transactions?year=2026&month=11'
Invoke-RestMethod -Uri 'http://localhost:5080/transactions/summary?year=2026&month=11'
```

当 11 月只有上述一笔支出时，汇总内容如下。📊

```json
{
  "year": 2026,
  "month": 11,
  "income": 0,
  "expense": 25,
  "balance": -25
}
```

将指定账单的金额修改为 30 元，重复发送相同内容仍会返回成功。✍️

```powershell
Invoke-RestMethod -Method Put -Uri 'http://localhost:5080/transactions/1' -ContentType 'application/json' -Body '{"type":"expense","amount":30,"category":"food","date":"2026-11-18"}'
```

删除指定账单，用 `-i` 查看响应状态码。删除成功返回 204，再次删除该编号返回 404。🗑️

```powershell
curl.exe -i -X DELETE 'http://localhost:5080/transactions/1'
```

## 手动验收

1. 新增账单并查询列表，确认字段和数据库记录一致。✅
2. 停止并重新启动 API，再查询列表，确认数据仍在。💾
3. 修改账单并查询月报，确认金额变化反映在汇总中。📊
4. 查询没有账单的月份，确认列表为空、月报为零。📅
5. 新增时提交金额 `1.234` 或空类别，确认返回 400 且列表没有增加记录。🧪
6. 按月查询只提供年份，或提交月份 13，确认返回 400。🧪
7. 删除一条测试账单，确认返回 204，重复删除返回 404。🗑️

## 常见问题

- 无法连接到远程服务器时，检查 API 启动窗口是否显示监听地址，确认请求端口一致。🔍
- 编译提示 `TodoApi.exe` 被锁定时，在原来的服务窗口按 `Ctrl+C`，停止旧进程后重新启动。🛑
- 出现 `AmbiguousMatchException` 时，检查是否重复注册了相同 HTTP 方法和路径的接口。📋
- 出现 500 时，查看运行 API 的窗口里的异常类型和消息，开发环境会输出异常详情。🔧
- 数据库连接失败时，检查 MySQL 服务、账号、数据库名称以及启动窗口中的 `Development` 环境配置。🔑

## 当前范围与限制

这是单用户本地学习项目，目前没有登录鉴权、分页和统一的数据库异常处理。列表查询会一次返回所有匹配记录，数据库异常可能返回 500。📝

日期校验存在一个待统一的边界，新增和修改接受 9999 年，但月份查询和汇总最高接受 9998 年。当前演示数据请使用 1000–9998 年范围内的日期。📅
