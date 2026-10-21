using CommonInterfaces;
using DeviceInterfaces.UIStructure;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;
using YamlDotNet.Core.Tokens;

namespace DeviceInterfaces
{
    public static class DeviceManager
    {
        private static Dictionary<string, Device> devices = new Dictionary<string, Device>();
        private static Dictionary<string, Type> deviceTypes = new Dictionary<string, Type>();

        public static void RegisterDevice(Type deviceType, Device device)
        {
            if (!VariableManager.HasVariableTable(device.Name))
            {
                var variableTable = VariableManager.GetVariableTable(device.Name);
                if (variableTable == null)
                {
                    variableTable = DeviceTemplateManager.CreateVariableTableByType(deviceType, device.Name);
                    if (variableTable != null)
                    {
                        VariableManager.AddVariableTable(device.Name, variableTable);
                    }
                }
                if (variableTable == null)
                {
                    variableTable = new VariableTable(device.Name, deviceType);
                }
                device.AssignVariableTable(variableTable);
                devices[device.Name] = device;
                deviceTypes[device.Name] = deviceType;
            }
        }

        public static bool UnregisterDevice(string name)
        {
            if (devices.ContainsKey(name))
            {
                devices.Remove(name);
                deviceTypes.Remove(name);
                // 清除与设备关联的变量表
                VariableManager.RemoveVariableTable(name); 
                return true;
            }
            return false;
        }

        public static bool Remove(string name)
        {
            return UnregisterDevice(name);
        }

        public static bool UpdateDevice(string deviceName, Device newDevice)
        {
            if (devices.ContainsKey(deviceName))
            {
                DeviceTemplate template = null;
                if (deviceTypes.TryGetValue(deviceName, out var deviceType))
                {
                    template = DeviceTemplateManager.GetTemplate(deviceType);
                    // 检查模板是否指向一个有效的设备类型
                    if (!newDevice.GetType().IsAssignableFrom(template.DeviceType))
                    {
                        return false;
                    }
                }
                devices[deviceName] = newDevice;
                deviceTypes[deviceName] = newDevice.GetType();
                return true;
            }
            return false;
        }

        // 一般设备工厂类，用于创建一般设备
        // 板卡驱动模块的专有设备：Axis（轴）、Controller（控制器）、CoordinateSystem（坐标系）、Vector（点位）
        public static Device CreateAndRegisterDevice(string deviceName, string templateName)
        {
            // 获取模板
            DeviceTemplate template = DeviceTemplateManager.GetTemplate(templateName);
            if (template == null)
            {
                throw new ArgumentException($"No template found with name: {templateName}");
            }

            // 检查模板是否指向一个有效的设备类型
            if (template.DeviceType == null || !typeof(Device).IsAssignableFrom(template.DeviceType))
            {
                throw new InvalidOperationException($"Template {templateName} does not specify a valid device type.");
            }

            // 使用反射来创建设备实例
            var deviceConfig = new DeviceConfig() { Name = deviceName, DeviceTypeName = templateName };
            Device device = Activator.CreateInstance(template.DeviceType, new object[] { deviceConfig }) as Device;

            if (device == null)
            {
                throw new InvalidOperationException($"Failed to create device instance for template {templateName}.");
            }

            RegisterDevice(template.DeviceType, device); // 注册设备到设备管理器中

            return device;
        }

        public static bool ExistDevice(string deviceName)
        {
            return devices.ContainsKey(deviceName);
        }

        public static Device GetDevice(string deviceName)
        {
            if (devices.TryGetValue(deviceName, out var device))
            {
                return device;
            }
            return null;
        }

        public static DeviceTemplate GetDeviceTemplate(string deviceName)
        {
            if (deviceTypes.TryGetValue(deviceName, out var deviceType))
            {
                return DeviceTemplateManager.GetTemplate(deviceType);
            }
            return null;
        }

        public static int Load()
        {
            // TODO
            return 0;
        }

        public static string Save()
        {
            // TODO 
            return "";
        }

