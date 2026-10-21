using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    public static partial class HalconHelper
    {
        public delegate bool SplitFileNameCallback(
            string fileName,
            out uint lineNumber,
            out uint batchNumber,
            out int position,
            out uint trayPos,
            out DateTime time,
            out string sn);

        public static SplitFileNameCallback SplitFileNameHandel;

        public static void Init(SplitFileNameCallback nameCallback)
        {
            SplitFileNameHandel = nameCallback;
        }

    }
}
