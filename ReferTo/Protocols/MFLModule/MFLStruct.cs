using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CxVisual.Platform.Protocols.Mfl
{
    // MFL: Message File Link Protocol 消息文件链路协议
    public enum MFLClientStatusEnum
    {
        Idle,   // 空闲状态
        Busy,   // 忙碌状态
        Suspended, // 挂起状态
        TimeOut // 超时状态
    }

    /// <summary>
    /// 一个长度为64字节的string类型内容
    /// </summary>
    public class MFLString64Data
    {
        public string stringData { get; set; }

        // 静态方法：从字节数组解析数据并创建 ClientRequestSuspendData 对象
        public static MFLString64Data Parse(byte[] data)
        {
            var requestData = new MFLString64Data();
            requestData.stringData = Encoding.UTF8.GetString(data).TrimEnd('\0');
            return requestData;
        }

        // 序列化方法：将 ClientRequestSuspendData 对象转成字节数组
        public byte[] ToByteArray()
        {
            var reasonBytes = Encoding.UTF8.GetBytes(stringData);
            Array.Resize(ref reasonBytes, 64); // 固定长度64字节
            return reasonBytes;
        }
    }

}
