using CxVisual.Foundation.Transport;
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
            TCTTestAspect.Mechanism, "Channel / Protocol", ObjectId = "FTRVirtualChannel")]
        public static void RealVirtualFrames(TCTTestContext test)
        {
            FTRVirtualChannel[] pair = FTRVirtualChannel.CreatePair("Virtual-A", "Virtual-B");
            MPALengthFrameProtocol protocol = new MPALengthFrameProtocol();
            FTRSubChannelAddress receiverAddress = new FTRSubChannelAddress(pair[1].Id);
            List<byte[]> decoded = new List<byte[]>();
            pair[1].Received += incoming => {
                foreach (byte[] payload in protocol.ProcessReceived(incoming,
                    new MPAProtocolContext("length-frame", "virtual-session")))
                    decoded.Add(payload);
            };
            pair[0].Open(); pair[1].Open();
            byte[] payload1 = new byte[] { 0x01, 0x03, 0xA5, 0xFF };
            byte[] frame = protocol.PrepareSend(payload1, null);
            for (int offset = 0; offset < frame.Length; offset += 2)
            {
                int length = Math.Min(2, frame.Length - offset);
                byte[] chunk = new byte[length];
                Buffer.BlockCopy(frame, offset, chunk, 0, length);
                pair[0].Send(chunk);
            }
            test.AssertTrue(decoded.Count == 1 && decoded[0].SequenceEqual(payload1),
                "Protocol 真正拼接Channel分片并解释CRC16", true, true);
            FTRChannelFrame[] tx = pair[0].ReadFrames()
                .Where(x => x.Direction == FTRFrameDirection.Tx).ToArray();
            FTRChannelFrame[] rx = pair[1].ReadFrames()
                .Where(x => x.Direction == FTRFrameDirection.Rx).ToArray();
            test.AssertTrue(tx.Length == rx.Length && rx.All(x => x.DecodedPayload == null) &&
                tx.SelectMany(x => x.Bytes).SequenceEqual(rx.SelectMany(x => x.Bytes)),
                "Channel只有原始IO，不再混入协议解码", true, true);

            byte[] corrupt = protocol.PrepareSend(new byte[] { 0x0A, 0x0B }, null);
            corrupt[4] ^= 0xff;
            // FTR的Received观察者异常必须隔离，不再穿透物理Send；
            // CRC拒绝由MPA自行证明，不能要求Channel把协议异常当成IO异常。
            pair[0].Send(corrupt);
            MPALengthFrameParser corruptParser = new MPALengthFrameParser();
            corruptParser.PushBytes(corrupt);
            byte[] rejectedPayload;
            test.AssertThrows<InvalidDataException>(
                () => corruptParser.TryReadFrame(out rejectedPayload),
                "CRC错误由正式Protocol拒绝，FTR发送不传播观察者异常");
            test.AssertEqual(FTRChannelState.Open, pair[1].State,
                "规约数据CRC错误不会使物理通道错误断开");
            protocol.Reset(receiverAddress);
            byte[] payload2 = new byte[] { 0x12, 0x34 };
            pair[0].Send(protocol.PrepareSend(payload2, null));
            test.AssertTrue(decoded.Count == 2 && decoded[1].SequenceEqual(payload2),
                "Protocol Reset后恢复正常，不重建Channel", true, true);
            pair[1].Close();
            test.AssertThrows<IOException>(() => pair[0].Send(new byte[] { 0x99 }),
                "断开的Virtual接收方拒绝投递");
            pair[0].Close();
            test.AssertThrows<InvalidOperationException>(() =>
                pair[0].Send(new byte[] { 0x01 }), "发送方关闭后不得继续发包");
            FTRChannelFrame[] all = pair.SelectMany(x => x.ReadFrames()).ToArray();
            byte[] evidence = TCTChannelEvidence.Serialize(all);
            XDocument restored = TCTChannelEvidence.Read(evidence);
            test.AssertEqual(all.Length, restored.Root.Elements("Frame").Count(),
                "Owner原始通道事件证据回放完整");
            test.AssertTrue(restored.Root.Elements("Frame")
                .Any(x => (string)x.Attribute("direction") == "Error"),
                "通道发送拒绝应保留错误证据", true, true);
            test.ObserveStructured("Channel", "Virtual纯传输 + Protocol CRC16/分片",
                "Connectivity Channel记录原始收发；Protocol完成分包、CRC和错误恢复；非真实PLC/HIL",
                pair[0].Id, "Virtual/" + pair[0].Id, pair[0].SessionId.ToString("D"),
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
            FTRVirtualChannel[] pair = FTRVirtualChannel.CreatePair("proof-A", "proof-B");
            pair[0].Open(); pair[1].Open();
            pair[0].Send(codec.Encode(new byte[] { 42 }), "proof");
            byte[] proof = TCTChannelEvidence.Serialize(
                pair.SelectMany(x => x.ReadFrames()).ToArray());
            XDocument forged = XDocument.Parse(Encoding.UTF8.GetString(proof));
            forged.Root.SetAttributeValue("schema", 99);
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