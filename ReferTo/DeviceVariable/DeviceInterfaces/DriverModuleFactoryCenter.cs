using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class DriverModuleFactoryCenter
    {
        // 定义一个工厂信息的结构
        public class FactoryInfo
        {
            public Func<DriverModuleConfig, DriverModuleBase> CreateInstance { get; set; }
            
            [DisplayName("类型名称")] 
            public string TypeName { get; set; }
            
            [DisplayName("类型标题")] 
            public string Title { get; set; }
        }

        // 存储所有注册的工厂
        private static Dictionary<string, FactoryInfo> factories = new Dictionary<string, FactoryInfo>();

        // 注册工厂的方法
        public static void RegisterFactory(string typeName, string title, Func<DriverModuleConfig, DriverModuleBase> createInstance)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("typeName cannot be null or empty", nameof(typeName));

            if (createInstance == null)
                throw new ArgumentNullException(nameof(createInstance), "createInstance cannot be null");

            if (factories.ContainsKey(typeName))
                throw new InvalidOperationException($"A factory for type {typeName} has already been registered.");

            factories[typeName] = new FactoryInfo { TypeName = typeName, Title = title, CreateInstance = createInstance };
        }

        // 根据类型名创建对象的方法
        public static DriverModuleBase CreateInstance(string typeName, DriverModuleConfig config)
        {
            if (factories.TryGetValue(typeName, out var factoryInfo))
            {
                return factoryInfo.CreateInstance(config);
            }
            else
            {
                throw new InvalidOperationException($"No factory registered for type {typeName}.");
            }
        }

        public static List<FactoryInfo> GetFactories() => factories.Values.ToList<FactoryInfo>() ?? new List<FactoryInfo>();

    }
}
