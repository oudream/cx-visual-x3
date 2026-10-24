using System;
namespace CxVisual.Mechanism.Data.Messages
{
    /// <summary>M36可选的强类型消息定义；本阶段尚无Publish/Subscribe实现。</summary>
    public sealed class MNMMessageTypeDefinition
    {
        public MNMMessageTypeDefinition(string typeId,int schemaVersion,Type payloadType)
        {
            if(string.IsNullOrWhiteSpace(typeId))throw new ArgumentException("类型不能为空","typeId");
            if(schemaVersion<1)throw new ArgumentOutOfRangeException("schemaVersion");
            if(payloadType==null||payloadType==typeof(void)||payloadType.IsPointer||
                payloadType.ContainsGenericParameters)throw new ArgumentException("消息载荷类型无效","payloadType");
            TypeId=typeId;SchemaVersion=schemaVersion;PayloadType=payloadType;
        }
        public string TypeId{get;private set;}
        public int SchemaVersion{get;private set;}
        public Type PayloadType{get;private set;}
        public void Validate(object payload)
        {
            if(payload==null||!PayloadType.IsInstanceOfType(payload))
                throw new ArgumentException("消息载荷类型不匹配","payload");
        }
    }
}
