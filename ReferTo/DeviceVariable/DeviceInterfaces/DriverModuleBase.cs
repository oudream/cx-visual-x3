using CommonInterfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VariableInterfaces;

namespace DeviceInterfaces
{
    // 设备驱动模块基类
    // 驱动模块里有一个点表，点表填入的是设备属性与“具体驱动模块”映射关系，
    // “具体驱动模块”还可以定义自己的“专有设备模板”，这样可以让用户在“具体驱动模块”中添加“专有设备”。
    public abstract class DriverModuleBase
    {
        // 驱动模块的Id，用于标识驱动模块
        [DisplayName("ID")] // DataGridView 显示名
        public long Id { get; private set; }

        // 驱动模块的名称，用于可视驱动模块
        [DisplayName("名称")] // DataGridView 显示名
        public string Name { get; private set; }

        [Browsable(false)] // 不在 DataGridView 中显示
        public DriverModuleConfig Config { get; private set; }
        
        [DisplayName("类型名称")]
        public string TypeName { get; set; }

        // 驱动模块的点表，用于驱动模块的设备属性与“具体驱动模块”映射关系
        [Browsable(false)] // 不在 DataGridView 中显示
        public PointSheet PointSheet { get; }

        // 专用设备，属于驱动模块，自己管理的
        protected Dictionary<string, Device> specDevices;
        protected Dictionary<string, Type> specDeviceTypes;

        public DriverModuleBase(DriverModuleConfig config)
        {
            // 驱动模块配置
            Id = config.Id;
            Name = config.Name;
            Config = config;

            // 驱动模块的点表
            this.PointSheet = new PointSheet();

            // 驱动模块专用设备表
            specDevices = new Dictionary<string, Device>();
            specDeviceTypes = new Dictionary<string, Type>();
        }

        // ###点表管理
        // 创建一个新的点，需要具体驱动模块实现创建具体的PointItem
        public abstract PointItem CreatePointItem(string deviceName, string variableName);

        // 发送点值到对方系统，需要具体驱动模块实现逻辑(PointItem要AS到具体类，就有具体驱动地址、IO等信息）
        protected abstract SendPointValueResult DoSendPointValue<TValue>(PointItem pointItem, TValue value);

        // 读点值从对方系统，需要具体驱动模块实现逻辑(PointItem要AS到具体类，就有具体驱动地址、IO等信息）
        protected abstract (SendPointValueResult, TValue) DoReadPointValue<TValue>(PointItem pointItem, TValue defalutValue);

        // 启动读取动态属性，给具体驱动模块实现。也可以这函数直接初始化设备属性
        public abstract void StartReadDynamicProperty();
        public abstract void StopReadDynamicProperty();

        // 设置点值，给具体驱动模块调用，涉及内部状态（变量）更新
        protected void SetPointValue<TValue>(PointItem pointItem, TValue value)
        {
            DeviceManager.GetDevice(pointItem.DeviceName)?.SetAttribute(pointItem.VariableName, value);
        }

        // 给外部调用，发送点值到对方系统
        public SendPointValueResult SendPointValue<TValue>(PointItem pointItem, TValue value)
        {
            // 根据点名查询点
            if (pointItem != null)
            {
                return DoSendPointValue(pointItem, value);
            }
            return SendPointValueResult.InternalError;
        }
        public SendPointValueResult SendPointValue<TValue>(string deviceName, string variableName, TValue value)
        {
            // 根据点名查询点
            var pointItem = FindPointItem(deviceName, variableName);
            if (pointItem != null)
            {
                return SendPointValue(pointItem, value);
            }
            return SendPointValueResult.InternalError;
        }

        // 读点值从对方系统，读到后(SentAndAcknowledged 对方表示处理成功)更新内部状态（变量）
        public (SendPointValueResult, TValue) ReadPointValue<TValue>(string deviceName, string variableName, TValue defalutValue)
        {
            SendPointValueResult result = SendPointValueResult.InternalError;
            // 根据点名查询点
            var pointItem = FindPointItem(deviceName, variableName);
            if (pointItem != null)
            {
                (SendPointValueResult rst, TValue value) = DoReadPointValue<TValue>(pointItem, defalutValue);
                if (rst == SendPointValueResult.SentAndAcknowledged)
                {
                    SetPointValue(pointItem, value);
                }
                result = rst;
            }
            return (result, defalutValue);
        }

