# StellaSora CN 官方 API 与 CDN 只读取证

采样日期：2026-09-30，Asia/Singapore **18:35:30–18:35:33**（元数据保留 UTC）。
开发仓库：Stella fork；对应 [M0 后续计划](../design/yostar-reference-follow-up-plan-2026-09-30.md)。
静态来源：`E:\Repos\_StellaSora_Gamelauncher\cn_app-32\resources\app` 的官方 **1.3.0** 发行包。
在线来源：该包声明的官方 API，及 API 返回的主备 CDN；未启动官方程序或游戏。

本轮证明 CN 档案可以使用官方 1.3.0 的签名版本取得配置、清单和有限游戏文件范围。
结果不能代替完整安装、官方状态互读、反作弊、游戏会话或非 Windows 兼容验收。

## 1. 静态契约与证据定位

以下路径均以该官方 `resources/app` 为根，文件哈希记录在
[official-source-hashes.json](samples/stella-2026-09-30/official-source-hashes.json)。

| 材料 | 定位 | 可确认内容 |
| --- | --- | --- |
| `package.json` | version | 官方启动器版本为 `1.3.0` |
| `out/main/index.js` | 866、890–896 | API base 为 `https://launcher-api.yostar.net`；`gameId=StellaSora_CN`，salt=`872550AD59A235662C5B7D5F88CEBE4B`；签名版本来自官方应用版本 |
| `node_modules/@launcher/utils/src/crypto.ts` | `getAuthHeader` | 有序 `head={game_tag,time,version}`；`time=floor(Date.now()/1000)`；`sign=hex(md5(JSON.stringify(head)+body+salt))`，GET 的 body 是空字符串 |
| `out/renderer/assets/main-2d82028d.js` | 26–40 | `game/config`、`base/config`、`social/media/resource` 官方读取端点 |
| `out/main/index.js` | 930–947 | 清单地址端点 `game/config/json?version=…&file_path=…`，CDN 端点 `advanced/game/download/cdn` |
| `out/main/index-52e4c114.js` | 108–139、18973–18976 | worker 拆分 source 与 file.path 的非空路径段，最后一段 `encodeURIComponent`，设置 CDN URL 的 pathname；文件 GET 带 Range，下载本身不带 API Authorization |
| `out/main/index-52e4c114.js` | 18875–18884 | 本地 game config 的输入顺序仍为 `tag,name,version`，计算 vc 后写出；**没有 params 字段** |
| `out/renderer/assets/GameControl-48cc65aa.js` | 78–80 | 官方“前往官网”使用 `https://stellasora.yostar.cn` |

主进程 SHA-256：`d96b0824409d7ccc78ad59b7facf99beab07e95740605e4eae6f470ecd2a9a38`。
worker SHA-256：`d40eb4aa7deb0b8f16d1f3cf492fe9b6fb6cf40c675d4867c1da66511b784eb0`。
“不写 params”只描述这一已取得的 1.3.0 包；不推断没有取得的新官方版本。

## 2. 在线配置

所有接口使用官方 `head.version=1.3.0`、`game_tag=StellaSora_CN`。
本轮每个接口均返回 **HTTP 200，业务 code 200，msg OK**。请求时刻与响应原文哈希见
[evidence.json](samples/stella-2026-09-30/evidence.json)；最小公共响应分别保存如下。
格式化 JSON 样本与网络原始字节不同，证据中的 SHA-256 是**网络原始字节**的哈希。

