using CommonInterfaces;
using CxVisual.Platform.VariableObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VariableInterfaces;

namespace CxVisual.Platform.Protocols.S7
{
    public class S7PointIncrementTest
    {
        private readonly object lockUpdateVariableList = new object();
        private List<VariableEntry> updateVariableList = new List<VariableEntry>();
        private AutoResetEvent variableChangeEvent = new AutoResetEvent(false);

        public Dictionary<string, VariableMeasure<int>> measures = new Dictionary<string, VariableMeasure<int>>();
        public Dictionary<string, int> measureValues = new Dictionary<string, int>();
        private bool isRunning = false;

        public S7PointIncrementTest(List<string> deviceNames)
        {
            foreach (var deviceName in deviceNames)
            {
                var template = VariableObjectManager.GetObjectTemplate(deviceName);
                if (template != null)
                {
                    if (template.ObjectType == typeof(MeasureInteger))
                    {
                        measures[deviceName] = new VariableMeasure<int>(deviceName);
                        measureValues[deviceName] = -1;
                    }
                }
            }
            VariableManager.OnVariableChanged += Manager_OnVariableChanged;
        }

        ~S7PointIncrementTest()
        {
            VariableManager.OnVariableChanged -= Manager_OnVariableChanged;
        }

        public void Start()
        {
            new Thread(() =>
            {
                var startTime = DateTime.Now;
                while (isRunning && DateTime.Now - startTime < TimeSpan.FromSeconds(60 * 60 * 24 * 7))
                {
                    variableChangeEvent.WaitOne(TimeSpan.FromMilliseconds(100));
                    ProcessUpdates();
                }
                Console.WriteLine($"S7PointIncrementTest exited");
            })
            { IsBackground = true }.Start();
        }

        public void Stop()
        {
            isRunning = false;
        }

        private void ProcessUpdates()
        {
            List<VariableEntry> changedEntries;
            lock (lockUpdateVariableList)
            {
                changedEntries = new List<VariableEntry>(updateVariableList);
                updateVariableList.Clear();
            }
            if (changedEntries.Count > 0)
            {
                foreach (var entry in changedEntries)
                {
                    // 判断变量是否存在measures中
                    if (measures.TryGetValue(entry.DeviceName, out var measure))
                    {
                        var valueInt = measure.GetValue();
                        var Test1 = measureValues[entry.DeviceName];
                        if (valueInt != Test1)
                        {
                            // 检查值是否为加1变化的值
                            if (valueInt - Test1 != 1 && Test1 != -1)
                            {
                                LogHelper.Error($"{entry.DeviceName} 采集不正确，值为: {valueInt}, 应该为：{Test1 + 1}");
                            }
                            measureValues[entry.DeviceName] = valueInt;
                        }
                    }
                }
            }
        }

        private void Manager_OnVariableChanged(int arg1, List<VariableEntry> entries)
        {
            lock (lockUpdateVariableList)
            {
                updateVariableList.AddRange(entries);
            }
            variableChangeEvent.Set();
        }

    }
}
