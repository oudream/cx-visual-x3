using System;
using CxVisual.Foundation.Data.Messages;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>仅依赖Foundation.Data.Messages的消息契约可独立使用。</summary>
    internal static class FoundationMessageContractsTests
    {
        private sealed class StringCodec : IMNMMessageCodec
        {
            public string ContentType { get { return "text/plain"; } }
            public byte[] Encode(object payload, Type type)
            { return System.Text.Encoding.UTF8.GetBytes((string)payload); }
            public object Decode(byte[] bytes, Type type)
            { return System.Text.Encoding.UTF8.GetString(bytes); }
        }

        public static void Run()
        {
            MNMMessageTypeDefinition definition = new MNMMessageTypeDefinition(
                "Camera.Captured", 1, typeof(string));
            MNMMessageTypeDefinition bound = definition.WithTopic(
                "/machine/1201/app/XRay/module/Camera/message/Captured");
            if (definition.Topic != null || bound.Topic == null)
                throw new InvalidOperationException("Foundation definition must be reusable and bindable");
            bound.Validate("Image001");
            if (!new MNMTopicFilter("/machine/1201/app/XRay/#").Matches(bound.Topic))
                throw new InvalidOperationException("Foundation MQTT filter contract mismatch");
            IMNMMessageCodec codec = new StringCodec();
            byte[] bytes = codec.Encode("Camera ready",typeof(string));
            if ((string)codec.Decode(bytes,typeof(string)) != "Camera ready")
                throw new InvalidOperationException("Foundation codec interface unusable");
            Console.WriteLine("PASS BUS.FOUNDATION message-only contracts / MQTT / codec interface");
        }
    }
}
