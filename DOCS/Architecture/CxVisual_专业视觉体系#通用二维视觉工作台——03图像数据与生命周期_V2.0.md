# CxVisual 通用二维视觉工作台——03 图像数据、序列与生命周期详细设计 V2.0

> 范围：V2D-01～04，兼顾05/07/12/20/21。目标架构；未申请新增csproj或更改接口。已核实基类FIMImageBuffer、FIMRawImageReader、MVIImageSession、MDSDataContext、FDTDataAdapterRegistry、CxVisionGrayFrame；专业统一契约与长时流管理**尚未实现**。详见[02 UML](CxVisual_专业视觉体系%23通用二维视觉工作台——02UML总体模型与设计模式_V2.0.md) C01/C02、ST04/SQ01/SQ06。

## 1. 三种数据身份和四个生命周期

| 数据 | 身份/有效范围 | 内存/持久化拥有者 | 不变量 |
|---|---|---|---|
| PixelBuffer（E：FIMImageBuffer） | 单张原始不可随意修改的像素快照 | FIM输入自持有；原始参数由构造时复制 | Width/Height/Stride/Format与字节长度匹配；CopyPixels不泄漏内部可变数组 |
| Frame（N候选语义） | SourceId+StreamId+FrameId+采集时戳；一次采样 | 采集Provider/运行任务及受控Lease | 不以图窗刷新率当帧率；时间与帧顺序明确 |
| ImageSet / Sequence（N候选） | DatasetId+FrameRef顺序+版本 | 资源/数据集引用，数据体属于Artifact | 单帧失效不能意外重排或覆盖别帧身份 |
| Stream（P目标） | StreamId+生命周期+容量+策略 | Automation输入与Vision运行租约 | Stop/Cancel保证阻塞释放；NoDrop必须背压，不许无限内存 |
| Mask/Region（FIM已有处理结果，通用Mask合同N） | 源图+空间+Origin+Size+像素/几何规则 | FIM计算数据；Vision结果持产物引用 | ROI内分割不能静默改变掩码坐标系；孔洞/轮廓一致 |
| ArtifactRef（平台契约需核对） | 产物稳定Ref+Hash/格式/版本 | 既有FDT/MDS或正式Artifact存储 | 工程只存Ref，生命周期/清理策略不得靠UI存活 |

**裁决：** 原有FIMImageBuffer继续承载单帧数值像素，不在基础类塞SDK句柄、设备配置、运行队列和文件持久化；专业帧/图像关联元数据优先组合。接口是否封装为单独ImageFrame类型、扩展FDT数据合同或使用已有对象须经Owner评审。当前Professional.Vision有CxVisionGrayFrame（8/16位ushort数组与像素上限），**与FIMImageBuffer是两个并存的真实像素表达**；先完成同样本等价/所有权/复制次数/性能测试，再决定收敛或明确边界，不以一次强制转换制造第三套核心图像类型。

## 2. 标准像素合同与变换

| 维度 | 必须写明 | 必须反证 |
|---|---|---|
| Format | Gray8/Gray16/GrayFloat32/Bgr24/Bgra32等真实受支持格式；扩充格式须独立设计 | 非支持格式明确拒绝；不能隐式8位裁剪 |
| Layout | Width、Height、Stride（≥width×Bpp）、行起点/字节序、各通道意义 | 溢出、负长、错Stride、长度不足 |
| PixelCoordinate | 离散像素索引、像素中心、闭区间/半开区间语义 | (0,0)/(W-1,H-1)边界、裁剪右下端 |
| Geometry/Spacing | FGE原图几何点、像素间距sx/sy及单位、变换链 | sx≠sy时不可用平均倍率，缩放/旋转坐标双向回环 |
| DisplayMapping | 窗宽/窗位、Gamma、LUT、伪彩独立于Raw | 调窗后CopyPixels前后完全一致 |
| Metadata | SourceId/FrameId/时间/标定Ref/设备配置版本 | 缺失标定要标null或Unknown，不造物理数值 |

Mask合同在通用Gray8像素之外必须明确：0/非零二值语义、原图完整尺寸或裁剪Origin、ROI边界和空间版本；算法输出轮廓的点位只对指定图像坐标有效；对不同视口做投影而不是修改源点。

## 3. 输入策略、所有权和安全

1. 文件输入应把格式探测、读取验证、解码/像素转换、图像显示分离；BMP/PNG/JPEG/TIFF/RAW由已有FIM/MPW路径逐个迁回专业入口，**WPF解码与Foundation算法分层**；单图读成功但不说明多页TIFF/序列支持。
2. 内存输入：默认防悬挂复制；大图的可借用缓冲要有明确Owner、Lease、ReadOnly、Dispose、防止跨线程重用，不能把原始IntPtr永久藏在MVI/MPW。
3. SHM/跨进程/网络：图像帧关联版本、共享区域代次、长度与端序、授权/过期处理；消费端验证全部元数据后才暴露。
4. 相机/探测器在Automation/驱动Provider接入；Vision接收稳定FrameRef或ImageHandle，采集卡失联/热插拔要通过SourceState通知而非让UI阻塞SDK。
5. 序列导入先具备稳定帧顺序/坏帧隔离/预取预算；多路并发需保持StreamId、SequenceId和FrameId，不把到达顺序冒充采集时序。

