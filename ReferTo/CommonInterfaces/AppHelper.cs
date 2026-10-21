using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class AppHelper
    {
        public static string GetExecutablePath()
        {
            // 获取当前执行的程序的 Assembly
            Assembly assembly = Assembly.GetEntryAssembly();

            // 获取程序的全路径
            string executablePath = assembly.Location;

            return executablePath;
        }

        public static string GetApplicationDirectory()
        {
            // 获取当前执行的程序集所在的目录
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;

            // 获取目录部分
            string appDirectory = Path.GetDirectoryName(assemblyLocation);

            // 返回上一级目录
            //return Directory.GetParent(appDirectory).FullName;
            return appDirectory;
        }

        public static bool IsFullPath(string path)
        {
            return Path.IsPathRooted(path);
        }

        // path: 你的相对文件路径
        public static string JoinFullPath(string path)
        {
            if (IsFullPath(path))
            {
                return path;
            }
            string appDirectory = GetApplicationDirectory();
            // 使用应用程序目录和相对路径拼接得到完整路径
            return Path.Combine(appDirectory, path);
        }

        public static string JoinFullPath(string path1, string path2)
        {
            if (IsFullPath(path1))
            {
                return Path.Combine(path1, path2);
            }
            string appDirectory = GetApplicationDirectory();
            // 使用应用程序目录和相对路径拼接得到完整路径
            return Path.Combine(appDirectory, path1, path2);
        }

        public static string JoinFullPath(string path1, string path2, string path3)
        {
            if (IsFullPath(path1))
            {
                return Path.Combine(path1, path2, path3); ;
            }
            string appDirectory = GetApplicationDirectory();
            // 使用应用程序目录和相对路径拼接得到完整路径
            return Path.Combine(appDirectory, path1, path2, path3);
        }

        public static string JoinFullPath(string path1, string path2, string path3, string path4)
        {
            if (IsFullPath(path1))
            {
                return Path.Combine(path1, path2, path3, path4); ;
            }
            string appDirectory = GetApplicationDirectory();
            // 使用应用程序目录和相对路径拼接得到完整路径
            return Path.Combine(appDirectory, path1, path2, path3, path4);
        }

        public static string JoinFullPath(string path1, string path2, string path3, string path4, string path5)
        {
            if (IsFullPath(path1))
            {
                return Path.Combine(path1, path2, path3, path4, path5); ;
            }
            string appDirectory = GetApplicationDirectory();
            // 使用应用程序目录和相对路径拼接得到完整路径
            return Path.Combine(appDirectory, path1, path2, path3, path4, path5);
        }

        public static string JoinFullPath(string path1, string path2, string path3, string path4, string path5, string path6)
        {
            if (IsFullPath(path1))
            {
                return Path.Combine(path1, path2, path3, path4, path5, path6);
            }
            string appDirectory = GetApplicationDirectory();
            // 使用应用程序目录和相对路径拼接得到完整路径
            return Path.Combine(appDirectory, path1, path2, path3, path4, path5, path6);
        }
    }
}
