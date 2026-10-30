using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Professional.HMI
{
    /// <summary>
    /// HMI专业只读写入意图；仅为受信Host的MRK Schema提供候选参数，
    /// 不携带操作员认证、不创建MRK OperationId、不授权或执行PLC控制。
    /// </summary>
    public sealed class PHMHmiPreparedWrite
    {
        internal PHMHmiPreparedWrite(Guid projectId,int revision,
            MRIObjectAddress buttonAddress,string actionId,
            PHMScadaDeviceIdentity identity,string pointId,ushort value)
        {
            ProjectId=projectId;ProjectRevision=revision;ButtonAddress=buttonAddress;
            ActionId=actionId;DeviceRef=identity.DeviceRef;
            ScopeId=identity.ScopeId;DeploymentVersion=identity.DeploymentVersion;
            ConfigVersion=identity.ConfigVersion;SessionGeneration=identity.Generation;
            StablePointId=pointId;Value=value;
            Inputs=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>
                (StringComparer.Ordinal) {
                {"StableId",pointId},{"Value",value},
                {"EffectiveVersion",identity.ConfigVersion},
                {"SessionGeneration",identity.Generation}
            });
        }
        public Guid ProjectId {get;private set;}
        public int ProjectRevision {get;private set;}
        public MRIObjectAddress ButtonAddress {get;private set;}
        public string ActionId {get;private set;}
        public Guid DeviceRef {get;private set;}
        public string ScopeId {get;private set;}
        public string DeploymentVersion {get;private set;}
        public string ConfigVersion {get;private set;}
        public string SessionGeneration {get;private set;}
        public string StablePointId {get;private set;}
        public ushort Value {get;private set;}
        public IReadOnlyDictionary<string,object> Inputs {get;private set;}
    }

    public static class PHMHmiWritePreparation
    {
        /// <summary>
        /// 必须逐次接收最新Host身份及只读点位Quality。Generation/Effective/Device
        /// 有任一漂移、坏品质、缺失已保存动作或无人工确认，一律拒绝。
        /// 此预检并非运行权限，真正MRK Host必须再次认证/检查版本/点级授权。
        /// </summary>
        public static PHMHmiPreparedWrite Prepare(
            MEMEngineeringSession engineering,int expectedRevision,
            MVIVisualActionInvocation savedClick,string expectedActionId,
            PHMScadaDeviceIdentity selectedIdentity,
            PHMScadaDeviceIdentity currentIdentity,
            IReadOnlyList<PHMScadaPointObservation> currentPoints,
            string operatorValue,bool confirmed,
            TimeSpan maximumAge,DateTime nowUtc)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(savedClick==null||savedClick.Address==null||
                !String.Equals(savedClick.EventId,"Click",StringComparison.Ordinal)||
                String.IsNullOrWhiteSpace(expectedActionId)||
                !String.Equals(savedClick.ActionId,expectedActionId,StringComparison.Ordinal))
                throw new InvalidOperationException("hmi.write.saved_click_required");
            if(engineering.IsClosed||engineering.Revision!=expectedRevision||
                savedClick.Address.ProjectId!=engineering.Project.Id)
                throw new InvalidOperationException("hmi.write.project_revision_stale");
            if(!(engineering.Resolve(savedClick.Address) is MRCWidgetElement)||
                ((MRCWidgetElement)engineering.Resolve(savedClick.Address)).TypeId!=
                    "core.widget.button")
                throw new InvalidOperationException("hmi.write.button_identity_invalid");
            if(!confirmed)throw new InvalidOperationException("hmi.write.operator_confirmation_required");
            if(selectedIdentity==null||currentIdentity==null||
                !selectedIdentity.Running||!currentIdentity.Running||
                selectedIdentity.DeviceRef!=currentIdentity.DeviceRef||
                !String.Equals(selectedIdentity.ScopeId,currentIdentity.ScopeId,StringComparison.Ordinal)||
                !String.Equals(selectedIdentity.DeploymentVersion,
                    currentIdentity.DeploymentVersion,StringComparison.Ordinal)||
                !String.Equals(selectedIdentity.ConfigVersion,
                    currentIdentity.ConfigVersion,StringComparison.Ordinal)||
                !String.Equals(selectedIdentity.Generation,
                    currentIdentity.Generation,StringComparison.Ordinal))
                throw new InvalidOperationException("hmi.write.device_version_or_generation_stale");
            string pointId=savedClick.ParameterBindingId;
            if(String.IsNullOrWhiteSpace(pointId)||currentPoints==null)
                throw new InvalidOperationException("hmi.write.point_binding_missing");
            PHMScadaPointObservation observation=currentPoints
                .FirstOrDefault(x=>x!=null&&x.StablePointId==pointId);
            if(observation==null||
                currentPoints.Count(x=>x!=null&&x.StablePointId==pointId)!=1||
                observation.ValueTypeName!="UInt16"||
                observation.EffectiveQuality(maximumAge,nowUtc)!=MDSVariableQuality.Good)
                throw new InvalidOperationException("hmi.write.point_type_or_quality_invalid");
            ushort value;
            if(String.IsNullOrEmpty(operatorValue)||operatorValue!=operatorValue.Trim()||
                !ushort.TryParse(operatorValue,NumberStyles.None,
                    CultureInfo.InvariantCulture,out value))
                throw new ArgumentOutOfRangeException("operatorValue",
                    "hmi.write.uint16_setpoint_invalid");
            return new PHMHmiPreparedWrite(engineering.Project.Id,expectedRevision,
                savedClick.Address,expectedActionId,currentIdentity,pointId,value);
        }
    }
}
