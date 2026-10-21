using CommonInterfaces;
using MessageInterfaces;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VariableInterfaces;
using static CxDeviceSuite.Core.Procedure;

namespace CxDeviceSuite.Core
{
    // 一个协程（Coroutine）内创建一个线程来运行，使用ManualResetEvent等待事件通知（变量变化、任务命令），
    // 协程内包含多个工序（Procedure）：工序列表顺序执行，一个工序（状态机）执行完成，就运行第二个工序，直到所有工序完成。
    // 协程内要有迭代控制（次数或永久）。
    // 协程应该也有判定器，用于判定（erminate）工序是否被执行（每次接收到事件通知时判断）
    public class Coroutine
    {
        public long Id { get; private set; }
        public string Name { get; private set; }
        private List<Procedure> procedures = new List<Procedure>();
        private AutoResetEvent signalMessageEvent = new AutoResetEvent(false);
        private bool isRunning = false;

        private readonly object lockUpdateMessageList = new object();
        private List<Message> updateMessageList = new List<Message>();

        public int CurrentProcedureIndex { get; private set; }

        // -1 表示无限迭代
        public Coroutine(CoroutineConfig config)
        {
            Id = config.Id;
            Name = config.Name;
        
            MessageCenter.OnMessageChanged += MessageCenter_OnMessageChanged;
        }
        
        ~Coroutine()
        {
            MessageCenter.OnMessageChanged -= MessageCenter_OnMessageChanged;
        }

