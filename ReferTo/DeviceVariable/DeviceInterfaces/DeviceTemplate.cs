using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    // 设备模板、设备模板管理器。
    // 设备模板有系统内部、有自定义。
    // 系统内部是收集（例如：AxisVariableAssociation、CylinderVariableAssociation、VectorVariableAssociation等）信息而来
    // 自定义是让用户输入属性名、数据类型、最大值、最小值、Title、枚举状态类型。
    public class DeviceTemplate
    {
        [DisplayName("模板名")] // 设置列头为 "模板名"
        public string Name { get; private set; }

        [DisplayName("模板标题")] // 设置列头为 "模板标题"
        public string Title { get; private set; }

        [Browsable(false)] // 不在 DataGridView 中显示
        public Type DeviceConfigType { get; private set; }

        [DisplayName("设备配置类")] // 设置列头为 "设备配置类"
        public string DeviceConfigTypeName { get; private set; }

        [Browsable(false)] // 不在 DataGridView 中显示
        public List<DeviceVariableDescription> VariableDescriptions { get; private set; }

        [Browsable(false)]
        public Type DeviceType { get; private set; }

        [DisplayName("设备类")]
        public string DeviceTypeName { get; private set; }

        // 是否专用设备
        [DisplayName("是否专用")]
        public bool IsSpec { get; private set; }

        // 设备类
        [Browsable(false)] // 不在 DataGridView 中显示
        public bool TypeIsDevice { get; private set; }

        // 所属驱动模块
        [DisplayName("驱动模块")]
        public string ModuleName { get; private set; }

        public DeviceTemplate(Type deviceConfigType, List<DeviceVariableDescription> variableDescriptions,
            Type deviceType,
            string deviceTypeName,
            string deviceTypeTitle,
            bool isSpec,
            string moduleName)
        {
            DeviceConfigType = deviceConfigType;
            VariableDescriptions = variableDescriptions;
            DeviceType = deviceType;
            Name = deviceTypeName;
            Title = deviceTypeTitle;
            IsSpec = isSpec;
            TypeIsDevice = !(deviceType == typeof(Device));
            ModuleName = moduleName;

            //
            DeviceConfigTypeName = StringHelper.ExtractShortNameAfterDot(deviceConfigType.Name);
            DeviceTypeName = StringHelper.ExtractShortNameAfterDot(deviceType.Name);
        }

        // Get the description of a variable
        public DeviceVariableDescription GetVariableDescription(string variableName)
        {
            for (int i = 0; i < VariableDescriptions.Count; i++)
            {
                var description = VariableDescriptions[i];
                if (description.Name == variableName)
                {
                    return description;
                }
            }
            return null;
        }

    }

}
