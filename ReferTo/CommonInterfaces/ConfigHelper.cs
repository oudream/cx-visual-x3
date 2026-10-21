using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CommonInterfaces
{
    public static class ConfigHelper
    {
        // AppData 目录名
        public const string APPDATA_DIR_NAME = "AppData";

        // Commands 目录名
        public const string COMMANDS_DIR_NAME = "Commands";

        // 常量，配置目录名
        public const string CONFIG_DIR_NAME = "Configs";

        //常量，产品模型目录名称
        public const string ProductModel_DIR_NAME = "ProductModel";

        //常量，产品目录名称
        public const string PRODUCT_DIR_NAME = "Product";

        // 常量，驱动目录名
        public const string DRIVER_DIR_NAME = "Drivers";  
        
        // 常量，日志目录名
        public const string LOG_DIR_NAME = "Logs";

        // 常量，临时文件目录名
        public const string TEMP_DIR_NAME = "Temp";

        // 常量，数据文件目录名
        public const string DATA_DIR_NAME = "Data";

        // 常量，图像文件目录名
        public const string DirNameVision = "Vision";

        // 常量，点检文件目录名
        public const string SELFCHECK_DIR_NAME = "SelfCheck";

        // 常量，点检文件目录名
        public const string CASES_DIR_NAME = "Cases";

        // 常量，APP异常日志文件名
        public const string EXCEPTION_LOG_FILE_NAME = "异常日志.txt";

        // 常量，各模块版本记录文件
        public const string VERSION_LOG_FILE_NAME = "版本记录.csv";

        private static string _appDataPath = null;
        
        private static string _commandsPath = null;

        // 获取应用程序的上一级目录（即 AppData 存放位置）
        public static string GetAppDataParentDirectory()
        {
            string appDirectory = AppHelper.GetApplicationDirectory();
            return Directory.GetParent(appDirectory).FullName; // 返回上一级目录
        }

        // 获取 AppData 目录，AppData 目录下的有：Configs、Logs、Temp、Data
        public static string GetAppDataDirectory()
        {
            if (_appDataPath == null)
            {
                var appDataParentDirectory = GetAppDataParentDirectory();
                _appDataPath = Path.Combine(appDataParentDirectory, APPDATA_DIR_NAME);
            }

            return _appDataPath;
        }

        // 获取 Commands 目录，Commands 目录下的有：CxEvtCommand
        public static string GetCommandsDirectory()
        {
            if (_commandsPath == null)
            {
                var appDataParentDirectory = GetAppDataParentDirectory();
                _commandsPath = Path.Combine(appDataParentDirectory, COMMANDS_DIR_NAME);
            }

            return _commandsPath;
        }

        // 统一路径拼接方法
        public static string JoinAppDataPath(string subPath)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, subPath);
        }

        public static string ConfigPath()
        {
            return JoinAppDataPath(CONFIG_DIR_NAME);
        }

        public static string DriverPath()
        {
            return JoinAppDataPath(DRIVER_DIR_NAME);
        }

        public static string LogPath()
        {
            return JoinAppDataPath(LOG_DIR_NAME);
        }

        public static string TempPath()
        {
            return JoinAppDataPath(TEMP_DIR_NAME);
        }

        public static string DataPath()
        {
            return JoinAppDataPath(DATA_DIR_NAME);
        }

        public static string CasesPath()
        {
            return JoinAppDataPath(CASES_DIR_NAME);
        }

        // Command 的 exe 文件
        public static string CommandsFilePath(string fileName)
        {
            var commandsDirectory = GetCommandsDirectory();
            return Path.Combine(commandsDirectory, fileName);
        }

        // 配置目录下的文件路径
        public static string ConfigFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, CONFIG_DIR_NAME, fileName);
        }

        // 驱动目录下的文件路径
        public static string DriverFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, DRIVER_DIR_NAME, fileName);
        }

        // 日志目录下的文件路径
        public static string LogFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, LOG_DIR_NAME, fileName);
        }

        // Data 目录下的文件路径
        public static string DataFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, DATA_DIR_NAME, fileName);
        }
        // 导航图 目录下的文件路径
        public static string NaviFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, "点位导航图", fileName);
        }

        // Temp 目录下的文件路径
        public static string TempFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, TEMP_DIR_NAME, fileName);
        }

        // Cases 目录下的文件路径
        public static string CasesFilePath(string fileName)
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, CASES_DIR_NAME, fileName);
        }

        // 驱动点表文件
        public static string ConfigFilePathDriverPoints(string driverName)
        {
            return ConfigFilePath($"{driverName}.points.conl");
        }

        public static string ConfigFilePathNGNameExchangePoints(string driverName)
        {
            return ConfigFilePath($"{driverName}.csv");
        }

        // 客户端配置文件
        public static string ConfigFilePathClient()
        {
            return ConfigFilePath($"ClientConfigs.yaml");
        }

        // CT配置文件
        public static string ConfigFilePathCT()
        {
            return ConfigFilePath($"CTSystemConfigs.yaml");
        }

        // 权限文件路径
        public static string PermissionsFilePathSystem()
        {
            return ConfigFilePath($"Permissions.yaml");
        }

        // 权限文件路径
        public static string ProductsFilePathSystem()
        {
            return ConfigFilePath($"Products.dat");
        }

        // 产品配置 JSON 文件路径
        public static string ProductsJsonFilePathSystem()
        {
            return ConfigFilePath($"Products.json");
        }

        // 守护进程配置文件路径
        public static string DaemonFilFilePathSystem()
        {
            return ConfigFilePath($"Daemon.yaml");
        }

        // Halcon主文件路径
        public static string HalconMainHDevFilePath()
        {
            return AppHelper.JoinFullPath($"Main.hdev");
        }

        // 配方文件路径
        public static string ConfigFilePathProductsRecipe()
        {
            return ConfigFilePath($"Recipe.dat");
        }

        /// <summary>
        /// 缺陷区域配置根目录。
        /// </summary>
        public static string DefectRegionPath()
        {
            return Path.Combine(ConfigPath(), "DefectRegion");
        }

        /// <summary>
        /// 指定产品编码的缺陷区域配置目录。
        /// </summary>
        public static string DefectRegionPath(string productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode))
            {
                return DefectRegionPath();
            }

            return Path.Combine(DefectRegionPath(), productCode.Trim());
        }

        /// <summary>
        /// 指定产品编码的网格背景图路径。
        /// </summary>
        public static string DefectRegionGridImagePath(string productCode)
        {
            return Path.Combine(DefectRegionPath(productCode), "Grid.png");
        }

        /// <summary>
        /// 指定产品编码的胶路背景图路径。
        /// </summary>
        public static string DefectRegionGluePathImagePath(string productCode)
        {
            return Path.Combine(DefectRegionPath(productCode), "GluePath.png");
        }

        /// <summary>
        /// 指定产品编码的胶路区域Json路径。
        /// </summary>
        public static string DefectRegionGluePathRegionJsonPath(string productCode)
        {
            return Path.Combine(DefectRegionPath(productCode), "GluePathRegion.json");
        }

        /// <summary>
        /// 指定产品编码的网格缺陷区域Xml路径。
        /// </summary>
        public static string DefectRegionGridDefectRegionXmlPath(string productCode)
        {
            return Path.Combine(DefectRegionPath(productCode), "GridDefectRegion.xml");
        }

        // 获取 AppData 目录（如果不存在则创建，并复制旧数据）
        public static string ConfirmAppDataDirectory()
        {
            if (_appDataPath == null)
            {
                var appDataParentDirectory = GetAppDataParentDirectory();
                var appDataPath = Path.Combine(appDataParentDirectory, APPDATA_DIR_NAME);

                // 如果 AppData 目录不存在，则创建
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                    Console.WriteLine($"[INFO] 创建 AppData 目录: {appDataPath}");

                    // 复制旧的目录到新位置
                    MoveOldDirectory(CONFIG_DIR_NAME, appDataPath);
                    MoveOldDirectory(DRIVER_DIR_NAME, appDataPath);
                    MoveOldDirectory(LOG_DIR_NAME, appDataPath);
                    MoveOldDirectory(TEMP_DIR_NAME, appDataPath);
                    MoveOldDirectory(DATA_DIR_NAME, appDataPath);
                }

                // 赋值 _appData
                _appDataPath = appDataPath;
            }
            return _appDataPath;
        }

        // 复制旧目录到新的 AppData 目录
        private static void MoveOldDirectory(string dirName, string appDataPath)
        {
            string oldDir = Path.Combine(AppHelper.GetApplicationDirectory(), dirName);
            string newDir = Path.Combine(appDataPath, dirName);

            if (Directory.Exists(oldDir))
            {
                FileSystemHelper.CopyDirectory(oldDir, newDir);
                Console.WriteLine($"[INFO] 复制 {dirName} 到 {newDir}");
            }
        }

        public static string GetProductModelPath(string fileName)
        {
            var appDataPath = GetAppDataParentDirectory();
            return Path.Combine(appDataPath, PRODUCT_DIR_NAME, ProductModel_DIR_NAME, fileName);
        }

        public static string GetProductModelPath()
        {
            var appDataPath = GetAppDataDirectory();
            return Path.Combine(appDataPath, PRODUCT_DIR_NAME, ProductModel_DIR_NAME);
        }

        /// <summary>
        /// 获取平场校正增益图片名称与路径
        /// </summary>
        public static string GetGainTiffFileNmae(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, $"Gain.tif");
        }
        /// <summary>
        /// 获取平场校正暗场图片名称与路径
        /// </summary>
        public static string GetDarkTiffFileNmae(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, $"Dark.tif");
        }
        /// <summary>
        /// 获取平场校正明场图片名称与路径
        /// </summary>
        public static string GetFlatTiffFileNmae(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, $"Flat.tif");
        }
        public static string GetAngleDataFlatPath(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, "Flat");
        }
        public static string GetAngleDataGainPath(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, "Gain");
        }

        public static string GetReconAVGFlatTiffFileNmae(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, $"AVGFlat.tif");
        }
        public static string GetReconAVGDarkTiffFileNmae(string baseDir = null)
        {
            var appDataPath = Path.Combine(GetAppDataDirectory(), "Data");
            if (!string.IsNullOrEmpty(baseDir))
                appDataPath = baseDir;
            return Path.Combine(appDataPath, $"AVGDark.tif");
        }
    }


}
