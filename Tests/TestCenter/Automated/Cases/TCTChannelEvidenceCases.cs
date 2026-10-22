using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using CxVisual.Mechanism.Connectivity;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTChannelEvidenceCases
    {
        [TCTTest("TCT.M3.CHANNEL.001", "生产Virtual Channel实际TX/RX/分包/CRC失败/恢复",
            TCTTestAspect.Mechanism, "Channel / Protocol", ObjectId = "MCCVirtualChannel")]
        public static void RealVirtualFrames(TCTTestContext test)
        {
            MCCVirtualChannel[] pair = MCCVirtualChannel.CreatePair("Virtual-A", "Virtual-B");
            var codec = new MPALengthFrameCodec();
            var source = new MCCCxChannelSession(pair[0], codec);
            var receiver = new MCCCxChannelSession(pair[1], codec);
            source.Open(); receiver.Open();
            byte[] payload = new byte[] { 0x01, 0x03, 0xA5, 0xFF };
            source.SendPayload(payload, "request-001", 2);
            MCCChannelFrame tx = pair[0].ReadFrames().Single(x => x.Direction == MCCFrameDirection.Tx);
            MCCChannelFrame rx = pair[1].ReadFrames().Single(x => x.Direction == MCCFrameDirection.Rx);
            test.AssertTrue(codec.Decode(tx.Bytes).SequenceEqual(payload) &&
                rx.DecodedPayload.SequenceEqual(payload) && rx.Bytes.SequenceEqual(tx.Bytes),
                "真实分包TX/RX字节与解码一致", true, true);
            test.AssertEqual("request-001", rx.CorrelationId, "真实Frame Correlation");
            byte[] corrupt = codec.Encode(new byte[] { 0x0A, 0x0B });
            corrupt[4] ^= 0xFF;
            test.AssertThrows<InvalidDataException>(() => pair[0].Send(corrupt, "bad-CRC", 3),
                "CRC错误必须真实触发生产Parser异常");
            test.AssertEqual(MCCChannelState.Faulted, receiver.State, "故障接收方进入Faulted");
            test.AssertEqual(1, receiver.Snapshot().Count(x => x.Direction == MCCFrameDirection.Error),
                "故障实际留存Frame错误");
            receiver.Open();
            source.SendPayload(new byte[] { 0x12, 0x34 }, "request-002", 1);
            test.AssertEqual(2, receiver.Snapshot().Count(x => x.Direction == MCCFrameDirection.Rx),
                "Reset后真实再次完成RX");
            receiver.Close();
            test.AssertThrows<IOException>(() => source.SendPayload(new byte[] { 0x99 }, "closed"),
                "断开的Virtual接收方拒绝投递");
            source.Close();
            test.AssertThrows<InvalidOperationException>(() =>
                source.SendPayload(new byte[] { 0x01 }, "after-close"),
                "关闭的Virtual发送方拒绝写入");
            MCCChannelFrame[] all = pair.SelectMany(x => x.ReadFrames()).ToArray();
            byte[] evidence = TCTChannelEvidence.Serialize(all);
            XDocument restored = TCTChannelEvidence.Read(evidence);
            test.AssertEqual(all.Length, restored.Root.Elements("Frame").Count(),
                "真实Owner Frame及错误回放完整");
            test.AssertTrue(restored.Root.Elements("Frame")
                .Any(x => (string)x.Attribute("direction") == "Error"), "错误不得回放为成功", true, true);
            test.ObserveStructured("Channel", "Virtual Channel TX/RX / CRC / Recover",
                "生产Connectivity Owner处理真实字节、分包、错误与再连接；非Real设备HIL",
                pair[0].Id, "Virtual/" + pair[0].Id, source.SessionId.ToString("D"),
                evidence, TCTChannelEvidence.Mime);
        }

        [TCTTest("TCT.M3.CHANNEL.002", "生产帧解析半包粘包与证据篡改反证",
            TCTTestAspect.Mechanism, "Channel / Protocol", ObjectId = "MPALengthFrameParser")]
        public static void ParserAndEvidenceFaults(TCTTestContext test)
        {
            var codec = new MPALengthFrameCodec();
            var parser = new MPALengthFrameParser();
            byte[] a = codec.Encode(new byte[] { 5, 6, 7 });
            byte[] b = codec.Encode(new byte[] { 8, 9 });
            parser.PushBytes(a.Take(3).ToArray());
            byte[] decoded;
            test.AssertTrue(!parser.TryReadFrame(out decoded), "半包不能变成RX", false, false);
            parser.PushBytes(a.Skip(3).Concat(b).ToArray());
            test.AssertTrue(parser.TryReadFrame(out decoded) && decoded.SequenceEqual(new byte[] { 5, 6, 7 }),
                "半包完整后只交付一帧", true, true);
            test.AssertTrue(parser.TryReadFrame(out decoded) && decoded.SequenceEqual(new byte[] { 8, 9 }),
                "粘包可继续读取第二帧", true, true);
            test.AssertTrue(!parser.TryReadFrame(out decoded), "无数据不得产生第三帧", false, false);
            a[a.Length - 1] ^= 0x10;
            parser.PushBytes(a);
            test.AssertThrows<InvalidDataException>(() => parser.TryReadFrame(out decoded),
                "CRC真实拒绝");
            test.AssertThrows<InvalidOperationException>(() => parser.TryReadFrame(out decoded),
                "故障Parser必须显式Reset");
            parser.Reset();
            parser.PushBytes(b);
            test.AssertTrue(parser.TryReadFrame(out decoded), "Reset后可恢复", true, true);
            MCCVirtualChannel[] pair = MCCVirtualChannel.CreatePair("proof-A", "proof-B");
            pair[0].Open(); pair[1].Open();
            pair[0].Send(codec.Encode(new byte[] { 42 }), "proof");
            byte[] proof = TCTChannelEvidence.Serialize(
                pair.SelectMany(x => x.ReadFrames()).ToArray());
            XDocument forged = XDocument.Parse(Encoding.UTF8.GetString(proof));
            forged.Root.SetAttributeValue("schema", 2);
            test.AssertThrows<InvalidDataException>(() =>
                TCTChannelEvidence.Read(Encoding.UTF8.GetBytes(forged.ToString())),
                "未知Channel证据Schema拒绝");
            forged = XDocument.Parse(Encoding.UTF8.GetString(proof));
            forged.Root.Elements("Frame").First().SetAttributeValue("sequence", 0);
            test.AssertThrows<InvalidDataException>(() =>
                TCTChannelEvidence.Read(Encoding.UTF8.GetBytes(forged.ToString())),
                "非法Sequence拒绝");
            test.AssertThrows<System.Xml.XmlException>(() =>
                TCTChannelEvidence.Read(Encoding.UTF8.GetBytes(
                    "<!DOCTYPE x [<!ENTITY z SYSTEM 'file:///secret'>]>" +
                    Encoding.UTF8.GetString(proof))), "DTD攻击拒绝");
        }
    }
}