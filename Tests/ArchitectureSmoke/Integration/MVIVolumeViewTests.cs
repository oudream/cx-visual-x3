using System;
using System.ComponentModel;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>四视图/三刀只使用一个会话状态；不依赖WPF或旧WinForms。</summary>
    internal static class MVIVolumeViewTests
    {
        public static void Run()
        {
            using (FVOVolumeData data = FVOVolumeData.CreateDemo(8, 10, 12))
            {
                MVIVolumeViewState state = MVIVolumeViewState.CreateCentered(data);
                Check(state.SliceX == 4 && state.SliceY == 5 && state.SliceZ == 6, "初始中心位置");
                int changes = 0;
                state.PropertyChanged += delegate(object sender, PropertyChangedEventArgs e) { changes++; };
                state.SetSlices(999, -10, 3, data);
                Check(state.SliceX == 7 && state.SliceY == 0 && state.SliceZ == 3, "越界三刀位置钳制");
                Check(changes == 1, "三刀一次更新一个事件");
                state.SetSliceViewport(FVOSlicePlane.XY, 0.7f, 0.2f, -0.5f, 1.2f);
                MVISliceViewport viewport = state.GetSliceViewport(FVOSlicePlane.XY);
                Check(Math.Abs(viewport.MinA - 0.2f) < 0.0001 && viewport.MaxB > 1, "任意视口映射不截断");
                FVOObliqueSliceDefinition oblique = new FVOObliqueSliceDefinition();
                oblique.AngleZDegrees = 45;
                state.SetObliqueSlice(oblique);
                oblique.AngleZDegrees = 0;
                Check(state.ObliqueSlice.AngleZDegrees == 45, "独立复制斜切定义");
                Check(state.GetSliceViewport(FVOSlicePlane.XZ).MinA == 0, "视口互不污染");
                Check(state.ShowCrosshair, "默认可见十字线");
                Console.WriteLine("PASS MVI single volume view state and 4-view synchronization contracts");
            }
        }
        private static void Check(bool success, string name)
        {
            if (!success) throw new InvalidOperationException("MVI体数据测试失败：" + name);
        }
    }
}