        // 协程运行
        // 1、检查变量变化（有变量变化先判定，再执行）
        // 2、检查消息变化（命令：暂停、继续、重来）
        // 3、检查超时
        public void Run()
        {
            LogHelper.Debug($"协程[{Name}]运行中...");

            isRunning = true;
            new Thread(() =>
            {
                try
                {
                    while (isRunning)
                    {
                        // 1、检查消息变化（其它协程的错误、超时）
                        if (signalMessageEvent.WaitOne(100))
                        {
                            List<Message> changedEntries = null;
                            lock (lockUpdateMessageList)
                            {
                                // 检查变量变化
                                if (updateMessageList.Count > 0)
                                {
                                    changedEntries = new List<Message>(updateMessageList);
                                    updateMessageList.Clear();
                                }
                            }
                            if (changedEntries != null)
                            {
                                foreach (var message in changedEntries)
                                {
                                    HandleMessage(message);
                                }
                            }
                        }
                        else
                        {
                            // 2、检查自己工序的错误、超时、完成（完成后继续执行下一个工序），如果工序完成则执行下一个工序
                            if (CurrentProcedureIndex >= 0 && CurrentProcedureIndex < procedures.Count)
                            {
                                var currentProcedure = procedures[CurrentProcedureIndex];
                                var procedureStatus = currentProcedure.GetProcedureStatus();
                                if (procedureStatus == Procedure.ProcedureStatus.Error)
                                {
                                    HandleProcedureError(currentProcedure);
                                }
                                else if (procedureStatus == Procedure.ProcedureStatus.Timeout)
                                {
                                    HandleStatusJudgementTimeout(currentProcedure);
                                }
                                else if (procedureStatus == Procedure.ProcedureStatus.Completed)
                                {
                                    CurrentProcedureIndex++;
                                    if (CurrentProcedureIndex < procedures.Count)
                                    {
                                        currentProcedure = procedures[CurrentProcedureIndex];
                                        currentProcedure.Start();
                                    }
                                    else
                                    {
                                        HandleCoroutineComplete();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录异常
                    LogException(ex);
                }

                LogHelper.Debug($"协程[{Name}]停止。");
            })
            { IsBackground = true }.Start();
        }

        public void Stop()
        {
            // 停止线程
            isRunning = false;
            // 通知线程停止
            signalMessageEvent.Set();
        }

        private void LogException(Exception ex)
        {
            // 这里只是一个示例，具体实现取决于您的日志系统
            LogHelper.Debug($"Exception in VariableManager thread: {ex.Message}");
            // 如果有更复杂的日志系统，可以这样：
            // Logger.Error(ex, "Exception in VariableManager thread");
        }

        private void MessageCenter_OnMessageChanged(string topic, Message message)
        {
            lock (lockUpdateMessageList)
            {
                updateMessageList.Add(message);
            }
            signalMessageEvent.Set();
        }

        private void HandleMessage(Message message)
        {
            // 忽略自己
            if (message.Source == Id) return;
            // 接收广播 或 自己的
            if (message.Target == MessageSystemIds.BroadcastId || message.Target == Id)
            {
                if (message.Topic == MessageTopics.StatusJudgementTimeout)
                {
                    StopCurrentProcedure();
                }
                else if (message.Topic == MessageTopics.ProcedureError)
                {
                    StopCurrentProcedure();
                }
            }
        }

        // 执行从第一个工序开始
        public void StartFromFirst()
        {
            // 设置工序的首个执行项
            for (int i = 0; i < procedures.Count; i++)
            {
                var procedure = procedures[i];
                if (!procedure.IsRunning())
                {
                    procedure.StateMachine.SetCurrentExecutorItemToFirst();
                }
            }
            // 开始
            if (procedures.Count > 0)
            {
                CurrentProcedureIndex = 0;
                var currentProcedure = procedures[CurrentProcedureIndex];
                currentProcedure.Start();
            }
        }

        // 执行原工序开始
        public void StartByContinue()
        {
            if (CurrentProcedureIndex >= 0 && CurrentProcedureIndex < procedures.Count)
            {
                var currentProcedure = procedures[CurrentProcedureIndex];
                currentProcedure.Start();
            }
        }

        // 停止、暂停
        public void StopCurrentProcedure()
        {
            if (CurrentProcedureIndex >= 0 && CurrentProcedureIndex < procedures.Count)
            {
                var currentProcedure = procedures[CurrentProcedureIndex];
                currentProcedure.Stop();
            }
           
        }
      
        public bool IsComplete()
        {
            return CurrentProcedureIndex >= procedures.Count;
        }

        private void HandleCoroutineComplete()
        {
            // post message: CoroutineComplete
            var message = MessageCenter.CreateMessage(MessageTopics.CoroutineComplete,
                string.Empty, Id, MessageSystemIds.MainId);
            MessageCenter.PushMessage(message);
            // log
            LogHelper.Debug($"协程[{Name}]完成");
        }

        private void HandleProcedureError(Procedure procedure)
        {
            // post message: ProcedureError 
            var message = MessageCenter.CreateMessage(MessageTopics.ProcedureError,
                string.Empty, Id, MessageSystemIds.BroadcastId);
            MessageCenter.PushMessage(message);
            // log
            var stateInfo = procedure.StateMachine.GetCurrentStateInfo();
            LogHelper.Debug($"协程[{Name}] 工序[{procedure.Name}] 状态[{stateInfo?.Name}]({stateInfo?.Title}) 产生错误");
        }

        private void HandleStatusJudgementTimeout(Procedure procedure)
        {
            // post message: StatusJudgementTimeout 
            var message = MessageCenter.CreateMessage(MessageTopics.StatusJudgementTimeout,
                string.Empty, Id, MessageSystemIds.BroadcastId);
            MessageCenter.PushMessage(message);
            // log
            var stateInfo = procedure.StateMachine.GetCurrentStateInfo();
            LogHelper.Debug($"协程[{Name}] 工序[{procedure.Name}] 状态[{stateInfo?.Name}]({stateInfo?.Title}) 产生超时");
        }

        public void AddProcedure(Procedure procedure)
        {
            procedures.Add(procedure);
        }

        public int GetProcedureCount()
        {
            return procedures.Count;
        }

        public Procedure GetProcedure(int index)
        {
            if (index >= 0 && index < procedures.Count)
            {
                return procedures[index];
            }
            return null;
        }

    }

    public class CoroutineConfig
    {
        public long Id { get; set; } // Coroutine 的Id

        public string Name { get; set; } // Coroutine 的名称

        public CoroutineConfig(long id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
