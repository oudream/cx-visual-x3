# Vision配置驱动真实检测闭环 V1.0

## 目标与证据

本轮从真实源码出发，在当前28项目结构中将公共配置、运行生效和Vision像素计算打通。
**数据处理是真正执行的CPU图像算法**，并非仅增加模拟Provider或返回固定结果。
开发分支构建及测试和合并后master的GitHub Actions作为验收证据。
用户截图里的V6.4对象规格为参考，继承已确立的FCM/MPM/MTC/MRK前缀与单一Owner。

## 归属和调用链

| 项目 | 代码 | 责任 |
|---|---|---|
| Foundation.Data · F15 | FCMConfigKey | 配置标识 |
| Mechanism.Core · M04/M07 | MPMConfigDescription / MTCConfigManager | 元数据、持久化、Revision、通知 |
| Mechanism.Runtime · M30 | MRKConfigRuntimeSession<T>、IMRKConfigApplier<T> | 软件运行生效、任务边界、失败补偿 |
| Professional.Vision · P30–P42 | CxVisionInspectionRuntime、CxVisionThresholdConfigApplier、CxVisionThresholdPipeline | 准备并应用真正的灰度图像处理流水线 |
| Applications.Runtime.Host | vision-demo / vision-inspect | 独立进程或一键演示 |

操作链路：参数保存 → Revision增加 → MRK RequestApply(NextTask) → 当前任务继续旧引擎 → 所有当前任务结束 → 下一任务启动时专业Prepare/Apply/Verify → 新Revision生效 → 真正检测像素并返回报告。

新Provider并非全场景硬件驱动；CPU算法的Verify成功**不代表X-Ray设备、探测器或PLC已经ACK**。

## 开发者最短调用

~~~csharp
FCMConfigKey key = new FCMConfigKey("Vision", "Line01");
MTCConfigManager configs = MTCConfigManager.Create<CxVisionInspectionOptions>(
    configDirectory, key, CxVisionInspectionOptions.Validate);
CxVisionInspectionRuntime vision = new CxVisionInspectionRuntime(configs, key.Id);
using (CxVisionInspectionTask task = vision.BeginInspection())
{
    CxVisionInspectionResult result = task.Inspect(grayFrame);
    Console.WriteLine(result.ConfigRevision + " " + result.Regions.Count);
}
configs.UpdateAndSave<CxVisionInspectionOptions>(key.Id, x => x.GrayThreshold = 230);
vision.RequestApply(MRKConfigApplyPolicy.NextTask);
~~~

Vision不重新编写配置保存器、类型编辑器、Revision系统或任务生效状态机。
全局软件ActiveRevision和每次任务固定Revision由公共MRK维护；每个任务单独获取并保存CPU流水线快照。

## 一键无设备演示

~~~powershell
.\Output\Release\x64\CxVisual.Runtime.Host.exe vision-demo
# VisionDemo Revision=1 Defects=1 ThresholdPixels=4
# VisionDemo Revision=2 Defects=0 ThresholdPixels=1
# VisionDemo PASS CPU threshold pipeline; no camera, AI inference or hardware ACK
~~~

若已经有配置文件Vision.Line01.json和RAW像素文件，也可在独立进程执行：

~~~powershell
.\Output\Release\x64\CxVisual.Runtime.Host.exe vision-inspect C:\vision-config Vision.Line01 C:\frame.raw 5 3 8
~~~

RAW输入**不带文件头**：8bit为逐行单字节，16bit为逐行little-endian ushort。必须给定准确的宽高/位深。
不接受不存在的图像文件、未知配置、位深不一致或图像大小不对的输入；不会悄悄默认启动。

## 真实算法边界

1. 严格复制帧像素，检查宽高、位深、数组长度与8bit范围，避免外部修改影响运行中的图像。
2. 每个像素与GrayThreshold比较，使用**四邻接**连通域（仅对角线相邻不连接），统计亮点数、像素面积和矩形包围盒。
3. 仅保留MinDefectArea至MaxDefectArea之间的连通域；结果包含每次检测使用的ConfigRevision及Passed。
4. 使用一次固定版本的引擎处理完整任务；新阈值只在允许的安全边界生效。
5. UseAi=true时CPU Provider明确拒绝运行生效，旧健康配置继续服务；需专业AI运行器才能支持模型推理。
6. NextTask应用失败、Rollback也失败时将MRK置Faulted，禁止新任务在不确定状态下继续。

## 采用TDD的验收案例

| ID | 测试意图 | 必须验证的事实 |
|---|---|---|
| VIS-01 | 5x3 8bit, threshold 200, area>=2 | 4阈值像素，1个面积3的四连通区域 |
| VIS-02 | 工作任务中修改阈值到230 | 旧任务Revision1不变，新任务Revision2只有1阈值像素且无有效缺陷 |
| VIS-03 | 软件CPU不支持AI配置 | 拒绝新配置、旧Active保持、关闭AI后可恢复 |
| VIS-04 | 3x2 16bit, threshold 3000 | 3个阈值像素，1个面积2的区域 |
| VIS-05 | 8bit像素999、错误尺寸、超大帧 | 抛出输入错误，不生成正常结果 |
| VIS-06 | 独立Runtime.Host进程读取RAW与配置 | 确认Revision1/2对应不同的实际像素计算结果 |
| VIS-07 | 下一任务Verify失败、补偿也失败 | 不允许开启新任务，必须进入Faulted |
| VIS-08 | 直接运行vision-demo | 独立命令输出两种真实检测结果及版本 |

测试源码：Tests/ArchitectureSmoke/VisionLiveConfigTests.cs；Windows工作流：.github/workflows/config-l1-ci.yml。
既有L1–L4、Automation、CT3D、WPF回归仍应全部通过。

## 本阶段未实现，不得误报

工业X-Ray完整图像预处理、探测器采图同步、AI模型执行、GPU加速、各缺陷专有算法、
真实机台安全联锁/设备ACK、长时间大尺寸UPH性能基准，以及跨机器签名审批发布，均未在本次实现。
现有CPU算法是可用的**正确性和配置协作验收基线**，不是可以未经现场验证直接投产的完整X-Ray检测产品。
