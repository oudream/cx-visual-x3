using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CxVisual.Runtime.Host
{
    // 工程预检只引用正式MEM/MHS/Connector/MDS装配产物；不启动Channel、不发送PLC指令。
    // 序列化仅输出身份、点位定义和已批准配置版本，不含IP、凭据或写命令参数。
    [DataContract]
    internal sealed class MRHScadaEngineeringInspection
    {
        [DataMember] public int SchemaVersion {get;set;}
        [DataMember] public string Status {get;set;}
        [DataMember] public string DeploymentVersion {get;set;}
        [DataMember] public bool DeviceCommunicationValidated {get;set;}
        [DataMember] public MRHScadaEngineeringDevice[] Devices {get;set;}
    }

    [DataContract]
    internal sealed class MRHScadaEngineeringDevice
    {
        [DataMember] public string DeviceRef {get;set;}
        [DataMember] public string ScopeId {get;set;}
        [DataMember] public string ConnectorModuleId {get;set;}
        [DataMember] public string ConnectorEffectiveVersion {get;set;}
        [DataMember] public string ChannelId {get;set;}
        [DataMember] public int ScanIntervalMs {get;set;}
        [DataMember] public bool Contiguous {get;set;}
        [DataMember] public MRHScadaEngineeringPoint[] Points {get;set;}
    }

    [DataContract]
    internal sealed class MRHScadaEngineeringPoint
    {
        [DataMember] public string StablePointId {get;set;}
        [DataMember] public string VariableAddress {get;set;}
        [DataMember] public string ValueType {get;set;}
        [DataMember] public string Unit {get;set;}
        [DataMember] public bool WritableDefinition {get;set;}
    }

    internal static class MRHScadaEngineeringPreflight
    {
        // 对保存工程重新装载正式Deployment Effective，再由原Group Owner
        // 完整验证MEM DeviceRef、Connector和唯一MDS映射，而非再写一套校验器。
        internal static MRHScadaEngineeringInspection Inspect(string projectPath,string configRoot)
        {
            using(MRHScadaRuntimeGroup group=MRHScadaRuntimeGroup.OpenFromEngineering(
                projectPath,configRoot))
            {
                MRHScadaRuntime[] devices=group.SnapshotDevices();
                MRHScadaDeviceLaunch[] launches=group.SnapshotLaunches();
                if(String.IsNullOrEmpty(group.DeploymentVersion)||
                    devices.Length!=launches.Length ||
                    !group.IsDeploymentApproved ||
                    devices.Any(x=>!x.IsStillApproved || x.IsRunning))
                    throw new InvalidOperationException(
                        "SCADA工程预检的项目生效版本或未启动会话不一致");
                MRHScadaEngineeringDevice[] rows=new MRHScadaEngineeringDevice[devices.Length];
                for(int i=0;i<rows.Length;i++)
                {
                    MRHScadaRuntime device=devices[i];
                    MRHScadaDeviceLaunch launch=launches[i];
                    if(device.DeviceRef!=launch.DeviceNodeId.ToString("N")||
                        device.ScopeId!=launch.ScopeId)
                        throw new InvalidOperationException("SCADA工程预检设备身份顺序失配");
                    rows[i]=new MRHScadaEngineeringDevice
                    {
                        DeviceRef=device.DeviceRef,
                        ScopeId=device.ScopeId,
                        ConnectorModuleId=launch.ConnectorModuleId,
                        ConnectorEffectiveVersion=device.ConfigVersion,
                        ChannelId=device.Device.ChannelId,
                        ScanIntervalMs=launch.IntervalMs,
                        Contiguous=launch.Contiguous,
                        Points=device.Device.PointDefinitions.Select(p=>
                            new MRHScadaEngineeringPoint
                            {
                                StablePointId=p.StableId,
                                VariableAddress=p.Address,
                                ValueType=p.ValueType.Name,
                                Unit=p.Unit,
                                WritableDefinition=p.Writable
                            }).ToArray()
                    };
                }
                // 核验结束前重新读取磁盘版本，禁止装配过程中被其它Studio覆盖。
                if(!group.IsDeploymentApproved||devices.Any(x=>!x.IsStillApproved))
                    throw new InvalidOperationException("SCADA工程预检期间部署版本变化");
                return new MRHScadaEngineeringInspection
                {
                    SchemaVersion=1,
                    Status="EngineeringValidated",
                    DeploymentVersion=group.DeploymentVersion,
                    DeviceCommunicationValidated=false,
                    Devices=rows
                };
            }
        }

        internal static string ToJson(MRHScadaEngineeringInspection inspection)
        {
            if(inspection==null)throw new ArgumentNullException("inspection");
            using(MemoryStream stream=new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(MRHScadaEngineeringInspection))
                    .WriteObject(stream,inspection);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        internal static int Run(string projectPath,string configRoot)
        {
            Console.Out.WriteLine(ToJson(Inspect(projectPath,configRoot)));
            return 0;
        }
    }
}
