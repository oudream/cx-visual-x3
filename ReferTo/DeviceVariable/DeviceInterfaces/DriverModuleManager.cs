using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class DriverModuleManager
    {
        // 驱动模块列表
        private static List<DriverModuleBase> driverModules = new List<DriverModuleBase>();

        // 从文件中加载全局模块
        public static void Load()
        {
            // TODO
        }

        // 添加模块
        public static void AddDriverModule(DriverModuleBase module)
        {
            module.TypeName = StringHelper.ExtractShortNameAfterDot(module.GetType().Name);
            driverModules.Add(module);
        }

        // 修改模块，假设通过某个标识符来查找并修改
        public static bool ModifyDriverModule(long id, DriverModuleBase newModule)
        {
            var index = driverModules.FindIndex(m => m.Id == id);
            if (index != -1)
            {
                driverModules[index] = newModule;
                return true;
            }
            return false;
        }

        // 删除模块
        public static bool RemoveDriverModule(long id)
        {
            var module = driverModules.FirstOrDefault(m => m.Id == id);
            if (module != null)
            {
                driverModules.Remove(module);
                return true;
            }
            return false;
        }

        // 查询模块
        public static DriverModuleBase GetDriverModule(long id)
        {
            return driverModules.FirstOrDefault(m => m.Id == id);
        }

        // 获取所有模块
        public static List<DriverModuleBase> GetAllDriverModules()
        {
            return driverModules;
        }

        // 实现AllVariablesHaveInitialAndUpdatedValues方法
        public static (bool, string, string, string) AllVariablesHaveInitialAndUpdatedValues()
        {
            foreach (var module in driverModules)
            {
                // 调用每个模块的AllVariablesHaveInitialAndUpdatedValues方法
                var (allUpdated, variableName, deviceName) = module.AllVariablesHaveInitialAndUpdatedValues();
                if (!allUpdated)
                {
                    // 如果任一模块的变量未完全初始化或更新，立即返回该模块的结果及其名称
                    return (false, variableName, deviceName, module.Name); // 假设每个模块有一个Name属性
                }
            }

            // 所有模块的变量都已初始化且更新
            return (true, string.Empty, string.Empty, string.Empty);
        }

        // 启动全部驱动模块的读取动态属性功能
        public static void StartReadDynamicProperty()
        {
            foreach (var module in driverModules)
            {
                module.StartReadDynamicProperty();
            }
        }

    }
}
