# V04 Schema1 兼容测试数据

此目录是合成测试案例，不是工业CT或精度标定证据。

- 2026-10-29在修改制品Schema之前，使用`2386b7d4`已构建的原CT3D正式DLL及MEM API生成，未手写制品二进制。
- `source.raw`：12³ UInt16小端，内部2³体素块原值1700，Spacing 1mm、Origin (20,30,40)，构造可信来源Frame用于合成数值验收。
- `schema1.cxproj`及`artifacts/ct3d/*.analysis`：真实Schema1保存输出，包含48三角Mesh、26顶点和9点端面拟合。
- `CT3DAnalysisPersistenceTests.VerifySchema1Compatibility`只读本目录，在独立临时目录另存/重开新Schema，验证原结果身份、ROI和毫米量值保持。

`MakeSchema1Fixture.cs`保留当时的生成入口。只能与`2386b7d4`的未改动旧DLL一起生成新的历史样本，当前DLL会写新Schema，不得用于替换此固定二进制。`SHA256SUMS.txt`记录本批RAW、MEM工程及制品的固定字节摘要；制品名同时是其SHA256。测试额外断言历史制品头版本为1，再用当前正式读取器验证并另存。

此目录共约8KiB，必须整体入库（包括通常被artifacts忽略规则排除的`.analysis`）。不包含锁文件、当前构建DLL或工业扫描数据。
