using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    public class VariableTest
    {
        private static int totalWrites = 0;

        private static readonly object lockUpdateVariableList = new object();
        private static List<VariableEntry> updateVariableList = new List<VariableEntry>();
        private static AutoResetEvent variableChangeEvent = new AutoResetEvent(false);

        public static void MainTest()
        {
            var writeThreads = new List<Thread>();

            VariableNotifier notifier1 = new VariableNotifier();
            notifier1.Id = 1;
            notifier1.Start();

            VariableNotifier notifier2 = new VariableNotifier();
            notifier2.Id = 2;
            notifier2.Start();

            VariableNotifier notifier3 = new VariableNotifier();
            notifier3.Id = 3;
            notifier3.Start();

            Thread.Sleep(100);

            // 创建和启动写入线程
            for (int i = 0; i < 5; i++)
            {
                int threadIndex = i; // 创建临时变量
                var writeThread = new Thread(() =>
                {
                    var intMeasures = new List<VariableMeasure<int>>();
                    var doubleMeasures = new List<VariableMeasure<double>>();
                    var stringMeasures = new List<VariableMeasure<string>>();
                    var rnd = new Random();
                    for (int j = 0; j < 1000; j++)
                    {
                        // 假设VariableMeasure<T>可以这样实例化和使用
                        intMeasures.Add(new VariableMeasure<int>($"int_{threadIndex}_{j}"));
                        doubleMeasures.Add(new VariableMeasure<double>($"double_{threadIndex}_{j}"));
                        stringMeasures.Add(new VariableMeasure<string>($"string_{threadIndex}_{j}"));
                    }

                    // 模拟一分钟内不断写入
                    var startTime = DateTime.Now;
                    int iValue = 0;
                    double dValue = 0;
                    string sValue = null;
                    while (DateTime.Now - startTime < TimeSpan.FromSeconds(60 * 50))
                    {
                        foreach (var measure in intMeasures)
                        {
                            measure.SetValue(iValue++);
                            Interlocked.Increment(ref totalWrites);
                        }
                        foreach (var measure in doubleMeasures)
                        {
                            dValue += 0.1;
                            measure.SetValue(dValue);
                            Interlocked.Increment(ref totalWrites);
                        }
                        foreach (var measure in stringMeasures)
                        {
                            sValue = $"{iValue++}";
                            measure.SetValue(sValue);
                            Interlocked.Increment(ref totalWrites);
                        }
                        // sleep 10毫秒
                        Thread.Sleep(10);
                    }
                });
                writeThreads.Add(writeThread);
                writeThread.Start();
            }


            {
                var startTime = DateTime.Now;
                while (DateTime.Now - startTime < TimeSpan.FromSeconds(60 * 50 + 20))
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1)); // 等待一分钟
                    Console.WriteLine($"Total writes: {totalWrites}");
                    Console.WriteLine($"TotalNotificationsR writes: {VariableManager.TotalNotificationsReceived}");
                    Console.WriteLine($"TotalNotificationsS writes: {VariableManager.TotalNotificationsSent}");
                    Console.WriteLine($"TotalNotificationsS2 writes: {VariableManager.TotalNotificationsSent2}");
                    Console.WriteLine($"Total notifier1 received: {notifier1.GetTotalNotificationsReceived()}, deal: {notifier1.GetTotalNotificationsDeal()}, push: {notifier1.TotalNotificationsPush}");
                    Console.WriteLine($"Total notifier2 received: {notifier2.GetTotalNotificationsReceived()}, deal: {notifier1.GetTotalNotificationsDeal()}, push: {notifier1.TotalNotificationsPush}");
                    Console.WriteLine($"Total notifier3 received: {notifier3.GetTotalNotificationsReceived()}, deal: {notifier1.GetTotalNotificationsDeal()}, push: {notifier1.TotalNotificationsPush}");
                }
            }
        }

    }

    public class VariableNotifier
    {
        public int Id = 0;

        private readonly object lockUpdateVariableList = new object();
        private List<VariableEntry> updateVariableList = new List<VariableEntry>();
        private AutoResetEvent variableChangeEvent = new AutoResetEvent(false);

        private int TotalNotificationsDeal = 0;

        private int TotalNotificationsReceived = 0;

        public int TotalNotificationsPush = 0;

        public VariableNotifier()
        {
            VariableManager.OnVariableChanged += Manager_OnVariableChanged;
        }

        ~VariableNotifier()
        {
            VariableManager.OnVariableChanged -= Manager_OnVariableChanged;
        }

        public void Start()
        {
            Thread notifyThread = new Thread(() =>
            {
                var startTime = DateTime.Now;
                while (DateTime.Now - startTime < TimeSpan.FromSeconds(60 * 50 + 10))
                {
                    variableChangeEvent.WaitOne(TimeSpan.FromMilliseconds(100));
                    ProcessUpdates();
                }
                Console.WriteLine($"notifyThread{Id} exit, notifier received: {TotalNotificationsDeal}");
            });
            notifyThread.Start();
        }

        private void ProcessUpdates()
        {
            List<VariableEntry> changedEntries;
            lock (lockUpdateVariableList)
            {
                TotalNotificationsReceived += updateVariableList.Count;
                changedEntries = new List<VariableEntry>(updateVariableList);
                updateVariableList.Clear();
            }
            if (changedEntries.Count > 0)
            {
                foreach (var entry in changedEntries)
                {
                    var table = VariableManager.FindVariableTable(entry.DeviceName);
                    if (table != null)
                    {
                        if (entry.DeviceName.StartsWith("int"))
                        {
                            var value = table.GetVariable<int>(entry.VariableName);
                        }
                        if (entry.DeviceName.StartsWith("double"))
                        {
                            var value = table.GetVariable<double>(entry.VariableName);
                        }
                        if (entry.DeviceName.StartsWith("string"))
                        {
                            var value = table.GetVariable<string>(entry.VariableName);
                        }
                    }
                    Interlocked.Increment(ref TotalNotificationsDeal);
                }
                // 尝试阻塞 sleep 10，看看有没有掉失变化
                Random rnd = new Random();
                // 模拟一段随机的处理时间，范围从0到1000毫秒
                Thread.Sleep(rnd.Next(0, 1001));
            }
        }

        private void Manager_OnVariableChanged(int arg1, List<VariableEntry> entries)
        {
            lock (lockUpdateVariableList)
            {
                TotalNotificationsPush += arg1;
                updateVariableList.AddRange(entries);
            }
            variableChangeEvent.Set();
        }

        public int GetTotalNotificationsDeal()
        {
            return TotalNotificationsDeal;
        }
        public int GetTotalNotificationsReceived()
        {
            return TotalNotificationsReceived;
        }
    }

}
