<p align="center">
  <a href="README.md">English</a> · <strong>简体中文</strong>
</p>

# Cedro Modern Dock Rev

> 基于 [Cedro Modern Dock](https://github.com/Cedro-Software/cedro-modern-dock) 的 Windows Dock / 快速启动增强分支。
>
> **Rev v1.4.0** 进一步增强了 Bottom AppBar 在多显示器与 DPI 变化场景下的稳定性，固定了运行指示器相关的 Dock 高度，修复了设置窗口的层级问题，并全面重构了设置中心界面；项目继续保留原有 GPL-3.0 许可证及上游项目署名。

![.NET](https://img.shields.io/badge/.NET-9-5122d3?style=for-the-badge&logo=dotnet&logoColor=white)
![Avalonia](https://img.shields.io/badge/Avalonia-11.3-0080ff?style=for-the-badge&logo=avalonia&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![License](https://img.shields.io/github/license/Andrew-Hello/cedro-modern-dock-rev?style=for-the-badge)

Cedro Modern Dock Rev 保留了上游项目轻量、直观的 Dock / 快速启动体验，并进一步加入 Windows 桌面集成、类似任务栏的运行程序管理、边缘贴靠、应用身份识别、脚本启动器、配置备份/恢复以及灵活的逐项目图标覆盖能力。

## Rev v1.4.0 亮点

### Bottom AppBar 自动适应显示器与 DPI 变化

Rev v1.4.0 进一步提升了 Bottom AppBar 在不同分辨率、不同 DPI 缩放显示器之间切换时的适应能力。

- Bottom AppBar 激活时，Cedro 会监听原生 `WM_DISPLAYCHANGE` 和 `WM_DPICHANGED` 事件。
- 分辨率、显示器拓扑以及 DPI 变化会先经过防抖处理，再执行 Windows Shell 工作区操作。
- Cedro 会先解除旧显示环境中的 AppBar 保留区，再重新识别当前显示器和任务栏边界。
- 随后只为新的显示环境创建一个新的 Bottom AppBar 保留区。
- 重绑定期间会暂停普通高度更新，避免 Avalonia 因新 DPI 引发的布局事件错误更新旧显示器上的 AppBar。
- 有意不使用 `WM_SETTINGCHANGE` / `SPI_SETWORKAREA` 作为重绑定触发条件，避免 Cedro 对自己引起的 Work Area 变化再次作出响应而形成反馈环。

这对于在高 DPI 笔记本屏幕与大尺寸外接显示器之间频繁切换尤其有用。

### 稳定的运行指示器几何布局

运行指示器的小白点不再改变 Dock 窗口高度。

- 无论程序是否正在运行，每个图标都会预留固定的运行指示器区域。
- 白点出现时，仅让图标在已经预留的区域内轻微上移。
- 启动或关闭应用程序不会再改变 Dock / AppBar 的总高度。
- 开关全局“显示运行指示器”选项时，Dock 总高度同样保持不变。

这样，最大化窗口的工作区和 Bottom AppBar 的位置不会再仅仅因为运行状态白点的出现或消失而上下跳动。

### 全新的设置中心界面

设置窗口已经从原先不断横向扩展的顶部选项卡布局，重构为更适合桌面工具的信息架构。

- 左侧导航栏取代原来的顶部横向选项卡。
- 右侧内容区采用受限宽度，不会在 2K / 4K 显示器上无限摊开。
- 默认窗口大小为 1000×700，并提供合理的最小/最大缩放范围。
- 每个设置页面拥有独立的滚动区域。
- Dock 项目页面采用独立项目列表，以及紧凑的“操作”和“自定义图标”卡片。
- Dock 外观、图标外观和常规设置均改为更紧凑的卡片 / 网格布局。
- XAML 现在直接定义最终页面结构，不再先创建旧页面，再在 `Opened` 后动态插入并重排控件。

### 设置窗口层级修复

设置窗口不再作为 Always-on-top Dock 主窗口的 owned child 打开。

这修复了长期存在的 Windows owned-window Z-order 问题：以前 Settings 有可能因为 Dock 的置顶属性而持续压在自己打开的子窗口上方。现在 Windows 系统图标库、颜色选择器、Windows 模块窗口等由 Settings 打开的窗口都可以自然显示在 Settings 上方。

## Rev v1.3.0 亮点

### 独立的 Bottom AppBar 定位模式

Rev v1.3.0 在静态定位和动态定位之外，加入了第三种独立定位模式：**Bottom AppBar**。

- **静态定位**和**动态定位**永远不会注册 Windows AppBar。
- **Bottom AppBar** 是一个刻意收敛功能边界的 Shell 集成模式，仅用于屏幕底部。
- Dock 始终保持水平排列，并直接贴在 Windows 任务栏 / Work Area 上边界。
- 只提供三个水平位置：**左下 / 中下 / 右下**。
- 此模式下禁止自由拖动和屏幕边距调节。
- 四边自动隐藏与 Bottom AppBar 完全隔离，不会同时运行。
- 离开 Bottom AppBar 或退出 Cedro 时，会立即释放 Windows 工作区保留区域。

因此普通最大化窗口会停止在 Dock 上边缘，而不会继续铺到 Always-on-top Dock 的下面。

### 保守且稳定的 AppBar 生命周期

Bottom AppBar 的设计目标是尽量避免反复重新协商 Windows 工作区。

- 进入该定位模式时只发送一次 `ABM_NEW`。
- 合法的底部边界只在初始注册时，与现有任务栏 / 其他 AppBar 协商一次。
- 后续 Dock 高度变化只更新同一个 AppBar 保留区，不重复注册。
- 已经被 Cedro 缩小过的 Windows Work Area 不会再次参与 AppBar 定位计算，从而避免递归内缩、嵌套或多层保留区。
- 不使用周期性的 AppBar Shell 轮询。
- 不使用 `ABN_POSCHANGED` 反馈循环。
- 打开或关闭设置窗口不会注册、解除或重新查询 AppBar。
- 旧配置里的 `reserveDesktopSpace` 不再能够激活 AppBar；只有明确选择 `BOTTOM_APPBAR` 定位模式时才会产生 AppBar 保留区。

### 与任务栏共存及动态高度兼容

- Bottom AppBar 会在 Cedro 修改 Windows Work Area 之前先完成初始边界协商，确保任务栏始终是硬性的最下边界。
- Dock 始终位于任务栏正上方，不会与任务栏争夺或重叠同一个屏幕边缘。
- Cedro 监听 Dock 实际的 SizeToContent 高度，并使用短防抖更新已有 AppBar 保留区。
- 图标大小、Dock 上下内边距以及其他真实布局高度变化都可以安全更新同一个 AppBar。
- 左 / 中 / 右定位变化只移动 Dock 的 X 位置，不改变 Windows 工作区保留高度。

Rev v1.3.0 的 Bottom AppBar 是在更通用的 AppBar 实验基础上重新设计的。测试证明，自由四边定位与 Shell AppBar 保留区混用容易导致递归 Work Area 变化，因此正式版本将功能边界明确收敛为单一、可预测的底部 AppBar 合约。

## Rev v1.2.0 亮点

### BAT / CMD / VBS 脚本启动器

- 可通过 **设置 → 图标 → 添加脚本（.bat/.cmd/.vbs）** 直接固定 Windows 脚本。
- `.bat`、`.cmd` 和 `.vbs` 与普通程序项目一样，可以排序、删除、导入导出配置以及指定自定义图标。
- 脚本按照 Windows Shell 默认关联方式启动，行为与资源管理器双击一致。
- Cedro 会显式把脚本所在目录设为 Working Directory，便于 BAT/VBS 包装器继续调用同目录的 `.ps1` 或其他相对路径资源。
- 新加入的脚本会自动提取 Windows 关联文件类型图标，而不是显示空白图标。

### Windows 系统图标中心

自定义图标选择器不再局限于 `SHELL32.dll`，而是可以浏览一组精选的 Windows DLL / EXE 图标资源库。

内置资源目录：

- **常用** — `SHELL32.dll`、`imageres.dll`
- **设备** — `DDORes.dll`、`setupapi.dll`、`compstui.dll`
- **网络** — `netshell.dll`、`netcenter.dll`、`networkexplorer.dll`
- **经典** — `moricons.dll`、`pifmgr.dll`
- **其他** — `explorer.exe`、`mmres.dll`、`wmploc.dll`

当前 Windows 版本中不存在的资源文件会自动跳过。选择资源库后，Cedro 会将其中可提取的图标以缩略图网格显示，点击即可应用到当前 Dock 项目。

系统图标覆盖采用轻量方式保存：记录资源路径表达式和图标索引，例如：

```json
{
  "customSystemIconSource": "%SystemRoot%\\System32\\imageres.dll",
  "customSystemIconIndex": 15
}
```

Cedro 在运行时展开 `%SystemRoot%` 并动态提取图标，不会把 Microsoft 系统图标的二进制内容复制进 Cedro 配置文件或持久缓存。

需要注意，图标索引属于 Windows 资源内部实现细节，不同 Windows 大版本理论上可能发生变化。如果跨电脑迁移时要求图标视觉完全一致，建议使用 PNG/ICO 自定义覆盖。

### 自定义图片 / 图标覆盖

- 可以在 **设置 → 图标** 中为任意固定 Dock 项目指定自己的图标。
- 支持 PNG、ICO、JPG/JPEG、BMP、GIF 和 TIFF。
- 导入图片会统一转换成 PNG，并以 Base64 嵌入 `config.json`，因此配置导出/导入时可以自包含迁移。
- Windows 系统图标覆盖和导入图片覆盖互斥，选择一种会自动清除另一种。
- **恢复默认图标** 会清除任一覆盖来源，并重新使用 Cedro 的自动图标解析流程。
- 支持普通程序、脚本、Windows 打包应用、Edge/Chromium PWA、文件夹、Windows 模块以及 Cedro 设置项目。
- Rev v1.1.0 的旧 Base64 自定义图标配置继续兼容。

### 窗口与桌面行为

- 真正的透明顶层 Dock 合成：Dock 背景透明时不会遮挡桌面图标和文字。
- 可选 **总在最前**。
- 当其他程序或游戏进入真正全屏时，Cedro 会自动暂停 TopMost，退出全屏后再恢复。
- Rev v1.3.0 及之后提供三种定位模式：静态、动态和 Bottom AppBar。
- 普通静态/动态工作流仍支持四边贴靠与自动隐藏：
  - 上 / 下边缘可以与左 / 右边缘分别启用。
  - 动态模式支持拖动到边缘自动磁吸。
  - 底边贴靠会尊重 Windows 任务栏 Work Area，不覆盖任务栏。
  - Dock 隐藏后保留可点击 / 悬停唤出的可见“小尾巴”。
  - 尾巴只在隐藏动画完成后出现，避免跟随动画移动造成视觉干扰。

### 外观与交互

- 可调节 Dock 上下内边距，实现更薄的 Dock 条带。
- 可开关运行指示器白点。
- 可开关悬停放大。
- 可开关悬停程序名称标签。
- 可开关实时窗口预览。
- 在当前定位模式允许的前提下，仍保留透明度、颜色、圆角、间距、图标大小、染色、横向/纵向排列等上游自定义能力。

### 运行程序与固定

- 可以显示当前正在运行但尚未固定到 Dock 的应用程序。
- 右键未固定运行程序可选择 **固定到 Dock**。
- 也可以直接把未固定运行程序拖入固定区域完成固定。
- 支持普通 Win32 桌面程序。
- 支持通过 Windows Application User Model ID（AUMID）识别和启动的打包应用，包括没有普通 `.exe` 启动入口的 Windows 应用。
- 对 Edge/Chromium 安装的 Web App（PWA）提供尽力而为的独立身份识别，使其尽量不被简单归并为浏览器进程。
- 窗口预览、激活与运行状态跟踪同样能够理解打包应用的窗口托管结构。

## 安装

推荐使用最新 **Cedro Modern Dock Rev** GitHub Release 中附带的 Windows x64 便携包：

1. 打开本仓库的 **Releases** 页面。
2. 下载 `CedroModernDock-Rev-v1.4.0-win-x64.zip`（或更新版本）。
3. 解压到普通可写目录。
4. 运行 `CedroModernDock.exe`。

测试新版本前请先退出已经运行的 Cedro，因为程序使用了单实例保护机制。

## 配置、备份与迁移

当前生效配置位于：

```text
%APPDATA%\CedroModernDock\config.json
```

设置窗口中提供 **配置与备份** 功能：

- **打开备份文件夹**
- **导出配置...**
- **导入配置...**

导入配置之前，Cedro 会自动把当前配置备份到：

```text
%APPDATA%\CedroModernDock\Backups\
```

导入成功后 Cedro 会自动重启，使新配置立即生效。

迁移到另一台电脑时建议通过 JSON 导出/导入。PNG/ICO 自定义图标会直接包含在配置中；Windows 系统图标则以 `%SystemRoot%` 资源路径 + Icon Index 的方式迁移。普通程序和脚本仍要求目标机器存在兼容的文件路径；Windows 打包应用和支持的 PWA 会尽量使用它们的 Windows 应用身份。

## 固定 Windows 应用和 PWA

对于没有普通 `.exe` 路径的程序（例如很多 Windows 打包应用）：

1. 从 Windows 正常启动该应用。
2. 等待它出现在 Dock 的未固定运行程序区域。
3. 右键选择 **固定到 Dock**，或者直接拖入固定区域。
4. 关闭应用，再测试新固定项目是否可以重新启动。

Cedro 会保存 Windows 应用身份，并在适用时通过 Windows AppsFolder Shell target 启动打包应用。

如果自动解析到的图标不理想——浏览器安装的 PWA 尤其常见——可以在 **设置 → 图标** 中选择该固定项目，再导入自定义图片/ICO，或者从 Windows 系统图标库中选择图标。

## 支持语言

保留上游项目的多语言体系，包括英语、葡萄牙语（巴西）、西班牙语、法语、德语、日语、简体中文、繁体中文、印地语、阿拉伯语、孟加拉语、俄语、乌尔都语、印度尼西亚语、尼日利亚皮钦语、马拉地语、泰卢固语、土耳其语、泰米尔语、粤语和越南语。

Rev 分支新增功能目前提供完整的英语和简体中文文本；其他语言在尚未翻译时可以回退到英语。

## 项目架构

当前项目使用 **C# / .NET 9 / Avalonia 11.3**，采用分层架构：

- `CedroModernDock.Core` — 领域模型、应用服务、配置契约与国际化。
- `CedroModernDock.Infrastructure.Windows` — Win32 / DWM / Shell 集成、系统/自定义图标提取、应用身份识别、JSON 持久化、注册表自启动与托盘行为。
- `CedroModernDock` — Avalonia UI、Dock 主窗口、设置窗口、ViewModel 和视觉行为。
- `CedroModernDock.Tests` — xUnit 测试。

`App.axaml.cs` 负责组装依赖并注入 UI / 应用层。

## 本地编译

前置要求：

- Windows 开发环境
- .NET 9 SDK（或兼容的 .NET 10 SDK）
- Git
- Visual Studio 2022 / Rider / VS Code 可选

在 `dotnet` 目录运行：

```powershell
dotnet restore
dotnet build src/CedroModernDock -c Release
```

构建输出位于：

```text
dotnet/src/CedroModernDock/bin/Release/net9.0-windows/
```

开发时也可以直接运行：

```powershell
dotnet run --project src/CedroModernDock
```

## 稳定基线与开发分支

- `main` — 仓库主页展示的当前稳定 Rev 源码。
- `cedro-enhanced-window` — 当前 Rev 开发分支。
- `stable/rev-v1.4.0` — 冻结的 Rev v1.4.0 基线。
- `stable/rev-v1.3.0` — 冻结的 Rev v1.3.0 基线。
- `stable/rev-v1.2.0` — 冻结的 Rev v1.2.0 基线。
- `stable/rev-v1.1.0` — 冻结的 Rev v1.1.0 基线。
- `stable/rev-v1.0.0` — 首个冻结的 Rev 稳定基线。
- 最新 Release tag：`rev-v1.4.0`。

冻结稳定基线与开发分支分离，以便未来继续实验和开发，同时始终保留已知可用的回退点。

## 上游项目与许可证

本仓库是 **Cedro Modern Dock** 的社区增强分支。核心应用与架构来源于原项目及其作者。

上游项目：

- https://github.com/Cedro-Software/cedro-modern-dock

本分支继续使用与上游一致的 **GPL-3.0** 许可证。详情参见 `LICENSE`。

## Rev 更新日志

增强分支的版本历史请参阅 [CHANGELOG-REV.md](CHANGELOG-REV.md)。
