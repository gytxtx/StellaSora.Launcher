# Yostar 双样本协议向量

这些是合成的离线 fixture，不是实际安装、HAR 或在线响应。
`fixture-host`、`fixture-version`、`fixture-basis`、文件与启动参数都是测试输入，不能登记到生产档案。

参考：Stella CN 官方 1.3.0 与 BA JP 官方 1.7.2，取证版本和哈希见
[后续计划](../../../../docs/design/yostar-reference-follow-up-plan-2026-09-30.md)。

- 两包 `node_modules/@launcher/utils/src/crypto.ts`：`getObjectHash` 与 `getAuthHeader`。
- Stella `out/main/index-52e4c114.js:18926–18930`：写入顺序为 `tag,name,version,vc`。
- BA `out/main/index-f057dd97.js` 的 `insertGameConfigFile` 调用：`tag,name,params,version,vc`。
- 两包共有本地 manifest 与文件 vc 格式。

`generate.cjs` 用 Node 内置 crypto 按官方算法独立产生预期值，完全不调用被测 .NET 代码。
运行 `node tests/Cafe.Launcher.Tests/Fixtures/YostarProtocol/generate.cjs` 可再生三个 JSON 文件。
测试读取提交的静态结果，不在运行时再生预期值；BA 既有真实安装固定向量也继续保留。
盐是参考发行包内的公开协议常量，fixture 不含用户数据、Cookie 或在线短期凭据。