        public static List<Device> GetAllDevices()
        {
            List<Device> list = new List<Device>();
            list.AddRange(devices.Values);
            return list;
        }

        public static List<string> GetAllDeviceName()
        {
            return devices.Keys.ToList();
        }

        public static List<string> PrintAllVariableInfo()
        {
            List<string> result = new List<string>();
            foreach (var deviceName in devices.Keys)
            {
                if (devices.TryGetValue(deviceName, out Device device))
                {
                    DeviceTemplate template = null;
                    if (deviceTypes.TryGetValue(deviceName, out var deviceType))
                    {
                        template = DeviceTemplateManager.GetTemplate(deviceType);
                    }
                    if (template != null)
                    {
                        foreach (var variableDescription in template.VariableDescriptions)
                        {
                            string typeInfo = variableDescription.Type == typeof(byte) ? "byte" :
                                              variableDescription.Type == typeof(int) ? "int" :
                                              variableDescription.Type == typeof(double) ? "double" :
                                              variableDescription.Type == typeof(bool) ? "bool" :
                                              variableDescription.Type == typeof(long) ? "long" :
                                              variableDescription.Type == typeof(string) ? "string" :
                                              "unknown";

                            string valueString = "Unable to retrieve value";

                            // 根据类型调用相应的GetAttribute方法来获取值
                            if (variableDescription.Type == typeof(byte))
                            {
                                var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<byte>(variableDescription.Name);
                                valueString = $"{value}, lastUpdated: {lastUpdated}, hasInitialValue: {hasInitialValue}";
                            }
                            else if (variableDescription.Type == typeof(int))
                            {
                                var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<int>(variableDescription.Name);
                                valueString = $"{value}, lastUpdated: {lastUpdated}, hasInitialValue: {hasInitialValue}";
                            }
                            else if (variableDescription.Type == typeof(double))
                            {
                                var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<double>(variableDescription.Name);
                                valueString = $"{value}, lastUpdated: {lastUpdated}, hasInitialValue: {hasInitialValue}";
                            }
                            else if (variableDescription.Type == typeof(bool))
                            {
                                var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<bool>(variableDescription.Name);
                                valueString = $"{value}, lastUpdated: {lastUpdated}, hasInitialValue: {hasInitialValue}";
                            }
                            else if (variableDescription.Type == typeof(long))
                            {
                                var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<long>(variableDescription.Name);
                                valueString = $"{value}, lastUpdated: {lastUpdated}, hasInitialValue: {hasInitialValue}";
                            }
                            else if (variableDescription.Type == typeof(string))
                            {
                                var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<string>(variableDescription.Name);
                                valueString = $"{value}, lastUpdated: {lastUpdated}, hasInitialValue: {hasInitialValue}";
                            }

                            string info = $"Device: {deviceName}, Variable: {variableDescription.Name}, Type: {typeInfo}, Value: {valueString}";
                            result.Add(info);
                        }
                    }
                }
            }
            return result;
        }

        public static List<DeviceVariableDetail> ExportAllDeviceVariableDetail(bool bLastUpdatedString)
        {
            var result = new List<DeviceVariableDetail>();
            foreach (var deviceName in devices.Keys)
            {
                var details = ExportDeviceVariableDetail(deviceName, bLastUpdatedString);
                result.AddRange(details);
            }
            return result;
        }

