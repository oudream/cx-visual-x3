namespace CxECTVolumeCore.Slicing
{
    /// <summary>二维切片的三个正交平面。</summary>
    public enum SlicePlane
    {
        XY,
        XZ,
        YZ,
    }

    /// <summary>提供切片平面的标题和坐标轴映射等辅助方法。</summary>
    public static class SlicePlaneExtensions
    {
        public static string GetTitle(this SlicePlane plane)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    return "XY / Axial";
                case SlicePlane.XZ:
                    return "XZ / Coronal";
                case SlicePlane.YZ:
                    return "YZ / Sagittal";
                default:
                    return plane.ToString();
            }
        }

        public static string GetWheelAxisName(this SlicePlane plane)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    return "Z";
                case SlicePlane.XZ:
                    return "Y";
                case SlicePlane.YZ:
                    return "X";
                default:
                    return string.Empty;
            }
        }
    }
}
