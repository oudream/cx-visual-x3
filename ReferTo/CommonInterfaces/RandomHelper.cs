using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class RandomHelper
    {
        public static string RandomTif(string directoryPath)
        {
            // 指定目录路径
            //string directoryPath = @"E:\solder-ball-tif";

            // 获取目录下所有的图片文件，这里假设图片文件的扩展名为.tif
            string[] files = Directory.GetFiles(directoryPath, "*.tif");

            if (files.Length > 0)
            {
                // 创建Random对象
                Random random = new Random();

                // 随机选择一个文件
                string selectedFile = files[random.Next(files.Length)];

                return selectedFile;
            }
            else
            {
                return "";
            }
        }

    }
}
