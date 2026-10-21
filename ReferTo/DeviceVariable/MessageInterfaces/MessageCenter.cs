using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageInterfaces
{
    public static class MessageCenter
    {
        private static readonly object lockUpdateList = new object();
        private static List<Message> updateList = new List<Message>();
        private static ManualResetEvent updateSignal = new ManualResetEvent(false);
        public static event Action<string, Message> OnMessageChanged;

        private static bool isRunning;

        public static void Run()
        {
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
                        updateSignal.Reset();
                        // 
                        ProcessUpdates();
                    }
                }
                catch (Exception ex)
                {
                    // 记录异常
                    LogException(ex);
                }

            })
            { IsBackground = true }.Start();
        }

        public static void Stop() { isRunning = false; }

        private static void LogException(Exception ex)
        {
            // 这里只是一个示例，具体实现取决于您的日志系统
            Console.WriteLine($"Exception in VariableManager thread: {ex.Message}");
            // 如果有更复杂的日志系统，可以这样：
            // Logger.Error(ex, "Exception in VariableManager thread");
        }

        public static void PushMessage(Message message)
        {
            lock (lockUpdateList)
            {
                updateList.Add(message);
            }
            updateSignal.Set(); // 触发信号通知更新
        }

        private static void ProcessUpdates()
        {
            List<Message> messageList;
            lock (lockUpdateList)
            {
                messageList = new List<Message>(updateList);
                updateList.Clear();
            }
            for (int i = 0; i < messageList.Count; i++)
            {
                var message = messageList[i];
                OnMessageChanged?.Invoke(message.Topic, message);
            }
        }

        public static Message CreateMessage(string topic, string body, long source, long target)
        {
            return new Message
            {
                Topic = topic,
                Body = body,
                Timestamp = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000,
                Source = source,
                Target = target,
                ReservedString1 = string.Empty,
                ReservedString2 = string.Empty,
            };
        }

    }
}
