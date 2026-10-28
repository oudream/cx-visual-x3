using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>V2D-02/DC-01/02: FIM与PVC专业帧及真实MRK链路的无WPF回归。</summary>
    internal static class RuntimeVisionImageInteropTests
    {
        private static void Check(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException("VISION_IMAGE_INTEROP: " + reason);
        }

        private static void Reject<T>(Action action, string reason) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("VISION_IMAGE_INTEROP: " + reason);
        }

        internal static void Run()
        {
            EightBitStrideAndOwnership();
            SixteenBitStrideAndRealExecution();
            UnsupportedAndMismatchedFormats();
        }

        private static void EightBitStrideAndOwnership()
        {
            byte[] original = {
                210, 210, 0, 123, 124,
                210, 0, 0, 125, 126
            };
            FIMImageBuffer image = new FIMImageBuffer(3, 2, FIMPixelFormat.Gray8, original, 5);
            original[0] = 0;
            Check(image.GetGrayValue(0, 0) == 210, "FIM输入数组变更污染原图");
            Check(CxVisionImageInterop.GetPixelDepth(image) == CxVisionPixelDepth.Bits8,
                "Gray8位深映射");
            CxVisionGrayFrame frame = CxVisionImageInterop.ToGrayFrame(image);
            FIMImageBuffer result = CxVisionImageInterop.ToImageBuffer(frame);
            Check(result.Width == 3 && result.Height == 2 && result.Stride == 3 &&
                result.GetGrayValue(0, 0) == 210 && result.GetGrayValue(1, 0) == 210 &&
                result.GetGrayValue(2, 0) == 0 && result.GetGrayValue(0, 1) == 210,
                "Gray8带Stride往返丢失像素或混入Padding");
            byte[] edited = result.CopyPixels();
            edited[0] = 0;
            Check(result.GetGrayValue(0, 0) == 210 && image.GetGrayValue(0, 0) == 210,
                "反向转换暴露可变别名");

            IDictionary<string, object> inputs =
                CxVisionImageInterop.ToCpuInspectionInputs(image, CxVisionPixelDepth.Bits8);
            var raw = (FDTReadOnlyBuffer)inputs["RawPixels"];
            byte[] copy = raw.ToArray();
            Check(copy.Length == 6 && copy[0] == 210 && copy[2] == 0 &&
                copy[3] == 210 && copy[4] == 0 && copy[5] == 0,
                "CPU Flow输入未去除行Padding");
            copy[0] = 0;
            Check(raw[0] == 210 && image.GetGrayValue(0, 0) == 210,
                "FDT数据副本被调用者更改");
        }

        private static void SixteenBitStrideAndRealExecution()
        {
            byte[] source = {
                0xF4, 0x01, 0xF4, 0x01, 0xEE, 0xEE,
                0, 0, 0, 0, 0xEE, 0xEE
            };
            var image = new FIMImageBuffer(2, 2, FIMPixelFormat.Gray16, source, 6);
            Check(CxVisionImageInterop.GetPixelDepth(image) == CxVisionPixelDepth.Bits16,
                "Gray16位深映射");
            var roundTrip = CxVisionImageInterop.ToImageBuffer(
                CxVisionImageInterop.ToGrayFrame(image));
            Check(roundTrip.Stride == 4 && roundTrip.GetGrayValue(0, 0) == 500 &&
                roundTrip.GetGrayValue(1, 0) == 500 &&
                roundTrip.GetGrayValue(0, 1) == 0 &&
                roundTrip.GetGrayValue(1, 1) == 0, "Gray16小端或Padding转换错误");
            var inputs = CxVisionImageInterop.ToCpuInspectionInputs(
                image, CxVisionPixelDepth.Bits16);
            var packed = (FDTReadOnlyBuffer)inputs["RawPixels"];
            Check(packed.Length == 8 && packed[0] == 0xF4 && packed[1] == 0x01 &&
                packed[4] == 0 && packed[6] == 0, "Gray16 RAW必须紧排");
            source[0] = 0;
            Check(packed[0] == 0xF4, "启动运行之前源数组更改影响输入");

            var options = new CxVisionInspectionOptions {
                PixelDepth = CxVisionPixelDepth.Bits16,
                GrayThreshold = 400, MinDefectArea = 2,
                MaxDefectArea = 100, UseAi = false
            };
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                MRKFlowResult run = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlan(options, 57), inputs)
                    .Completion.GetAwaiter().GetResult();
                Check(run.State == MRKFlowState.Completed,
                    "FIM输入实际MRK检测链失败：" + (run.Error == null ? "" : run.Error.ToString()));
                var found = run.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
                Check(found.ConfigRevision == 57 && found.Width == 2 && found.Height == 2 &&
                    found.ThresholdPixelCount == 2 && found.Regions.Count == 1 &&
                    found.Regions[0].PixelArea == 2 &&
                    run.GetCommittedOutput<long>("GraySum") == 1000 &&
                    !run.GetCommittedOutput<bool>("Passed"),
                    "FIM Gray16→MRK→PVC真实连通域/统计结果错误");
                Check(image.GetGrayValue(0, 0) == 500,
                    "运行流程不允许更改FIM输入原图");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void UnsupportedAndMismatchedFormats()
        {
            Reject<ArgumentNullException>(
                () => CxVisionImageInterop.ToGrayFrame(null), "null输入未拒绝");
            Reject<ArgumentNullException>(
                () => CxVisionImageInterop.ToImageBuffer(null), "null帧未拒绝");
            var color = new FIMImageBuffer(1, 1, FIMPixelFormat.Bgr24,
                new byte[] { 255, 0, 0 });
            Reject<NotSupportedException>(
                () => CxVisionImageInterop.ToGrayFrame(color),
                "彩色图被隐式降为灰度");
            Reject<NotSupportedException>(
                () => CxVisionImageInterop.ToCpuInspectionInputs(
                    color, CxVisionPixelDepth.Bits8), "BGR被直接输入单通道Plan");
            var floatImage = new FIMImageBuffer(1, 1, FIMPixelFormat.GrayFloat32,
                new byte[] { 0, 0, 128, 63 });
            Reject<NotSupportedException>(
                () => CxVisionImageInterop.ToGrayFrame(floatImage),
                "Float32被隐式转换为8/16位");
            var gray16 = new FIMImageBuffer(1, 1, FIMPixelFormat.Gray16,
                new byte[] { 0xF4, 0x01 });
            Reject<ArgumentException>(
                () => CxVisionImageInterop.ToCpuInspectionInputs(
                    gray16, CxVisionPixelDepth.Bits8),
                "已编译Plan与输入位深不一致仍接受");
            Reject<ArgumentNullException>(
                () => CxVisionImageInterop.ToCpuInspectionInputs(
                    null, CxVisionPixelDepth.Bits8),
                "空FIM被接受");
        }
    }
}
