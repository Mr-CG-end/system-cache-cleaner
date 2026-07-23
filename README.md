# 系统缓存清理工具软件 V1.0 (System Cache Cleaner)

![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue)
![Architecture](https://img.shields.io/badge/Architecture-WPF%20%7C%20MVVM-green)

## 项目简介

**系统缓存清理工具软件 V1.0** 是一款基于 .NET 8 WPF 框架开发的轻量级、可视化 Windows 系统缓存清理应用。应用采用严格的白名单目录机制与重解析点 (Junction / 符号链接) 防护，确保在不破坏系统稳定性、无静默提权的前提下安全清理常见系统缓存。

## 使用文档

- [Markdown 用户操作手册](docs/系统缓存清理工具软件V1.0-操作手册.md)
- [Word 用户操作手册](output/doc/系统缓存清理工具软件V1.0-用户操作手册.docx)

---

## 核心特性

- 🛡️ **严格安全隔离**：只允许在硬编码的白名单根目录下进行清理，严禁根目录删除、包含 `..` 相对路径以及重解析点跨目录防护。
- ⚡ **只读安全扫描**：枚举扫描阶段严格执行只读遍历，支持中途响应取消。
- 📊 **预估与确认**：清理前提供准确的释放空间预估与二次确认模态对话框。
- 📋 **清理报告与重新扫描**：清理完成后详细列出成功、跳过与失败项日志摘要，支持一键闭环重新扫描。
- 🔒 **无 UAC 提权要求**：以普通用户权限运行，独占锁定或无权限文件自动跳过并记录。
- 🧪 **内置演示模式 (`--demo`)**：提供受控的隔离测试模式，方便在不修改系统真实文件的前提下演示完整清理流程。

---

## 环境要求

- **操作系统**：Windows 10 / Windows 11 (x64)
- **开发与构建环境**：.NET 8 SDK
- **运行发布版**：.NET 8 Desktop Runtime（当前发布方式为框架依赖发布，目标电脑必须安装）

---

## 构建与测试指南

### 1. 编译解决方案 (Debug 模式)
```powershell
dotnet build SystemCacheCleaner.sln -c Debug
```

### 2. 执行自动化单元测试
```powershell
dotnet test SystemCacheCleaner.sln -c Debug
```

### 3. Release 模式编译
```powershell
dotnet build SystemCacheCleaner.sln -c Release
```

### 4. 启动普通模式
```powershell
.\src\SystemCacheCleaner\bin\Debug\net8.0-windows\SystemCacheCleaner.exe
```

---

## 演示模式与测试数据

为了便于安全演示与测试，项目内置了受控的演示数据准备脚本与启动参数。

### 1. 初始化演示测试数据
在 PowerShell 中运行根目录的准备脚本：
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Prepare-DemoData.ps1
```
*该脚本将在 `%LOCALAPPDATA%\SystemCacheCleaner\DemoCache\` 的子目录中生成样例测试文件。*

### 2. 启动演示模式应用
```powershell
.\src\SystemCacheCleaner\bin\Debug\net8.0-windows\SystemCacheCleaner.exe --demo
```
*在演示模式下，界面顶部将显示醒目的黄色演示标志，所有数据均在 Demo 隔离目录下操作。*

---

## 框架依赖的单文件发布

执行以下标准发布命令，可生成单文件发布可执行程序：

```powershell
dotnet publish src/SystemCacheCleaner/SystemCacheCleaner.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

**产物导出路径**：
`src/SystemCacheCleaner/bin/Release/net8.0-windows/win-x64/publish/SystemCacheCleaner.exe`

直接双击运行或附加 `--demo` 参数启动即可。

> 发布命令使用 `--self-contained false`，因此目标电脑需要预先安装 **.NET 8 Desktop Runtime**。如果目标电脑没有对应运行时，程序不能直接启动。

---

## 项目结构说明

```
system-cache-cleaner-v1.0/
├── docs/                                    # 文档库 (验收文档、操作手册等)
├── src/
│   └── SystemCacheCleaner/                 # WPF 主工程
│       ├── Dialogs/                         # 模态对话框 (确认弹窗、清理报告弹窗)
│       ├── Infrastructure/                  # 基础设施 (安全校验 PathSafety, 字节格式化 ByteSizeFormatter)
│       ├── Models/                          # 领域模型 (Category, ScanResult, CleanupResult)
│       ├── Services/                        # 核心服务 (DiskSpace, Catalog, Scan, Cleanup)
│       └── ViewModels/                      # MVVM 视图模型 (MainViewModel)
├── tests/
│   └── SystemCacheCleaner.Tests/           # MSTest 单元测试工程 (49 项测试)
├── tools/
│   └── Prepare-DemoData.ps1                 # 演示数据初始化脚本
└── SystemCacheCleaner.sln                  # 解决方案入口
```
