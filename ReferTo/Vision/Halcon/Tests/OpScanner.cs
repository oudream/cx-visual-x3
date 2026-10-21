using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    // 反射扫描

    public sealed class HalconOpMeta
    {
        public string Procedure;
        public int TimeoutMs;
        public HalconInvokeKind Invoke;
        public MethodInfo Method;
        public HalconInAttribute[] Inputs;
        public HalconOutAttribute[] Outputs;
    }

    public static class HalconOpScanner
    {
        public static List<HalconOpMeta> Scan(Assembly asm)
        {
            var list = new List<HalconOpMeta>();

            foreach (var t in asm.GetTypes())
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    var op = m.GetCustomAttribute<HalconOpAttribute>();
                    if (op == null) continue;

                    list.Add(new HalconOpMeta
                    {
                        Procedure = op.Procedure,
                        TimeoutMs = op.TimeoutMs,
                        Invoke = op.Invoke,
                        Method = m,
                        Inputs = m.GetCustomAttributes<HalconInAttribute>().ToArray(),
                        Outputs = m.GetCustomAttributes<HalconOutAttribute>().ToArray()
                    });
                }
            }

            return list;
        }
    }
}