        public void AddPointItem(PointItem point)
        {
            PointSheet.AddPointItem(point);
        }

        public void RemovePointItem(string deviceName, string variableName)
        {
            PointSheet.RemovePointItem(deviceName, variableName);
        }

        public PointItem FindPointItem(string deviceName, string variableName)
        {
            return PointSheet.FindPoint(deviceName, variableName);
        }

        public PointItem FindPointMeasure(string deviceName)
        {
            return PointSheet.FindPoint(deviceName, ConstHelper.VariableNameValue);
        }

        // ###专用设备的管理
        // 注册新的专用设备
        public void RegisterSpecDevice(Device device, Type deviceType)
        {
            if (!VariableManager.HasVariableTable(device.Name))
            {
                var variableTable = VariableManager.GetVariableTable(device.Name);
                if (variableTable == null)
                {
                    variableTable = DeviceTemplateManager.CreateVariableTableByType(deviceType, device.Name);
                }
                if (variableTable != null)
                {
                    device.AssignVariableTable(variableTable);
                }
            }
            specDevices[device.Name] = device;
            specDeviceTypes[device.Name] = deviceType;
        }

        // 删除专用设备
        public bool UnregisterSpecDevice(string name)
        {
            if (specDevices.ContainsKey(name))
            {
                specDevices.Remove(name);
                specDeviceTypes.Remove(name);
                // 清除与设备关联的变量表
                VariableManager.RemoveVariableTable(name);
                return true;
            }
            return false;
        }

        // 删除专用设备
        public bool RemoveSpecDevice(string name)
        {
            return UnregisterSpecDevice(name);
        }

        // 更新现有专用设备
        public bool UpdateSpecDevice(string name, Device newDevice)
        {
            if (specDevices.ContainsKey(name))
            {
                specDevices[name] = newDevice;
                // 如果需要，也可以更新 deviceTypes
                return true;
            }
            return false;
        }

        // 通过名称查询专用设备
        public Device GetSpecDevice(string name)
        {
            if (specDevices.TryGetValue(name, out var device))
            {
                return device;
            }
            return null;
        }

        // 列出所有专用设备
        public List<Device> GetAllSpecDevices()
        {
            return specDevices.Values.ToList();
        }

        // 返回不满足条件的第一个变量名称
        public (bool, string, string) AllVariablesHaveInitialAndUpdatedValues()
        {
            // 检查 specDevices 中的每个设备的变量
            foreach (var deviceEntry in specDevices)
            {
                var device = deviceEntry.Value;
                var variableTableResult = device.GetVariableTable().AllVariablesHaveInitialAndUpdatedValues();
                if (!variableTableResult.Item1)
                {
                    // 如果有变量未初始化或未更新，返回该变量名称
                    return (false, variableTableResult.Item2, device.Name);
                }
            }

            // 使用 GetDeviceNameList 获取全部设备名称
            var allDeviceNames = PointSheet.GetDeviceNameList();

            // 遍历每个设备名称，检查对应设备的变量状态
            foreach (var deviceName in allDeviceNames)
            {
                var device = GetSpecDevice(deviceName);
                if (device != null)
                {
                    var variableTableResult = device.GetVariableTable().AllVariablesHaveInitialAndUpdatedValues();
                    if (!variableTableResult.Item1)
                    {
                        // 如果找到未初始化或未更新的变量，返回 false 和变量名称
                        return (false, variableTableResult.Item2, device.Name);
                    }
                }
            }

            return (true, string.Empty, string.Empty); // 所有变量都已初始化且更新
        }

