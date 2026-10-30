using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CxVisual.Professional.HMI
{
    /// <summary>HMI只消费Runtime.Host scada-project-check的正式只读结果。</summary>
    [DataContract]
    public sealed class PHMScadaEngineeringPoint
    {
        [DataMember] public string StablePointId { get; set; }
        [DataMember] public string VariableAddress { get; set; }
        [DataMember] public string ValueType { get; set; }
        [DataMember] public string Unit { get; set; }
        [DataMember] public bool WritableDefinition { get; set; }
    }
    [DataContract]
    public sealed class PHMScadaEngineeringDevice
    {
        [DataMember] public string DeviceRef { get; set; }
        [DataMember] public string ScopeId { get; set; }
        [DataMember] public string ConnectorModuleId { get; set; }
        [DataMember] public string ConnectorEffectiveVersion { get; set; }
        [DataMember] public string ChannelId { get; set; }
        [DataMember] public int ScanIntervalMs { get; set; }
        [DataMember] public bool Contiguous { get; set; }
        [DataMember] public PHMScadaEngineeringPoint[] Points { get; set; }
    }
    [DataContract]
    public sealed class PHMScadaEngineeringInspection
    {
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember] public string DeploymentVersion { get; set; }
        [DataMember] public bool DeviceCommunicationValidated { get; set; }
        [DataMember] public PHMScadaEngineeringDevice[] Devices { get; set; }
        /// <summary>校验Host输出身份与预算，不重新实现MEM/MHS专业配置校验。</summary>
        public static PHMScadaEngineeringInspection Parse(string json)
        {
            if(String.IsNullOrWhiteSpace(json)||Encoding.UTF8.GetByteCount(json)>1048576)
                throw new InvalidDataException("hmi.preflight.reply_budget");
            PHMScadaEngineeringInspection report;
            using(MemoryStream input=new MemoryStream(Encoding.UTF8.GetBytes(json),false))
                report=(PHMScadaEngineeringInspection)
                    new DataContractJsonSerializer(typeof(PHMScadaEngineeringInspection))
                        .ReadObject(input);
            if(report==null||report.SchemaVersion!=1||
                report.Status!="EngineeringValidated"||
                String.IsNullOrWhiteSpace(report.DeploymentVersion)||
                report.DeviceCommunicationValidated||report.Devices==null||
                report.Devices.Length>256)
                throw new InvalidDataException("hmi.preflight.invalid_report");
            HashSet<Guid> ids=new HashSet<Guid>();
            HashSet<string> scopes=new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> modules=new HashSet<string>(StringComparer.Ordinal);
            foreach(PHMScadaEngineeringDevice device in report.Devices)
            {
                Guid id;
                if(device==null||!Guid.TryParse(device.DeviceRef,out id)||id==Guid.Empty||
                   !ids.Add(id)||String.IsNullOrWhiteSpace(device.ScopeId)||
                   String.IsNullOrWhiteSpace(device.ConnectorModuleId)||
                   String.IsNullOrWhiteSpace(device.ConnectorEffectiveVersion)||
                   String.IsNullOrWhiteSpace(device.ChannelId)||
                   !scopes.Add(device.ScopeId)||!modules.Add(device.ConnectorModuleId)||
                   device.ScanIntervalMs<=0||device.Points==null||device.Points.Length>64)
                    throw new InvalidDataException("hmi.preflight.invalid_device");
                HashSet<string> points=new HashSet<string>(StringComparer.Ordinal);
                foreach(PHMScadaEngineeringPoint p in device.Points)
                    if(p==null||String.IsNullOrWhiteSpace(p.StablePointId)||
                       String.IsNullOrWhiteSpace(p.VariableAddress)||
                       String.IsNullOrWhiteSpace(p.ValueType)||
                       !points.Add(p.StablePointId))
                        throw new InvalidDataException("hmi.preflight.invalid_point");
            }
            return report;
        }
    }
}
