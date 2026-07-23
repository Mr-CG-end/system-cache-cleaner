# M02 修复复核记录

- 复核日期：2026-07-23
- 复核 Agent：Codex
- 模块：M02 白名单目录表与安全边界
- 当前结论：通过

## 已完成修复

1. 白名单目录加载前校验路径非空且为绝对路径；系统特殊目录无法解析时直接停止加载，避免退化为相对于程序工作目录的路径。
2. 普通模式和演示模式测试均增加白名单绝对路径断言。
3. 增加目录符号链接/junction 越界回归测试，验证白名单内部链接指向外部目录时 `PathSafety.IsPathSafe` 必须拒绝。
4. 测试优先使用 .NET 目录符号链接；权限不允许时在 Windows 上回退创建 junction，只有两种方式均不可用时才将用例标记为环境受阻。

## 自动验证

- M02 定向测试：
  - 命令：`dotnet test tests\SystemCacheCleaner.Tests\SystemCacheCleaner.Tests.csproj --no-restore --filter "FullyQualifiedName~CacheCatalogTests|FullyQualifiedName~PathSafetyTests"`
  - 结果：通过，8/8 测试通过，0 失败，0 跳过。
- 完整回归测试：
  - 命令：`dotnet test SystemCacheCleaner.sln --no-restore`
  - 结果：通过，17/17 测试通过，0 失败，0 跳过。

## 验收结论

- AC-M02-01：普通模式恰好返回三个约定类别，默认选择正确，且所有白名单根目录均为绝对路径。
- AC-M02-02：业务代码不存在任意路径输入、外部清理命令或静默提权配置。
- AC-M02-03：演示模式持续显示提示，三个类别映射到固定的 `DemoCache` 子目录。
- AC-M02-04～07：白名单内文件通过；相似前缀、根目录自身和规范化后越界的 `..` 路径均被拒绝。
- AC-M02-08：目录符号链接/junction 指向白名单外部时，安全检查正确拒绝，自动化测试通过且未跳过。
- AC-M02-09：当前业务代码不存在删除入口、Shell 调用和提权配置。

