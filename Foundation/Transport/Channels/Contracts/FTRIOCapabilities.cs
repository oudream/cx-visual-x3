using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
[Flags]
public enum FTRIOCapabilities
    {
        None = 0, ByteStream = 1, Datagram = 2, Message = 4,
        RequestResponse = 8, SharedBuffer = 16, Listener = 32
    }
}