        public static List<DeviceVariableDetail> ExportDeviceVariableDetail(string deviceName, bool bLastUpdatedString)
        {
            var result = new List<DeviceVariableDetail>();
            if (devices.TryGetValue(deviceName, out Device device))
            {
                // 假设VariableDescriptions提供了变量的描述
                DeviceTemplate template = null;
                if (deviceTypes.TryGetValue(deviceName, out var deviceType))
                {
                    template = DeviceTemplateManager.GetTemplate(deviceType);
                }
                if (template != null)
                {
                    foreach (var variableDescription in template.VariableDescriptions)
                    {
                        DeviceVariableDetail detail = new DeviceVariableDetail
                        {
                            DeviceName = deviceName,
                            VariableName = variableDescription.Name,
                            VariableTitle = variableDescription.Title,
                            ValueEnumTypeName = variableDescription.EnumTypeName,
                        };

                        detail.ValueTypeName = variableDescription.Type == typeof(byte) ? "byte" :
                                          variableDescription.Type == typeof(int) ? "int" :
                                          variableDescription.Type == typeof(double) ? "double" :
                                          variableDescription.Type == typeof(bool) ? "bool" :
                                          variableDescription.Type == typeof(long) ? "long" :
                                          variableDescription.Type == typeof(string) ? "string" :
                                          "unknown";

                        // 根据类型调用相应的GetAttribute方法来获取值
                        if (variableDescription.Type == typeof(byte))
                        {
                            var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<byte>(variableDescription.Name);
                            detail.Value = $"{value}";
                            detail.LastUpdated = lastUpdated;
                            detail.HasInitialValue = hasInitialValue;
                        }
                        else if (variableDescription.Type == typeof(int))
                        {
                            var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<int>(variableDescription.Name);
                            detail.Value = $"{value}";
                            detail.LastUpdated = lastUpdated;
                            detail.HasInitialValue = hasInitialValue;
                        }
                        else if (variableDescription.Type == typeof(double))
                        {
                            var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<double>(variableDescription.Name);
                            detail.Value = $"{value}";
                            detail.LastUpdated = lastUpdated;
                            detail.HasInitialValue = hasInitialValue;
                        }
                        else if (variableDescription.Type == typeof(bool))
                        {
                            var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<bool>(variableDescription.Name);
                            detail.Value = $"{value}";
                            detail.LastUpdated = lastUpdated;
                            detail.HasInitialValue = hasInitialValue;
                        }
                        else if (variableDescription.Type == typeof(long))
                        {
                            var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<long>(variableDescription.Name);
                            detail.Value = $"{value}";
                            detail.LastUpdated = lastUpdated;
                            detail.HasInitialValue = hasInitialValue;
                        }
                        else if (variableDescription.Type == typeof(string))
                        {
                            var (value, lastUpdated, hasInitialValue) = device.GetAttribute3<string>(variableDescription.Name);
                            detail.Value = $"{value}";
                            detail.LastUpdated = lastUpdated;
                            detail.HasInitialValue = hasInitialValue;
                        }

                        if (bLastUpdatedString && detail.LastUpdated != 0)
                        {
                            detail.LastUpdatedString = TimeHelper.GetDateTimeString(detail.LastUpdated);
                        }

                        result.Add(detail);
                    }
                }
            }
            return result;
        }

        public static void SetAllVariableValue(int value)
        {
            foreach (var deviceName in devices.Keys)
            {
                if (devices.TryGetValue(deviceName, out Device device))
                {
                    // 假设VariableDescriptions提供了变量的描述
                    DeviceTemplate template = null;
                    if (deviceTypes.TryGetValue(deviceName, out var deviceType))
                    {
                        template = DeviceTemplateManager.GetTemplate(deviceType);
                    }
                    if (template != null)
                    {
                        foreach (var variableDescription in template.VariableDescriptions)
                        {
                            // 根据类型调用相应的GetAttribute方法来获取值
                            if (variableDescription.Type == typeof(byte))
                            {
                                device.SetAttribute<byte>(variableDescription.Name, (byte)value);
                            }
                            else if (variableDescription.Type == typeof(int))
                            {
                                device.SetAttribute<int>(variableDescription.Name, value);
                            }
                            else if (variableDescription.Type == typeof(double))
                            {
                                device.SetAttribute<double>(variableDescription.Name, (double)value);
                            }
                            else if (variableDescription.Type == typeof(bool))
                            {
                                device.SetAttribute<bool>(variableDescription.Name, value==0);
                            }
                            else if (variableDescription.Type == typeof(long))
                            {
                                device.SetAttribute<long>(variableDescription.Name, (long)value);
                            }
                            else if (variableDescription.Type == typeof(string))
                            {
                                device.SetAttribute<string>(variableDescription.Name, value.ToString());
                            }
                        }
                    }
                }
            }
        }

    }

}
