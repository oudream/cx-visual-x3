namespace CxVisual.Foundation.Volume
{
    /// <summary>二维切片的三个正交平面。</summary>
    public enum FVOSlicePlane
    {
        XY,
        XZ,
        YZ,
    }

    /// <summary>提供切片平面的标题和坐标轴映射等辅助方法。</summary>
    public static class FVOSlicePlaneExtensions
    {
        public static string GetTitle(this FVOSlicePlane plane)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    return "XY / Axial";
                case FVOSlicePlane.XZ:
                    return "XZ / Coronal";
                case FVOSlicePlane.YZ:
                    return "YZ / Sagittal";
                default:
                    return plane.ToString();
            }
        }

        public static string GetWheelAxisName(this FVOSlicePlane plane)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    return "Z";
                case FVOSlicePlane.XZ:
                    return "Y";
                case FVOSlicePlane.YZ:
                    return "X";
                default:
                    return string.Empty;
            }
        }
    }
}
