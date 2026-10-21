using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VariableInterfaces
{

    public class VariableManager
    {
        // 变量表
        private static Dictionary<string, VariableTable> variableTables = new Dictionary<string, VariableTable>();
        private static readonly object lockVariableTables = new object();

        private static int updateId;
        private static readonly object lockUpdateList = new object();
        private static List<VariableEntry> updateList = new List<VariableEntry>();
        private static AutoResetEvent updateSignal = new AutoResetEvent(false);
        public static event Action<int, List<VariableEntry>> OnVariableChanged;

        private static volatile bool isRunning = false;

        public static int TotalNotificationsReceived = 0;
        public static int TotalNotificationsSent = 0;
        public static int TotalNotificationsSent2 = 0;

        public static void Run()
        {
            //_updateStack = new ConcurrentStack<VariableEntry>();
            isRunning = true;
            new Thread(() =>
            {
                try
                {
                    while (isRunning)
                    {
                        // 等待一小段时间或直到收到更新信号
                        //_updateSignal.WaitOne(); // 等待更新信号
                        updateSignal.WaitOne(TimeSpan.FromMilliseconds(100));

                        ProcessUpdates();
                    }
                }
                catch (Exception ex)
                {
                    // 记录异常
                    LogHelper.Error($"Exception in VariableManager thread, Message: {ex.Message}, StackTrace: {ex.StackTrace}");
                    // 再次抛出
                    throw;
                }

            })
            { IsBackground = true }.Start();
        }

        public static void Stop() 
        { 
            isRunning = false;
            // 通知线程
            updateSignal.Set();
        }

        public static void PushUpdate(string deviceName, string variableName, VariableChangeType changeType)
        {
            Interlocked.Increment(ref updateId);
            var entry = new VariableEntry { UpdateId = updateId, DeviceName = deviceName, VariableName = variableName, ChangeType = changeType };
            lock (lockUpdateList)
            {
                updateList.Add(entry);
                TotalNotificationsReceived++;
            }
            updateSignal.Set(); // 触发信号通知更新
        }

        private static void ProcessUpdates()
        {
            List<VariableEntry> changedEntries;
            lock (lockUpdateList)
            {
                TotalNotificationsSent += updateList.Count;
                changedEntries = new List<VariableEntry>(updateList);
                updateList.Clear();
            }
            if (changedEntries.Count > 0)
            {
                // 一次性通知所有变化
                OnVariableChanged?.Invoke(changedEntries.Count, changedEntries);
                TotalNotificationsSent2 += changedEntries.Count;
            }
        }

        // 访问变量表时首先锁定整个表，然后进行查询。这在并发量大时可能成为瓶颈。
        // 可以考虑使用并发集合如ConcurrentDictionary
        public static VariableTable FindVariableTable(string deviceName)
        {
            lock (lockVariableTables)
            {
                if (variableTables.TryGetValue(deviceName, out var table))
                {
                    return table;
                }
            }
            return null;
        }
        public static VariableTable GetVariableTable(string deviceName)
        {
            lock (lockVariableTables)
            {
                if (variableTables.TryGetValue(deviceName, out var table))
                {
                    return table;
                }
            }
            return null;
        }

        public static void AddVariableTable(string deviceName, VariableTable table)
        {
            lock (lockVariableTables)
            {
                variableTables.Add(deviceName, table);
            }
        }


        public static bool HasVariableTable(string deviceName)
        {
            lock (lockVariableTables)
            {
                return variableTables.ContainsKey(deviceName);
            }
        }

        public static bool RemoveVariableTable(string deviceName)
        {
            lock (lockVariableTables)
            {
                return variableTables.Remove(deviceName);
            }
        }

        public static void ClearAllVariableTables()
        {
            lock (lockVariableTables)
            {
                variableTables.Clear();
            }
        }

        public static TValue GetVariableValue<TValue>(string deviceName, string variableName)
        {
            VariableTable table;
            lock (lockVariableTables)
            {
                variableTables.TryGetValue(deviceName, out table);
            }
            if (table != null)
            {
                return table.GetVariable<TValue>(variableName);
            }
            else
            {
                // Handle type mismatch
                throw new InvalidOperationException($"设备[{deviceName}]找不到.");
            }
        }

        public static bool TryGetVariableValue<TValue>(string deviceName, string variableName, out TValue value)
        {
            VariableTable table;
            lock (lockVariableTables)
            {
                variableTables.TryGetValue(deviceName, out table);
            }
            if (table != null)
            {
                return table.TryGetVariable<TValue>(variableName, out value);
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public static (TValue, long) GetVariableValue2<TValue>(string deviceName, string variableName)
        {
            VariableTable table;
            lock (lockVariableTables)
            {
                variableTables.TryGetValue(deviceName, out table);
            }
            if (table != null)
            {
                return table.GetVariable2<TValue>(variableName);
            }
            else
            {
                // Handle type mismatch
                throw new InvalidOperationException($"设备[{deviceName}]找不到.");
            }
        }

        public static bool TryGetVariableValue2<TValue>(string deviceName, string variableName, out TValue value, out long lastUpdated)
        {
            VariableTable table;
            lock (lockVariableTables)
            {
                variableTables.TryGetValue(deviceName, out table);
            }
            if (table != null)
            {
                return table.TryGetVariable2<TValue>(variableName, out value, out lastUpdated);
            }
            else
            {
                value = default(TValue);
                lastUpdated = 0;
                return false;
            }
        }

        public static (TValue, long, bool) GetVariableValue3<TValue>(string deviceName, string variableName)
        {
            VariableTable table;
            lock (lockVariableTables)
            {
                variableTables.TryGetValue(deviceName, out table);
            }
            if (table != null)
            {
                return table.GetVariable3<TValue>(variableName);
            }
            else
            {
                // Handle type mismatch
                throw new InvalidOperationException($"设备[{deviceName}]找不到.");
            }
        }

        public static bool TryGetVariableValue3<TValue>(string deviceName, string variableName, out TValue value, out long lastUpdated, out bool hasInitialValue)
        {
            VariableTable table;
            lock (lockVariableTables)
            {
                variableTables.TryGetValue(deviceName, out table);
            }
            if (table != null)
            {
                return table.TryGetVariable3<TValue>(variableName, out value, out lastUpdated, out hasInitialValue);
            }
            else
            {
                value = default(TValue);
                lastUpdated = 0;
                hasInitialValue = false;
                return false;
            }
        }

    }

    public struct VariableEntry
    {
        public int UpdateId { get; set; }
        public string DeviceName { get; set; }
        public string VariableName { get; set; }
        public VariableChangeType ChangeType { get; set; }
    }

    public enum VariableChangeType
    {
        ScanUpdate, // 扫描更新
        ValueChange, // 值变化
        VariableCreation // 创建变量
    }

}
