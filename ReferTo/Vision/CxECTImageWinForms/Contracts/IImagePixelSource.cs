namespace CxECTImageWinForms.Contracts
{
    /// <summary>
    /// 统计服务需要的最小像素读取能力，HALCON 适配器与测试假数据都可实现。
    /// </summary>
    public interface IImagePixelSource
    {
        int Width { get; }

        int Height { get; }

        double GetGrayValue(int x, int y);
    }
}
