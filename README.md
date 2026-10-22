# FuturesTicker

期货行情悬浮窗 —— 实时显示国内商品期货价格的无边框桌面工具，贴任务栏常驻，涨红跌绿一目了然。

![Framework](https://img.shields.io/badge/Framework-.NET%2010-512BD4.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-lightgrey.svg)
![License](https://img.shields.io/badge/License-MIT-blue.svg)

## ✨ 功能特性

- **实时行情**：新浪免费行情接口，默认 3 秒轮询（可选 1/2/3/5/10 秒），支持国内商品期货主力连续合约；
- **悬浮窗**：无边框、置顶、半透明，贴任务栏不挡视线；鼠标穿透可选，拖动可自由调整位置；
- **涨红跌绿**：以昨结算价为基准，符合国内期货配色习惯；
- **主界面**：自选合约管理（添加 / 删除 / 上下移动），34 个常用品种一键快加；
- **分时图**：双击期货合约弹出，价格线 + 均价线 + 昨结虚线，叠加成交量柱状图与持仓量变化趋势线；
- **期权监控**：支持 28 个商品期权品种（上期所 / 大商所 / 郑商所 / 广期所），代码格式 `品种+月份+C/P+行权价`；
- **系统托盘**：低调灰色 ¥ 图标，双击打开主界面，右键菜单快捷切换设置；
- **丰富开关**：涨跌颜色、只显示价格、鼠标穿透、始终置顶、开机自启、显示悬浮窗；
- **可调字号与透明度**：字号 8~20、淡显透明度 30%~75% 实时调节；
- **配置持久化**：JSON 存储于 `%APPDATA%\FuturesTicker\config.json`。

## 🖥 运行环境

| 项目 | 要求 |
| ---- | ---- |
| 操作系统 | Windows 10 / 11 |
| 运行时 | .NET 10 Desktop Runtime（发布安装包为**自包含**，无需额外安装） |

## 🚀 快速开始

### 直接下载

前往 [Releases](https://github.com/yuchuan1943/FuturesTicker/releases) 下载 `FuturesTicker-{版本号}-setup.exe` 安装，或下载源码包自行构建。

### 从源码构建

1. 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)；
2. 克隆仓库：

   ```bash
   git clone https://github.com/yuchuan1943/FuturesTicker.git
   cd FuturesTicker
   ```

3. 构建并运行：

   ```bash
   dotnet run
   ```

   或发布为独立目录（自包含 win-x64，输出至 `bin\Release\net10.0-windows\publish\`）：

   ```bash
   dotnet publish -c Release
   ```

### 制作安装包

项目提供了 [Inno Setup](https://jrsoftware.org/isdl.php) 安装脚本 `installer.iss`，可一键打包为安装程序。

1. 安装 [Inno Setup](https://jrsoftware.org/isdl.php)（安装时勾选中文语言包）；
2. 发布项目：

   ```bash
   dotnet publish -c Release
   ```

3. 用 Inno Setup Compiler 打开 `installer.iss` 点击「编译」，或在命令行执行：

   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\Compil32.exe" /cc installer.iss
   ```

4. 安装包输出至 `installer\FuturesTicker-{版本号}-setup.exe`。

安装包特性：独立部署（免装 .NET Runtime）、简体中文安装界面、开始菜单与卸载入口、可选桌面快捷方式、安装后可选立即启动。

### 自动打包（GitHub Actions）

推送 `v*` 标签（如 `v1.0.0`）会自动触发构建、打包并发布 GitHub Release；也可在仓库 Actions 页手动触发（可填版本号，留空用日期）。

## 📖 使用说明

1. 启动后，悬浮窗贴任务栏显示自选合约的实时价格；
2. **双击悬浮窗**（或双击托盘 ¥ 图标）打开主界面，管理合约与设置；
3. 主界面左栏可添加 / 删除 / 排序合约，右栏调整刷新间隔、字号、透明度及各显示开关；
4. **双击期货合约行**弹出该合约的分时图（价格线、均价线、昨结线 + 成交量、持仓量变化）；
5. 期权合约无分时数据，双击不弹窗；
6. 主界面与分时窗口均可按 `Esc` 关闭。

> ⚠️ 行情数据来自新浪财经公开接口，仅供个人学习与行情展示参考，不构成任何投资建议。

## 🏗 技术架构

- **技术栈**：.NET 10 WPF，零第三方依赖；
- **数据层**：`Services/FuturesDataService.cs` 对接新浪 `nf_`（期货）、`getMinLine`（分时）、`getOptionData`（期权）接口，GBK 解码；
- **模型**：`FuturesQuote`（行情）、`MinutePoint`（分时点）；
- **配置**：`AppConfig` 静态单例，JSON 持久化；
- **悬浮窗**：无边框透明窗口，系统级 `DragMove` 拖拽 + 工作区边界 clamp；
- **分时图**：Canvas 手绘价格线 / 均价线 / 昨结线 / 成交量柱 / 持仓量趋势线；
- **托盘**：`NotifyIcon`（灰色 ¥ 图标）。

## 📁 目录结构

```
FuturesTicker/
├── Converters/        # 涨跌颜色转换器
├── Models/            # 行情、分时点模型
├── Native/            # Win32 P/Invoke、任务栏/工作区辅助
├── Services/          # 行情服务、配置、托盘、自启、品种映射
├── Properties/        # 发布配置（自包含 win-x64）
├── .github/           # GitHub Actions 自动打包工作流
├── app.ico            # 应用图标（exe / 窗口 / 托盘统一）
├── installer.iss      # Inno Setup 安装脚本
└── LICENSE            # MIT 许可证
```

## 📄 许可证

本项目基于 [MIT License](LICENSE) 开源，欢迎自由使用、修改与分发。
