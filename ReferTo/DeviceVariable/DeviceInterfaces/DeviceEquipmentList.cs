using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    // 设备模板列表，deviceTypeName等同模板名称
    public static class DeviceEquipmentList
    {
        public const string MeasureIntegerName = "MeasureInteger";
        public static readonly DeviceTemplate MeasureInteger = new DeviceTemplate(
                    deviceConfigType: typeof(DeviceConfig),
                    variableDescriptions: VariableAttributeHelper.CreateVariableDescriptions(typeof(MeasureIntegerConfig)).ToList<DeviceVariableDescription>(),
                    deviceType: typeof(MeasureInteger),
                    deviceTypeName: MeasureIntegerName,
                    deviceTypeTitle: "整型量",
                    isSpec: false,
                    moduleName: string.Empty
                );

        public const string MeasureLongName = "MeasureLong";
        public static readonly DeviceTemplate MeasureLong = new DeviceTemplate(
                deviceConfigType: typeof(DeviceConfig),
                variableDescriptions: VariableAttributeHelper.CreateVariableDescriptions(typeof(MeasureLongConfig)).ToList<DeviceVariableDescription>(),
                deviceType: typeof(MeasureLong),
                deviceTypeName: MeasureLongName,
                deviceTypeTitle: "长整型量",
                isSpec: false,
                moduleName: string.Empty
            );

        public const string MeasureDoubleName = "MeasureDouble";
        public static readonly DeviceTemplate MeasureDouble = new DeviceTemplate(
                deviceConfigType: typeof(DeviceConfig),
                variableDescriptions: VariableAttributeHelper.CreateVariableDescriptions(typeof(MeasureDoubleConfig)).ToList<DeviceVariableDescription>(),
                deviceType: typeof(MeasureDouble),
                deviceTypeName: MeasureDoubleName,
                deviceTypeTitle: "浮点量",
                isSpec: false,
                moduleName: string.Empty
            );

        public const string MeasureStringName = "MeasureString";
        public static readonly DeviceTemplate MeasureString = new DeviceTemplate(
                deviceConfigType: typeof(DeviceConfig),
                variableDescriptions: VariableAttributeHelper.CreateVariableDescriptions(typeof(MeasureStringConfig)).ToList<DeviceVariableDescription>(),
                deviceType: typeof(MeasureString),
                deviceTypeName: MeasureStringName,
                deviceTypeTitle: "字符量",
                isSpec: false,
                moduleName: string.Empty
            );


        public static void RegisterAllDevice()
        {
            DeviceTemplateManager.AddTemplate(MeasureInteger);
            DeviceTemplateManager.AddTemplate(MeasureLong);
            DeviceTemplateManager.AddTemplate(MeasureDouble);
            DeviceTemplateManager.AddTemplate(MeasureString);
        }
    }
}
