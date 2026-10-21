using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    public static class DeviceTemplateManager
    {
        private static List<DeviceTemplate> templates = new List<DeviceTemplate>();

        public static List<DeviceTemplate> GetAllDeviceTemplates()
        {
            return templates;
        }

        // 添加模板
        public static void AddTemplate(DeviceTemplate template)
        {
            templates.Add(template);
        }

        // 删除模板
        public static bool RemoveTemplate(string templateName)
        {
            var template = templates.FirstOrDefault(t => t.Name == templateName);
            return template != null && templates.Remove(template);
        }

        // 根据模板名称获取模板 templateName == DeviceTypeName
        public static DeviceTemplate GetTemplate(string templateName)
        {
            return templates.FirstOrDefault(t => t.Name == templateName);
        }

        // 根据设备类型获取模板
        public static DeviceTemplate GetTemplate(Type deviceType)
        {
            return templates.FirstOrDefault(t => t.DeviceType == deviceType);
        }

        // 更新模板
        public static bool UpdateTemplate(string templateName, Action<DeviceTemplate> updateAction)
        {
            var template = GetTemplate(templateName);
            if (template != null)
            {
                updateAction(template);
                return true;
            }
            return false;
        }

        // 清除所有模板
        public static void ClearAllTemplates()
        {
            templates.Clear();
        }

        // 根据设备类型创建变量表
        public static VariableTable CreateVariableTableByType(Type type, string deviceName)
        {
            var template = GetTemplate(type);
            if (template != null)
            {
                var variableTable = new VariableTable(deviceName, type);
                var descs = template.VariableDescriptions;
                for (int i = 0; i < descs.Count; i++)
                {
                    var desc = descs[i];
                    var variableType = desc.Type;
                    // 根据变量类型执行相应的逻辑
                    if (variableType == typeof(byte))
                    {
                        // TODO, default value
                        variableTable.AddVariable<byte>(desc.Name, 0, true);
                    }
                    else if (variableType == typeof(int))
                    {
                        variableTable.AddVariable<int>(desc.Name, 0, true);
                    }
                    else if (variableType == typeof(double))
                    {
                        variableTable.AddVariable<double>(desc.Name, 0f, true);
                    }
                    else if (variableType == typeof(bool))
                    {
                        variableTable.AddVariable<bool>(desc.Name, false, true);
                    }
                    else if (variableType == typeof(long))
                    {
                        variableTable.AddVariable<long>(desc.Name, 0, true);
                    }
                    else if (variableType == typeof(string))
                    {
                        variableTable.AddVariable<string>(desc.Name, string.Empty, true);
                    }
                }
                return variableTable;
            }
            return null;
        }

        // 取得所有的模板名称
        public static List<string> GetTemplateNames() => templates.Select(t => t.Name).ToList();

        // 取得所有的模板名称，条件是专用设备
        public static List<string> GetSpecTemplateNames() => templates.Where(t => t.IsSpec).Select(t => t.Name).ToList();

        // 取得所有的模板类型
        public static List<Type> GetTemplateTypes() => templates.Select(t => t.DeviceType).ToList();

        // 取得所有的模板名称，条件是所属驱动模块
        public static List<string> GetModuleTemplateNames(string moduleName) => templates.Where(t => t.ModuleName == moduleName).Select(t => t.Name).ToList();

        public static void Load()
        {
            // TODO
        }

    }

}