| 官方 GET 来源 | 已观察的值 | 保存样本 |
| --- | --- | --- |
| [game/config](https://launcher-api.yostar.net/api/launcher/game/config) | 最低/最新版本均 `1.13.0`；`game_start_exe_name=xtlr`；`game_latest_file_path=prod/ZIP_TEMP/StellaSora_CN_TEMP/StellaSora_CN-1.13.0-game.zip`；没有 `game_start_params`；`decompression_size=18GB` | [game-config.json](samples/stella-2026-09-30/game-config.json) |
| [base/config](https://launcher-api.yostar.net/api/launcher/base/config) | 背景图、CRC64、协议/隐私地址；响应还保留预下载提示文本，但本轮不据此判断上线时间 | [base-config.json](samples/stella-2026-09-30/base-config.json) |
| [installation/config](https://launcher-api.yostar.net/api/launcher/installation/config) | 安装背景图、开启协议选项、公共协议 HTML | [installation-config.json](samples/stella-2026-09-30/installation-config.json) |
| [advanced/game/download/cdn](https://launcher-api.yostar.net/api/launcher/advanced/game/download/cdn) | `primary_cdn=https://game-launcher-ss-cn.yostar.net`；`back_up_cdn=https://game-launcher-ss-cn-bk.yostar.net` | [cdn-config.json](samples/stella-2026-09-30/cdn-config.json) |
| [social/media/resource](https://launcher-api.yostar.net/api/launcher/social/media/resource) | “官网”的 `jump_url=https://stellasora.yostar.cn/`，与官方包静态地址一致 | [social-media-resource.json](samples/stella-2026-09-30/social-media-resource.json) |

背景原始字段：

| 字段 | 本轮值 |
| --- | --- |
| `launcher_background_img` | `https://game-launcher-ss-cn.yostar.net/launcher_background_img/ad0b5c35936ecb249b38abd4abb3b344.jpg` |
| `launcher_background_img_crc64` | `8722612434055043732` |
| `installer_background_img` | `https://game-launcher-ss-cn.yostar.net/installer_background_img/141ee5f2971cc127db45d03c1b0152ea.png` |
| `user_agreement` | `https://stellasora.yostar.cn/agreement` |
| `privacy_policy` | `https://stellasora.yostar.cn/privacy_policy` |

本轮背景字段均为绝对 URL；不需要补相对前缀。
重新搜索该官方包未取得 `/prod/StellaSora_CN/…` 的相对背景前缀字面量，
旧研究中的此项不作为已验证的生产 `PackageAssetPrefix` 值。
官网网页亦只读访问成功，标题为“《星塔旅人》官方网站 - 爱与希望，就在路上”。
地址来自上述 API 和包内代码，而非根据游戏名拼接。[官方页面](https://stellasora.yostar.cn/)

## 3. 清单与文件名称

把本轮 `game_latest_version` 与 `game_latest_file_path` 传给官方
`GET /api/launcher/game/config/json`，返回
[manifest-location.json](samples/stella-2026-09-30/manifest-location.json) 中的公共 URL：
[1.13.0 官方清单](https://game-launcher-ss-cn.yostar.net/zip_online_config_json/1.13.0rUzo0gbw.json)。
随后不附 API Authorization 读取该 URL，返回 HTTP 200；不构造签名 CDN 地址。

| 项目 | 观察值 |
| --- | --- |
| 原始 body 大小 | 1,072,682 bytes |
| 原始 body SHA-256 | `c2a0f30e46d0c00d538eacf013c5c91c1e7f64623d7002f9da13f0c4fa088a19` |
| 根字段 | `source`、`file`（文件数组键为单数） |
| `source` | `/StellaSora_CN-1.13.0-game` |
| 文件项字段 | `path`、`hash`、`size`；hash/size 均为十进制字符串 |
| 文件数量 | 8,183 |
| 清单 size 合计 | 20,297,831,850 bytes，约 18.90 GiB；这是清单文件合计，不等同实际安装/游戏下载后的完整目录大小 |
| `Executable` / `Script` | 远端清单没有这些字段；API 启动名是 `game_start_exe_name` |

只保存了带原始摘要和数量的 [manifest-selected.json](samples/stella-2026-09-30/manifest-selected.json)，
其中 file 是前三个原始条目，另列出全部 `.exe` 条目；它**不是可安装的完整清单**。

| 清单可执行路径 | size | CRC64 hash |
| --- | --- | --- |
| `/xtlr.exe` | 687488 | `18094590205132865541` |
| `/UnityCrashHandler64.exe` | 1185200 | `1187912115009684763` |
| `/AntiCheatExpert/ACE-Service64.exe` | 3493272 | `8497848642197881968` |
| `/AntiCheatExpert/ACE-Setup64.exe` | 1069392 | `11851761189204996280` |
| `/xtlr_Data/Plugins/x86_64/BLPlatform64/PCGamePlatform.exe` | 7322072 | `10894313443635886374` |
| `/xtlr_Data/Plugins/x86_64/BLPlatform64/game_security_protection.exe` | 5639128 | `2155727590044816657` |

全部 8,183 条目中未找到 `.bat`、`.cmd`、`.ps1` 或 `.sh` 路径，不能给 Stella 填一个假启动脚本名。
`xtlr.exe` 是 API 启动名和清单交叉支持的入口；其他文件名只证明包内存在，
不证明其进程启动关系、寿命、反作弊运行行为或 Linux 可运行性。
Cookie 库路径、游戏 UID 数据格式与实际进程家族本轮均**未知**。

## 4. 主备 CDN 有限文件读取

按官方 worker 的路径规则，从 CDN、source、`/xtlr.exe` 得到：

- [主 CDN 文件](https://game-launcher-ss-cn.yostar.net/StellaSora_CN-1.13.0-game/xtlr.exe)
- [备 CDN 文件](https://game-launcher-ss-cn-bk.yostar.net/StellaSora_CN-1.13.0-game/xtlr.exe)

对两者分别先 HEAD，再 `GET Range: bytes=0-4095`。两者结果相同：

| 请求/字段 | 主 CDN | 备 CDN |
| --- | --- | --- |
| HEAD 状态 | 200 | 200 |
| HEAD Content-Length | 687488 | 687488 |
| HEAD Accept-Ranges | bytes | bytes |
| `x-oss-hash-crc64ecma` | `18094590205132865541` | `18094590205132865541` |
| Range GET 状态 | 206 | 206 |
| Content-Range | `bytes 0-4095/687488` | `bytes 0-4095/687488` |
| 读取量 | 4096 bytes | 4096 bytes |
| 前 4096 bytes SHA-256 | `b8322e2ec1839b7cd4bda43f30c4b0ec2cea692a1d4f9fffc5fb3abda9979d36` | 同左 |
| 前两个字节 | `4D5A`（MZ） | `4D5A`（MZ） |

HEAD 的大小/服务端 CRC64 与清单一致；主备前 4096 bytes 相同。
未下载完整可执行文件，因此这不表示本地验证了整文件 CRC64 或完整文件一致性。
读取到的二进制前缀未作为 EXE 或二进制文件保存，只存摘要与 16 bytes 十六进制前缀。
本轮没有出现 `NoSuchKey`；历史未稳定复现的错误不应推导为私有桶、必须签名或下载协议不可用。

## 5. 可重复方法与限制

使用 [Test-StellaProtocol.ps1](../../scripts/Test-StellaProtocol.ps1)：

```powershell
pwsh -NoProfile -File .\scripts\Test-StellaProtocol.ps1
```

默认输出到 `artifacts/stella-protocol-probe`；另存一次观测使用
`-OutputDirectory <采样目录>`。本次归档目录为 `docs/research/samples/stella-2026-09-30`。
脚本需要 PowerShell 7 与网络，不依赖生产 C#；探测专用 HttpClient 仅在此只读研究脚本里生存，
不改变应用的 `RemoteHttpTransport`/`HttpClientFactory` 约束。

脚本逐请求重新生成官方签名，不输出或保存带时刻的 Authorization，
只允许已观察到的三个 HTTPS 主机、禁自动跳转，并限制 API body 为 256 KiB、清单为 4 MiB、
每个游戏文件响应最多读取 4096 bytes；文件响应若忽略 Range 也会在读取上限停止。
数据只写到指定证据目录，不写 `game-launcher-config.json`、`manifest.json`、用户配置或真实安装目录。
失败元数据保留状态/异常类型，存储 XML 错误只保存公开 Code；不记录请求头或用户数据。

本轮脚本退出码 0，六个 API 请求和清单 GET 均成功，两个 HEAD 与两个 Range 成功，
共 11 个只读请求。脚本语法由 PowerShell 7 执行验证；样本 JSON 可解析。
没有生产或测试 C# 变更，也没有把在线响应加入默认离线 CI。

后续必须另验：官方/Cafe 状态互读、完整下载续传与 CRC64、写入中断、真实启动与进程家族、
反作弊和平台兼容。本轮记录不能宣布 Stella 正式支持或发行完成。
