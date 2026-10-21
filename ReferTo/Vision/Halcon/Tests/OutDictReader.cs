using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    public static class OutDictReader
    {
        public static bool TryGetTuple(HTuple dict, string key, out HTuple t)
        {
            t = null;
            try { HOperatorSet.GetDictTuple(dict, key, out t); return t != null; }
            catch { return false; }
        }

        public static bool TryGetObject(HTuple dict, string key, out HObject obj)
        {
            obj = null;
            try { HOperatorSet.GetDictObject(out obj, dict, key); return obj != null; }
            catch { return false; }
        }
    }
}
