# StellaSora Launcher

面向《星塔旅人》国服（StellaSora CN）的第三方桌面启动器，基于 [Cafe Launcher](https://github.com/bluearchive-cafe/Cafe.Launcher.Avalonia) fork，使用 .NET 10 与 Avalonia 开发。目前用于技术评估与开发验证，尚未完成正式发行验收。

本项目与 Yostar 官方无隶属关系。游戏内容、商标及官方素材的权利归各自权利人所有。游戏官网：[stellasora.yostar.cn](https://stellasora.yostar.cn/)。

[本项目仓库](https://github.com/gytxtx/StellaSora.Launcher) · [后续计划](docs/design/yostar-reference-follow-up-plan-2026-09-30.md) · [官方 API/CDN 取证](docs/research/stella-api-cdn-verification-2026-09-30.md) · [MIT 许可证](LICENSE)

## 当前进度

截至 2026-09-30，已完成官方协议与独立产品身份接入，并在 Windows 实机观察到主页展示和游戏文件下载。

| 项目 | 状态 |
| --- | --- |
| 官方 API、游戏清单、主备 CDN | 已验证 API 响应、HEAD 与 Range 请求，证据及复现方法见取证文档 |
| 官方背景、轮播与公告 | 已在运行界面观察到展示 |
| 游戏下载 | 已观察到实际下载进度；完整安装完成仍待验收 |
| 本地配置与清单 | 已实现 Stella 格式及双游戏协议契约测试；与官方启动器互读仍待实机验收 |
| 产品隔离 | 已切换 Stella 产品文案、游戏档案、数据目录与单实例标识 |
| 游戏首次启动、进程家族与反作弊 | 待实机验收 |
| 启动器自动更新 | 保持关闭，尚未配置独立发行通道 |
| Cafe 镜像、资源面板及专属服务 | 已关闭 |
| 程序集、图标与安装包身份 | 仍沿用上游，正式发行前需要完成迁移 |

仓库已建立，但这不等于已配置发行通道。当前从源码运行；请勿将上游 Cafe 发行包当作本项目的下载入口。

## 已接入能力

- 使用 Stella 国服官方 API 获取游戏配置、清单、公告、轮播与远程背景，下载保留官方主备 CDN。
- 复用上游安装、更新、修复、卸载与下载管线，包括最多 10 路并发、Range 续传、暂停/恢复、限速、临时文件暂存与 CRC64 校验。
- 兼容 Stella 配置中缺少 `params` 的格式，保留原始字段顺序参与校验，并拒绝跨游戏配置与清单。
- 通过独立游戏及产品档案注入身份，保留 Blue Archive/Cafe 参考档案用于协议与界面回归测试。
- 提供简体中文、繁体中文、日语和英语界面，以及主题、背景设置、日志与诊断工具。

上述游戏操作已接入现有实现；完整安装、更新、修复、卸载和游戏运行的端到端兼容性按实机验收结果确认。

## 从源码运行

开发环境需要 [global.json](global.json) 指定的 .NET SDK `10.0.302`，以及 PowerShell 7（`pwsh`）。仓库脚本不支持 Windows PowerShell 5.1。构建启用可空引用类型、编译绑定和警告视为错误。

```powershell
git clone https://github.com/gytxtx/StellaSora.Launcher.git
cd StellaSora.Launcher
```

进入包含 Stella 接入代码的分支后，在仓库根目录执行：

```powershell
.\build.ps1
dotnet run --project .\src\Cafe.Launcher\Cafe.Launcher.csproj
```

当前工程目录、解决方案、程序集与源码命名空间仍使用 `Cafe.Launcher`，因此构建命令中的名称保持不变。

首次运行时通过设置向导选择游戏目录。当前游戏安装目录归一为 `YostarGames\StellaSora_CN`，启动入口为 `xtlr.exe`。接入已有官方安装目录前，请先备份其中的 `game-launcher-config.json` 与 `manifest.json`；状态互读尚在验收计划中。

### 平台状态

| 平台 | 当前范围 |
| --- | --- |
| Windows x64 | 主要开发与验收平台，已观察到实际下载；完整游戏启动链路待验收 |
| macOS / Linux | 保留上游跨平台代码，尚未验证 Stella 的安装、运行与反作弊兼容性 |

上游 Blue Archive 的平台验证记录仅适用于对应游戏与环境，不能视为 Stella 的兼容性结论。

## 本地数据

Windows 启动器数据保存在 `%LOCALAPPDATA%\StellaSora Launcher\`，包括设置、下载状态与日志。该目录与 Cafe Launcher 的数据目录独立。

游戏目录中的 `game-launcher-config.json` 与 `manifest.json` 由安装状态存储协调管理。协议层覆盖了 Stella 配置变体，真实安装与官方启动器的双向识别仍需验收。

## 开发与验证

| 命令 | 用途 |
| --- | --- |
| `.\build.ps1` | 还原依赖并构建 Debug |
| `.\test.ps1` | 运行单元测试与 Headless UI 测试 |
| `.\test.ps1 -Suite Unit` / `.\test.ps1 -Suite Headless` | 运行指定测试套件 |
| `.\test.ps1 -Filter "FullyQualifiedName~VersionComparerTests"` | 运行过滤后的测试 |
| `.\dev.ps1 ui` | 检查 UI 样式契约与 Headless UI |
| `.\scripts\Test-LocalizationContract.ps1` | 检查多语言资源键与格式占位符 |
| `.\coverage.ps1` | 运行覆盖率门禁 |
| `.\verify.ps1` | Debug 构建、覆盖率与 Release 构建的完整验证 |

协议取证脚本 [Test-StellaProtocol.ps1](scripts/Test-StellaProtocol.ps1) 可采集官方 API 与 CDN 的有限只读样本，不下载整包或执行游戏。脚本需要本地官方 CN 启动器解包目录；默认路径为 `E:/Repos/_StellaSora_Gamelauncher/cn_app-32/resources/app`，也可通过 `-OfficialAppDirectory` 指定。输出默认位于 `artifacts/stella-protocol-probe`。

```powershell
pwsh -NoProfile -File .\scripts\Test-StellaProtocol.ps1
```

2026-09-30 接入批次的完整验证通过：Debug/Release 构建零警告，单元测试 2353 通过、15 项平台跳过，Headless 测试 224 通过，Release 资源契约 23 通过。手写 C# 行覆盖率为 86.55%，分支覆盖率为 93.16%。这些结果验证代码与契约，不替代真实游戏安装和启动验收。

### 工程结构

| 项目 | 职责 |
| --- | --- |
| [src/Cafe.Launcher](src/Cafe.Launcher/) | 宿主、进程生命周期、单实例转发与依赖注入组合根 |
| [src/Cafe.Launcher.UI](src/Cafe.Launcher.UI/) | Avalonia 界面、功能 ViewModel、主题、本地化与运行时资源 |
| [src/Cafe.Launcher.Core](src/Cafe.Launcher.Core/) | 游戏协议、安装状态、网络传输、设置与游戏操作 |
| [src/Cafe.Launcher.Updater.Core](src/Cafe.Launcher.Updater.Core/) | 自更新契约与应用实现，当前 Stella 产品未启用 |
| [src/Cafe.Launcher.Updater](src/Cafe.Launcher.Updater/) | Windows 自更新 helper，当前 Stella 产品未启用 |
| [tests/Cafe.Launcher.Tests](tests/Cafe.Launcher.Tests/) | xUnit v3 单元与契约测试 |
| [tests/Cafe.Launcher.HeadlessTests](tests/Cafe.Launcher.HeadlessTests/) | Avalonia Headless UI 与金标截图测试 |

依赖方向为宿主 → UI → Core → Updater.Core；宿主也直接引用 Core 与 Updater.Core。Core 不依赖 Avalonia。网络请求统一通过 `RemoteHttpTransport`，设置写入统一通过 `ISavedSettingsWriter`，游戏文件操作遵循路径校验与运行状态检查。

开发约定见 [AGENTS.md](AGENTS.md) 与 [PROJECT_CONVENTIONS.md](PROJECT_CONVENTIONS.md)，分层与档案设计见 [ADR-042](docs/design/adr/ADR-042-Avalonia启动器按Core与UI程序集分层.md)、[ADR-044](docs/design/adr/ADR-044-游戏与产品身份由档案声明并注入.md)。部分继承文档仍以 Cafe/Blue Archive 为背景，具体差异以本 README、Stella 档案和取证记录为准。

## 后续计划

1. 完成 Windows 隔离安装、官方配置与清单双向识别、暂停续传及中断恢复验收。
2. 验证实际游戏首次启动、进程家族识别与反作弊兼容性。
3. 在双样本证据基础上试点签名、校验、协议模型与序列化的共享库，保持产品策略独立。
4. 完成程序集、图标、安装器和发行资产身份迁移，再配置独立发行通道及自动更新。

详细范围与验收条件见[后续计划](docs/design/yostar-reference-follow-up-plan-2026-09-30.md)。

## 素材与许可

源代码使用 [MIT License](LICENSE)，保留上游版权声明。第三方组件许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。游戏和插画素材不随源代码的 MIT 许可证授权。

- **内置默认壁纸**：继续使用上游收录的 Pixiv 画师 [めるき（Meruki）](https://www.pixiv.net/users/15737611) 作品 [初めてのゲーム](https://www.pixiv.net/artworks/142932674)（ID `142932674`）。运行时文件为 [launcher-background.png](src/Cafe.Launcher.UI/Assets/launcher-background.png)，原始与裁剪素材存放于 [docs/assets/art-sources](docs/assets/art-sources/)。插画版权归原作者所有，仅作为启动器背景使用；用于其他场合需取得作者许可。
- **远程素材**：背景、轮播、标识等来自游戏官方接口，权利归各自权利人所有。远程背景按官方返回的地址与 CRC64 下载并缓存，失败时回退到内置壁纸。
- **自定义背景**：可选择本机图片或文件夹，支持 PNG、JPG、JPEG、BMP、WebP。使用许可由用户自行负责。

感谢 [Cafe Launcher](https://github.com/bluearchive-cafe/Cafe.Launcher.Avalonia) 的原作者与贡献者，以及 Avalonia、CommunityToolkit.Mvvm 等开源项目。

## 参与开发与反馈

本项目地址为 [gytxtx/StellaSora.Launcher](https://github.com/gytxtx/StellaSora.Launcher)。反馈请说明使用的提交或版本、平台、复现步骤，并提供必要的截图或诊断日志。

提交遵循 Conventional Commits，例如 `fix(stella): 修复安装状态读取`。界面文本变更需同步四种语言资源，架构与开发流程变更需同步仓库约定。