        // 保存 与 加载 点表
        public void SavePointsToFile(string filePath)
        {
            // 判断目录不存在，创建目录
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(filePath))
            {
                foreach (var point in PointSheet.Points) // 假设 PointSheet.Points 是存储 PointItem 对象的集合
                {
                    var context = new Dictionary<string, string>();
                    PointToContext(context, point);
                    var line = string.Join(";", context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    writer.WriteLine(line);
                }
            }
        }

        public void LoadPointsFromFile(string filePath)
        {
            // 改变后缀名为 .csv，如果存在，就按 .csv 处理，否则按 .conf 处理
            var csvFilePath = FileSystemHelper.ChangeExtension(filePath, ".csv");
            if (File.Exists(csvFilePath))
            {
                LoadPointsFromFileByCsv(csvFilePath);
            }
            else
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    throw new Exception($"{Name}: 加载点表文件 '{filePath}' 不存在。");
                }
                LoadPointsFromFileByConf(filePath);
            }
        }

        [Obfuscation(Exclude = true)]
        private void LoadPointsFromFileByCsv(string filePath)
        {
            var encoding = Encoding.UTF8;
            if (File.Exists(filePath) && EncodingHelper.GuessFileEncoding(filePath) == Encoding.GetEncoding("GBK"))
            {
                encoding = Encoding.GetEncoding("GBK");
            }

            var lines = File.ReadAllLines(filePath, encoding);
            if (lines.Length < 2) // 确保文件不仅有头部，还包含至少一行数据
            {
                LogHelper.Debug($"{Name}: 加载点表文件 '{filePath}' 仅包含 {lines.Length} 行数据。");
                return;
            }

            // 使用文件的第一行作为键
            var headers = lines[0].Split(',');

            PointSheet.Points.Clear(); // 清除现有点项

            // 从第二行开始处理数据行
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                if (values.Length != headers.Length)
                {
                    throw new Exception($"{Name}: 加载点表文件 '{filePath}' 的第 {i + 1} 行数据的列数与头部不匹配。");
                }

                // 将列名和值组合为字典
                var context = headers.Zip(values, (header, value) => new { header, value })
                                     .ToDictionary(k => k.header, k => k.value);

                var pointItem = PointFromContext(context); // 根据上下文创建 PointItem 对象
                if (pointItem != null && pointItem.DeviceName.Count() > 1)
                {
                    PointSheet.AddPointItem(pointItem);
                }
            }

            // 加载点表完成后的操作
            LoadPointsAfter();
        }

        private void LoadPointsFromFileByConf(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            PointSheet.Points.Clear(); // 清除现有点项

            foreach (var line in lines)
            {
                var context = line.Split(';').Select(part => part.Split('=')).ToDictionary(split => split[0], split => split[1]);
                var pointItem = PointFromContext(context); // 假设 CreatePointItem(Dictionary<string, string> context) 能根据上下文创建 PointItem 对象
                if (pointItem != null)
                {
                    PointSheet.AddPointItem(pointItem);
                }
            }
            // 加载点表完成后，通知驱动模块更新点表
            LoadPointsAfter();
        }

        protected virtual void LoadPointsAfter() { }

        // 根据上下文（键值对的行文本）创建 PointItem 对象
        public bool PointToContext(Dictionary<string, string> context, PointItem pointItem)
        {
            context["DeviceName"] = pointItem.DeviceName;
            context["VariableName"] = pointItem.VariableName;
            context["Description"] = pointItem.Description;
            context["CanRead"] = pointItem.CanRead.ToString();
            context["CanWrite"] = pointItem.CanWrite.ToString();
            context["CanControl"] = pointItem.CanControl.ToString();
            context["CanEvent"] = pointItem.CanEvent.ToString();
            context["CanAlarm"] = pointItem.CanAlarm.ToString();
            context["TagsString"] = pointItem.TagsString.ToString();
            if (PointToContextImpl(context, pointItem))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // 根据上下文（键值对的行文本）创建 PointItem 对象
        // 一个设备就一个 PointItem，variableName 默认为 ConstHelper.VariableNameValue
        // CanRead、CanWrite、CanControl、CanEvent 默认为 false
        public PointItem PointFromContext(Dictionary<string, string> context)
        {
            try
            {
                var deviceName = context["DeviceName"].Trim();    
                if (deviceName.Length <= 1)
                {
                    return null;
                }
                if (context.TryGetValue("VariableName", out string variableName))
                {
                    variableName = variableName.Trim();
                    if (string.IsNullOrEmpty(variableName)) { variableName = ConstHelper.VariableNameValue; }
                }
                else
                {
                    // 默认值为 VariableNameValue
                    variableName = ConstHelper.VariableNameValue;
                }
                var pointItem = CreatePointItem(deviceName, variableName);
                if (context.TryGetValue("Description", out string description))
                {
                    description = description.Trim();
                    pointItem.Description = description;
                }
                else
                {
                    pointItem.Description = "";
                }
                // 读取属性：CanRead，默认值为 true
                if (context.TryGetValue("CanRead", out string canReadString))
                {
                    canReadString = canReadString.Trim();
                    pointItem.CanRead = string.IsNullOrEmpty(canReadString) ? true : bool.Parse(canReadString);
                }
                else
                {
                    pointItem.CanRead = true;
                }
                // 读取属性：CanWrite，默认值为 false
                if (context.TryGetValue("CanWrite", out string canWriteString))
                {
                    canWriteString = canWriteString.Trim();
                    pointItem.CanWrite = string.IsNullOrEmpty(canWriteString) ? false : bool.Parse(canWriteString);
                }
                else
                {
                    pointItem.CanWrite = false;
                }
                // 读取属性：CanControl，默认值为 false
                if (context.TryGetValue("CanControl", out string canControlString))
                {
                    canControlString = canControlString.Trim();
                    pointItem.CanControl = string.IsNullOrEmpty(canControlString) ? false : bool.Parse(canControlString);
                }
                else
                {
                    pointItem.CanControl = false;
                }
                // 读取属性：CanEvent，默认值为 false
                if (context.TryGetValue("CanEvent", out string canEventString))
                {
                    canEventString = canEventString.Trim();
                    pointItem.CanEvent = string.IsNullOrEmpty(canEventString) ? false : bool.Parse(canEventString);
                }
                else
                {
                    pointItem.CanEvent = string.IsNullOrEmpty(canEventString) ? false : bool.Parse(canEventString);
                }
                // 读取属性：CanEvent，默认值为 false
                if (context.TryGetValue("CanAlarm", out string canAlarnString))
                {
                    canAlarnString = canAlarnString.Trim();
                    pointItem.CanAlarm = string.IsNullOrEmpty(canAlarnString) ? false : bool.Parse(canAlarnString);
                }
                else
                {
                    pointItem.CanAlarm = string.IsNullOrEmpty(canAlarnString) ? false : bool.Parse(canAlarnString);
                }
                // 读取属性：TagsString，默认值为 空
                if (context.TryGetValue("TagsString", out string tagsString))
                {
                    tagsString = tagsString.Trim();
                    pointItem.TagsString = tagsString;
                }
                else
                {
                    pointItem.TagsString = "";
                }
                if (PointFromContextImpl(context, pointItem))
                {
                    return pointItem;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
            
        }

        protected abstract bool PointFromContextImpl(Dictionary<string, string> context, PointItem pointItem);

        protected abstract bool PointToContextImpl(Dictionary<string, string> context, PointItem pointItem);

    }

    public enum SendPointValueResult
    {
        SentButUnknown, // 已发送，但不知道对方是否成功处理
        SentAndAcknowledged, // 已发送，对方表示处理成功
        SendFailed, // 发送失败
        InternalError // 内部错误，例如点不在点表中
    }

    public class DriverModuleConfig
    {
        [Category("基础"), DisplayName("ID")]
        public long Id { get; set; } // DriverModuleConfig 的Id

        [Category("基础"), DisplayName("名称")]
        public string Name { get; set; } // DriverModuleConfig 的名称

        public DriverModuleConfig(long id, string name)
        {
            Id = id;
            Name = name;
        }
    }

}
