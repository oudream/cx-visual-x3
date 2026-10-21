using CommonInterfaces;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    public static partial class HalconHelper
    {
        /// <summary>
        /// 把 imageDir 里所有图片按 imageByProductCount 纵向拆分，分别写入：
        /// imageDir\{position}\(原相对路径)\(原文件名)
        /// position 从 startPosition 开始递增
        /// </summary>
        public static string SplitImagesToPositionFolders(string imageDir, int startPosition, int imageByProductCount)
        {
            try
            {
                // 1) 先准备 position 目录
                for (int i = 0; i < imageByProductCount; i++)
                {
                    int pos = startPosition + i;
                    string posDir = Path.Combine(imageDir, pos.ToString());
                    if (!Directory.Exists(posDir))
                    {
                        Directory.CreateDirectory(posDir);
                    }
                }

                // 2) 找出 imageDir 下所有图片文件（你这里采图保存是多级目录，所以用 AllDirectories）
                //    注意：要排除我们刚刚生成的 position 目录，避免重复拆
                var allFiles = Directory.GetFiles(imageDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".tif" && ext != ".tiff")
                            return false;

                        return true;
                    })
                    .ToList();

                if (allFiles.Count == 0)
                {
                    return $"多产品拆分失败：目录下未找到可用图片文件：{imageDir}";
                }

                // 3) 逐张图拆分并写入对应 position 目录（保持原相对路径结构）
                foreach (var file in allFiles)
                {
                    HObject src = null;
                    try
                    {
                        src = HalconHelper.ReadImage(file);
                        if (src == null)
                        {
                            return $"多产品拆分失败：读图为空：{file}";
                        }

                        var parts = HalconHelper.VerticalPartVector(src, imageByProductCount);
                        if (parts == null || parts.Count != imageByProductCount)
                        {
                            return $"多产品拆分失败：拆分数量不对，期望={imageByProductCount} 实际={(parts == null ? 0 : parts.Count)}，文件={file}";
                        }

                        // 原相对路径（相对于 imageDir）
                        string rel = FileSystemHelper.GetFileName(file);

                        // rel 形如： "0\xxx.tif" 或 "0\sub\xxx.tif"（你的采图会有子目录）
                        // 目标： imageDir\{pos}\(原相对路径去掉第一段)\(原文件名)
                        // 也就是把 rel 的第一段（采图序号目录）保留，方便模板逻辑不变
                        for (int i = 0; i < parts.Count; i++)
                        {
                            int pos = startPosition + i;
                            string posDir = Path.Combine(imageDir, pos.ToString());

                            string dstPath = Path.Combine(posDir, rel);

                            string dstFolder = Path.GetDirectoryName(posDir);
                            if (!Directory.Exists(dstFolder))
                            {
                                Directory.CreateDirectory(dstFolder);
                            }

                            HalconHelper.SaveImage(parts[i], dstPath);
                        }

                        // 释放 parts
                        foreach (var p in parts)
                        {
                            try { p.Dispose(); } catch { }
                        }
                    }
                    finally
                    {
                        try { src?.Dispose(); } catch { }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"多产品拆分落盘异常：{ex.Message}";
            }
        }

    }

}
