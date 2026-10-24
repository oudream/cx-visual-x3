using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Data.Messages;
using CxVisual.Mechanism.Logic.Commands;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>消息定义与MQTT Topic过滤、IoT结构化命令定义的独立真实断言。</summary>
    internal static class InteractionDefinitionsTests
    {
        private sealed class Captured
        {
            public string ImageRef { get; set; }
            public int Width { get; set; }
        }
        private static void Check(bool ok, string name)
        { if (!ok) throw new InvalidOperationException("BUS.DEFINITION " + name); }
        private static void Reject<T>(Action call) where T : Exception
        {
            bool rejected = false;
            try { call(); } catch (T) { rejected = true; }
            Check(rejected, "expect " + typeof(T).Name);
        }
        public static void Run()
        {
            FoundationMessageContractsTests.Run();
            const string captured = "/machine/1201/app/XRay/module/Camera/message/Captured";
            const string faulted = "/machine/1201/app/XRay/module/Camera/message/Faulted";
            const string anotherMachine = "/machine/1202/app/XRay/module/Camera/message/Captured";
            MNMMessageTypeDefinition type =
                new MNMMessageTypeDefinition("Camera.Captured", 2, typeof(Captured),
                    name: "采图完成", description: "只发布图像定位与元信息");
            Check(type.TopicCode == "Captured" && type.SchemaVersion == 2 &&
                type.Name == "采图完成", "message schema");
            type.Validate(new Captured { ImageRef = "Image1", Width = 4300 });
            type.ValidateTopic(captured);
            const string anotherCamera = "/machine/1202/app/XRay/module/Camera/message/Captured";
            MNMMessageTypeDefinition bound = type.WithTopic(captured);
            Check(bound.Topic == captured && type.Topic == null, "typed definition topic binding");
            bound.ValidateTopic(captured);
            Reject<ArgumentException>(() => bound.ValidateTopic(anotherCamera));
            Reject<ArgumentException>(() => new MNMMessageTypeDefinition(
                "Camera.Captured", 1, typeof(Captured), topic: faulted));
            MNMMessageTypeDefinition textPayload = new MNMMessageTypeDefinition(
                "Log.Text", 1, typeof(string), topicCode: "Captured",
                payloadKind: MNMPayloadKind.Text, maxPayloadBytes: 4);
            Check(textPayload.PayloadKind == MNMPayloadKind.Text &&
                textPayload.ContentType.StartsWith("text/plain"), "text defaults");
            textPayload.Validate("ABC");
            Reject<ArgumentOutOfRangeException>(() => textPayload.Validate("你好"));
            MNMMessageTypeDefinition binaryPayload = new MNMMessageTypeDefinition(
                "Camera.Bytes", 1, typeof(byte[]), topicCode: "Captured",
                payloadKind: MNMPayloadKind.Binary, maxPayloadBytes: 3);
            binaryPayload.Validate(new byte[] { 1, 2, 3 });
            Reject<ArgumentOutOfRangeException>(() => binaryPayload.Validate(new byte[4]));
            Reject<ArgumentException>(() => binaryPayload.Validate("binary"));
            MNMMessageTypeDefinition referencePayload = new MNMMessageTypeDefinition(
                "Data.Reference", 1, typeof(string), topicCode: "Captured",
                payloadKind: MNMPayloadKind.Reference,
                contentType: "application/vnd.cxvisual.dataref");
            referencePayload.Validate("ImageRef001");
            Reject<ArgumentException>(() => new MNMMessageTypeDefinition(
                "Bad.Structured", 1, typeof(byte[]), payloadKind: MNMPayloadKind.Structured));

            Reject<ArgumentException>(() => type.Validate("wrong"));
            Reject<ArgumentException>(() => type.ValidateTopic(faulted));
            Reject<ArgumentException>(() => new MNMMessageTypeDefinition(
                "Camera.Captured", 1, typeof(Captured), topicCode: "Capt+ured"));
            Check(new MNMTopicFilter(captured).Matches(captured), "exact topic");
            Check(new MNMTopicFilter("/machine/1201/app/XRay/module/+/message/Captured").Matches(captured), "single +");
            Check(new MNMTopicFilter("/machine/1201/app/XRay/#").Matches(faulted), "multi #");
            Check(new MNMTopicFilter("/machine/1201/#").Matches(captured), "prefix #");
            Check(new MNMTopicFilter("/#").Matches(captured), "root #");
            Check(new MNMTopicFilter("/machine/+/app/XRay/module/+/message/Captured").Matches(anotherMachine), "multi-machine");
            Check(!new MNMTopicFilter("/machine/1201/#").Matches(anotherMachine), "machine isolation");
            Check(!new MNMTopicFilter("/machine/1201/app/XRay/module/Camera/message/captured").Matches(captured), "case sensitive");
            Reject<ArgumentException>(() => new MNMTopicFilter("/machine/1201/app/XRay/#/Captured"));
            Reject<ArgumentException>(() => new MNMTopicFilter("/machine/1201/app/XRay/module/Ca+mera/message/Captured"));
            Reject<ArgumentException>(() => new MNMTopicFilter("/machine/1201/app/XRay/**"));
            Reject<ArgumentException>(() => MNMTopicFilter.ValidateTopic("/machine/1201/app/XRay/module/Camera/message/+"));

            MCECommandParameterDefinition position = new MCECommandParameterDefinition(
                "Position", typeof(double), true, unit: "mm",
                minValue: 0.0, maxValue: 200.0);
            MCECommandParameterDefinition velocity = new MCECommandParameterDefinition(
                "Velocity", typeof(double), false, true, 20.0,
                unit: "mm/s", description: "速度",
                minValue: 1.0, maxValue: 100.0);
            MCECommandParameterDefinition enabled = new MCECommandParameterDefinition(
                "Enable", typeof(bool), false, true, true);
            MCECommandDefinition command = new MCECommandDefinition(
                91001, "MoveAbs", "绝对运动",
                new[] { position, velocity, enabled },
                new[] { new MCECommandParameterDefinition("ActualPosition", typeof(double), true) },
                schemaVersion: 2, description: "运动轴绝对移动",
                permissionKey: "axis.move", timeoutMilliseconds: 10000);
            Check(command.SchemaVersion == 2 &&
                command.PermissionKey == "axis.move" &&
                command.TimeoutMilliseconds == 10000, "command metadata");
            IReadOnlyDictionary<string, object> args = command.BindInputs(
                new Dictionary<string, object> { { "Position", 0.0 }, { "Enable", false } });
            Check((double)args["Position"] == 0.0 &&
                (double)args["Velocity"] == 20.0 && !(bool)args["Enable"],
                "missing default and explicit zero/false");
            Reject<ArgumentOutOfRangeException>(() => command.BindInputs(
                new Dictionary<string, object> { { "Position", 201.0 } }));
            Reject<ArgumentOutOfRangeException>(() => command.BindInputs(
                new Dictionary<string, object> { { "Position", 3.0 }, { "Velocity", 0.0 } }));
            Reject<ArgumentException>(() => command.BindInputs(
                new Dictionary<string, object> { { "Position", double.NaN } }));
            Reject<ArgumentException>(() => new MCECommandParameterDefinition(
                "Velocity", typeof(double), false, true, 0.0, minValue: 1.0));
            Reject<ArgumentException>(() => new MCECommandParameterDefinition(
                "Position", typeof(double), true, minValue: 20.0, maxValue: 10.0));
            Check(command.ValidateOutputs(new Dictionary<string, object> {
                { "ActualPosition", 100.0 } }).Count == 1, "typed output");
            Reject<ArgumentException>(() => command.ValidateOutputs(
                new Dictionary<string, object> { { "ActualPosition", "bad" } }));
            Console.WriteLine("PASS BUS.DEFINITION MQTT +/# filter and IoT command schema");
        }
    }
}
