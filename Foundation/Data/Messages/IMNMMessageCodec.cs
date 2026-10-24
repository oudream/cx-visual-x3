using System;
namespace CxVisual.Foundation.Data.Messages
{
    public interface IMNMMessageCodec
    {
        string ContentType { get; }
        byte[] Encode(object payload, Type declaredType);
        object Decode(byte[] bytes, Type declaredType);
    }

}