## 4. 内存预算、缓存和性能模型

建议每次Run形成可审计预算：RawBytes=Stride×Height；DerivedBytes=各Intermediate实际Bytes；ResidentPeak≈InputHandles+WorkingSet+Results+DisplayCopy+GPUCopies+QueueCapacity×FrameBytes（实际有所有权共享时按唯一Buffer去重）。例如4300×4300×Gray16紧排单帧=36,980,000字节，**不包含灰度显示副本、Mask、双Pane、算法工作区、缓存及驱动缓冲**；显示两张图/拷贝多次可能显著放大峰值。实施时必须量化而非规定虚构达标指标。

缓存分层：不可变原图引用 → 受预算的CPU中间图 → 显示缩放/瓦片缓存 → GPU纹理缓存；每层声明缓存键（FrameId+PixelFormat+Transform+LUTRevision）、上限、淘汰和跨线程释放责任。灰度LUT变化只重算显示路径，不能重新载入或修改原图；过期工作项不占据长时租约。

## 5. 流、取消和时序

~~~mermaid
sequenceDiagram
  participant A as Automation Source
  participant Q as BoundedQueue
  participant V as Vision
  participant M as MDS DataHandle
  participant W as MPW
  A->>Q: FrameRef+timestamp
  alt capacity available
    Q->>V: lease frame
    V->>M: typed result
    V-->>Q: release lease
    M-->>W: latest preview result
  else backpressure
    Q-->>A: block/throttle/drop as policy
  end
~~~

默认对UI预览使用Latest（丢旧显示帧不等于丢生产检测帧）；生产NoDrop需设备背压或持久队列，并对超时、容量/磁盘耗尽给明确失败策略。请求Identity建议由OriginalIdentity + ParameterRevision + RequestEpoch + CancellationToken组合；现有MVIImageSession.SourceVersion也因SetResult改变，**不得充当唯一原图Revision**。

## 6. GoF/AOP与开放点

Observer用于帧/会话变化，Strategy仅在两种真实读取Provider可替换时；Bridge/Adapter只在SDK边界有真实不兼容问题时使用，不另加通用包装；AOP记录帧来源/租约数/峰值/解码耗时/队列积压/失效次数/Trace，绝不逐像素写日志。运行产生ArtifactRef，与MEM工程资源引用通过正式合同相连。

## 7. 独立验收：D03系列

| 编号 | 真值/反证 | 结果证据 |
|---|---|---|
| D03-01 | 8/16/Float32/BGR及Stride解码与逐像素等值 | Foundation数值测试 |
| D03-02 | 负尺寸、错长度、溢出、无效格式不污染现有图 | 异常/稳定性测试 |
| D03-03 | 16位原始像素对调窗/镜像/画ROI不改变 | FIM+WPF回环 |
| D03-04 | 同一数据FIM与CxVisionGrayFrame显式转换等价 | 专业迁移样本 |
| D03-05 | 原图更新时旧异步结果拒绝、取消后租约释放 | MVI+任务竞争 |
| D03-06 | 4300²及更大图、双Pane、历史中间结果统计真实峰值 | Windows压力与Trace |
| D03-07 | 有界流过载下明确Latest/Drop/NoDrop、队列不无限增长 | 回放及设备HIL |
| D03-08 | SHM/网络/设备失联重连、坏帧和权限拒绝 | 真实Provider/设备专项 |

**状态**：此文档是架构设计；D03-06～08不能因合成小图WPF smoke成功而标记通过。

## 8. 统一数据合同的深化裁决（2026-10-28）

基础数据不得因为引入实时流或专业SDK就变成第三套像素底座。**E已核实**：FIMImageBuffer使用带Width/Height/Stride/PixelFormat的自有byte[]；CxVisionGrayFrame为专业8/16位灰度ushort快照，复制与像素语义不同；FDTReadOnlyBuffer、FDTDataAdapterRegistry提供只读字节与专业值捕获；MDSDataBudget计入FlowHeap/DataStack已提交数据，但不含全部临时工作区/GPU显存；MDSBoundedByteStream是本机有界字节块流而非完整CameraFrameStream。

**P待裁决的FrameRef**只表达SourceId+SourceGeneration+FrameId+采集Time+PixelFormat+CalibrationRef+可选受控Lease，不复制整份FIMImageBuffer。ImageSet持FrameRef索引；RunResult持InputRef及ArtifactRef；MPW显示持当前选中的像素与MVI视口。FrameLease的实际接口须根据MDS生命周期和设备Provider评审，不得凭文档臆造Dispose语义。

**三项优先测试**：Gray8/16由FIM和PVC运行相同样本逐像素及数值比较、Stride/字节序/复制峰值测量、图像被切换或流关闭后的异步数据与租约不误发布。大图应分别测量输入副本、算法工作区、WPF显示副本、GPU纹理和队列预算；对专业GPU/SDK占用必须另记账。
