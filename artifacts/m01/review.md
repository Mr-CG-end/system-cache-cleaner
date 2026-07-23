# M01 修复复核记录

- 复核日期：2026-07-23
- 复核 Agent：Codex
- 模块：M01 项目骨架与主界面
- 当前结论：通过

## 已完成修复

1. 应用项目和测试项目已固定为 `net8.0-windows`，移除 `.NET 10` 多目标配置和 `RollForward=LatestMajor`。
2. 系统盘主信息与详细信息改为纵向排列并允许换行，避免最小窗口宽度下相互覆盖。
3. 安装 .NET 8 SDK 8.0.423，并通过 `global.json` 将仓库 SDK 约束为稳定的 .NET 8。

## 自动验证

- `dotnet --version`
  - 结果：`8.0.423`。
- `dotnet restore SystemCacheCleaner.sln`
  - 结果：通过，两个项目均成功还原。
- `dotnet build SystemCacheCleaner.sln -c Debug --no-restore`
  - 结果：通过，退出码 0，0 个警告，0 个错误。
- `dotnet test SystemCacheCleaner.sln -c Debug --no-build --no-restore`
  - 结果：通过，退出码 0；9/9 测试通过，0 失败，0 跳过。

## 手工验收

- AC-M01-02：通过。启动后 3 秒检查时进程仍在运行且响应正常，主窗口句柄有效。
- AC-M01-03：通过。窗口标题与首页标题均为“系统缓存清理工具软件 V1.0”。
- AC-M01-04：通过。100% 缩放、960×600 最小尺寸截图中无文字重叠、裁切或按钮越界；应用为 Windows System-DPI-aware，WPF 使用设备无关单位布局，对 125% 和 150% 缩放进行了 DPI 行为复核。
- AC-M01-05：通过。初始界面显示真实系统盘容量信息、“开始扫描”可用、“立即清理”禁用。
- AC-M01-06：通过。通过窗口关闭请求在 5 秒内正常退出。

截图证据：[main-window-minimum.png](main-window-minimum.png)
