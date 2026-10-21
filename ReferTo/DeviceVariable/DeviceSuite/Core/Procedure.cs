using CommonInterfaces;
using DeviceInterfaces;
using MessageInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VariableInterfaces;
using YamlDotNet.Core.Tokens;
using static CxDeviceSuite.Core.Procedure;

namespace CxDeviceSuite.Core
{
    // 工序（Procedure）
    // 每个工序的执行，使用System.Threading.Thread独立运行，使用AutoResetEvent等变量变化通知。使用状态机的机制。
    // 每个状态对应一个”执行器“。每个状态对应一个”前置判定器“。每个状态对应一个”后置转换器“。
    //
    // 执行逻辑如下：
    // 每个状态执行“前变量判定”，“前变量判定不通过”就置“工序状态”为“前判定状态”进入等待变量变化，等待有变量变化通知才去进行前变量判定，直至通过才能进入“状态执行”，或直至“前判定状态”超时（就置“工序状态”为“错误”，提前退出工序；
    // “状态执行”时，如果有执行前延时，就置“工序状态”为“前延时状态”；“前延时状态”到达时间后才能执行；
    // 执行后，如果有执行后延时，就置“工序状态”为“后延时状态”；“后延时状态”到达时间才能到“状态转换器”；
    // “状态转换器”进行转换判断（有多个条件判断），如果有条件（判断器）通过就转到对应状态，继续执行以上流程。
    // 如果没条件（判断器）通过就置“工序状态”为“后判定状态”进入等待变量变化，等待有变量变化通知才去进行后判定，要么通过（继续执行以上流程），要么超时（等待超时，就退出整个执行，置“工序状态”为错误状态）。
    public class Procedure
    {
        // 工序的状态
        public enum ProcedureStatus
        {
            Init, Executing, DelayingBefore, DelayingAfter, WaitVariableBefore, WaitVariableAfter, Paused, Completed, Stopped, Error, Timeout
        }

        // 工序的Id
        public long Id { get; private set; }

        // 工序的名称
        public string Name { get; private set; } = string.Empty;

        // 工序的状态机
        public StateMachine StateMachine { get; private set; }

        // 工序执行
        private ProcedureStatus procedureStatus = ProcedureStatus.Init;
        private long procedureStatusTime = 0;
        public string lastError;
        private AutoResetEvent variableChangeEvent = new AutoResetEvent(false);
        private AutoResetEvent commandEvent = new AutoResetEvent(false); // 等待命令（停止、暂停）
        private AutoResetEvent pauseEvent = new AutoResetEvent(false); // 等待恢复命令
        private Thread procedureThread = null;
        private bool isRunning = false;

        private readonly object lockUpdateVariableList = new object();
        private List<VariableEntry> updateVariableList = new List<VariableEntry>();

        // 工程状态变化
        public delegate void ProcedureStatusChangedEventHandler(object sender, ProcedureStatusChangedEventArgs e);
        public event ProcedureStatusChangedEventHandler ProcedureStatusChanged;

        public Procedure(ProcedureConfig config, StateMachine stateMachine)
        {
            Id = config.Id;
            Name = config.Name;
            StateMachine = stateMachine;  
            if (stateMachine.IsEndByCurrentExecutor())
            {
                procedureStatus = ProcedureStatus.Completed;
            }
            else
            {
                procedureStatus = ProcedureStatus.Init;
            }
        }

        public void Start()
        {
            if (isRunning) return;
            isRunning = true;
            procedureThread = new Thread(Run);
            procedureThread.Start();
            StartTime = TimeHelper.GetNow();
        }

        private void Manager_OnVariableChanged(int count, List<VariableEntry> entries)
        {
            lock (lockUpdateVariableList)
            {
                updateVariableList.AddRange(entries);
            }
            variableChangeEvent.Set();
        }

        public void Run()
        {
            VariableManager.OnVariableChanged += Manager_OnVariableChanged;

            SetStatus(ProcedureStatus.Executing, string.Empty);

            while (isRunning)
            {
                var currentExecutor = StateMachine.GetCurrentExecutorItem();
                if (currentExecutor == null)
                {
                    isRunning = false;
                    SetStatus(ProcedureStatus.Error, $"工序[{Name}]没有找到当前执行器");
                    break;
                }

                // 执行前的状态判定，前置判定
                var evaluator = StateMachine.GetCurrentEvaluator();
                if (evaluator != null)
                {
                    evaluator.StartEvaluate(TimeHelper.GetNow());
                    if (!evaluator.Evaluate())
                    {
                        if (evaluator.RequiredVariables().Count > 0)
                        {
                            SetStatus(ProcedureStatus.WaitVariableBefore, string.Empty);
                            if (!WaitForVariableChangeBefore())
                            {
                                // 等待变量变化失败（用户Stop），工序执行失败
                                break;
                            }
                        }
                    }
                }

                // 执行前的延时
                if (currentExecutor.DelayBefore > 0)
                {
                    SetStatus(ProcedureStatus.DelayingBefore, string.Empty);
                    if (!ControlledDelay(currentExecutor.DelayBefore))
                    {
                        // 等待变量变化失败（用户Stop），工序执行失败
                        break;
                    }
                }

                // 工序执行
                var result = StateMachine.ExecuteCurrent();
                if (result != StateHandleResult.Success && result != StateHandleResult.Warning)
                {
                    isRunning = false; // 停止循环
                    SetStatus(ProcedureStatus.Error, $"工序[{Name}]执行错误[{result}]");
                    break;
                }

                // 执行后的延时
                if (currentExecutor.DelayAfter > 0)
                {
                    SetStatus(ProcedureStatus.DelayingAfter, string.Empty);
                    if (!ControlledDelay(currentExecutor.DelayAfter))
                    {
                        // 等待变量变化失败（用户Stop），工序执行失败
                        break;
                    }
                }

                // 执行后的状态转换，后置判定
                {
                    var transitioner = StateMachine.GetCurrentStateTransitioner();
                    if (transitioner != null)
                    {
                        // 计算下一个状态
                        transitioner.StartTrans(TimeHelper.GetNow());
                        int nextStateValue = transitioner.TransTo();
                        if (nextStateValue > 0)
                        {
                            var nextExecutor = StateMachine.ExecutorMetrics.FindStateExecutionItem(nextStateValue);
                            if (nextExecutor == null || StateMachine.EnumMetrics.IsEnd(nextExecutor.State))
                            {
                                isRunning = false;
                                SetStatus(ProcedureStatus.Completed, string.Empty); // 标记为完成
                                break;
                            }
                            else
                            {
                                SetStatus(ProcedureStatus.Executing, string.Empty); // 标记新一个状态开始，执行中
                                StateMachine.SetCurrentExecutorItem(nextExecutor);
                            }
                        }
                        else
                        {
                            SetStatus(ProcedureStatus.WaitVariableAfter, string.Empty); // 标记为等待后置变量变化
                            if (!WaitForVariableChangeAfter(transitioner))
                            {
                                // 等待变量变化失败（用户Stop），工序执行失败
                                break;
                            }
                        }
                    }
                    else
                    {
                        isRunning = false; // 停止循环
                        SetStatus(ProcedureStatus.Error, $"工序[{Name}]没有找到状态转换器"); // 标记为错误
                        break;
                    }
                }
            }

            VariableManager.OnVariableChanged -= Manager_OnVariableChanged;
        }

        private void SetStatus(ProcedureStatus status, string err)
        {
            var oldStatus = procedureStatus;
            var newStatus = status;
            procedureStatus = status;
            procedureStatusTime = TimeHelper.GetNow();
            lastError = err;
            // 发送状态变化消息
            if (oldStatus != newStatus)
            {
                LogHelper.Debug($"工序[{Name}]状态改变[ {oldStatus} 变为 {newStatus}]");
                ProcedureStatusChangedEventArgs args = new ProcedureStatusChangedEventArgs(Id, Name, oldStatus, newStatus, err);
                ProcedureStatusChanged?.Invoke(null, args);
            }
        }

        private bool ControlledDelay(int milliseconds)
        {
            // 等待commandEvent被触发或延时完成
            if (commandEvent.WaitOne(milliseconds))
            {
                // 1、命令事件触发
                if (procedureStatus == ProcedureStatus.Paused)
                {
                    pauseEvent.WaitOne(); // Wait for resume
                }
                else if (procedureStatus == ProcedureStatus.Stopped)
                {
                    isRunning = false;
                    SetStatus(ProcedureStatus.Stopped, string.Empty);
                    return false;
                }
            }
            return true;
        }

        private bool WaitForVariableChangeBefore()
        {
            while (isRunning)
            {
                WaitHandle[] handles = { commandEvent, variableChangeEvent };
                int signaledIndex = WaitHandle.WaitAny(handles, 30);
                if (signaledIndex == 0) // Command event was signaled
                {
                    // 1、命令事件触发
                    if (procedureStatus == ProcedureStatus.Paused)
                    {
                        pauseEvent.WaitOne(); // Wait for resume
                    }
                    else if (procedureStatus == ProcedureStatus.Stopped)
                    {
                        isRunning = false;
                        return false;
                    }
                }
                else if (signaledIndex == 1) // Variable change event was signaled
                {
                    List<VariableEntry> changedEntries = null;
                    lock (lockUpdateVariableList)
                    {
                        // 检查变量变化
                        if (updateVariableList.Count > 0)
                        {
                            changedEntries = new List<VariableEntry>(updateVariableList);
                            updateVariableList.Clear();
                        }
                    }
                    // 2、变量变化事件触发
                    if (changedEntries != null && changedEntries.Count > 0)
                    {
                        var evaluator = StateMachine.GetCurrentEvaluator();
                        if (evaluator != null)
                        {
                            var requiredVariables = evaluator.RequiredVariables();
                            if (requiredVariables != null && requiredVariables.Count > 0)
                            {
                                bool exists = false;
                                foreach (var requiredVariable in requiredVariables)
                                {
                                    foreach (var entry in changedEntries)
                                    {
                                        if (requiredVariable.deviceName == entry.DeviceName && requiredVariable.variableName == entry.VariableName)
                                        {
                                            exists = true;
                                            break;
                                        }
                                    }
                                    if (exists) break;
                                }
                                if (exists && evaluator.Evaluate())
                                {
                                    SetStatus(ProcedureStatus.Executing, string.Empty);
                                    return true;
                                }
                            }
                            // 无变量关联，忽略
                        }
                        else
                        {
                            isRunning = false;
                            SetStatus(ProcedureStatus.Error, $"工序[{Name}]没有找到当前执行器。");
                            return false;
                        }
                    }
                }
                else
                {
                    // 3、检测超时
                    var evaluator = StateMachine.GetCurrentEvaluator();
                    if (evaluator != null)
                    {
                        int timeout = evaluator.GetTimeOutMS();
                        if (timeout > 0)
                        {
                            var dtNow = TimeHelper.GetNow();
                            if (dtNow - evaluator.StartEvaluateTime > timeout)
                            {
                                isRunning = false;
                                var stateInfo = StateMachine.GetCurrentStateInfo();
                                SetStatus(ProcedureStatus.Timeout, $"工序[{Name}] 状态[{stateInfo?.Name}]({stateInfo?.Title}) 执行前置判定器超时。");
                                return false;
                            }
                        }
                    }
                }
            }
            SetStatus(ProcedureStatus.Error, $"工序[{Name}] WaitForVariableChangePre 内部错误。");
            return false;
        }

        private bool WaitForVariableChangeAfter(StateTransitioner transitioner)
        {
            while (isRunning)
            {
                if (commandEvent.WaitOne(100)) // Command event was signaled
                {
                    // 1、命令事件触发
                    if (procedureStatus == ProcedureStatus.Paused)
                    {
                        pauseEvent.WaitOne(); // Wait for resume
                    }
                    else if (procedureStatus == ProcedureStatus.Stopped)
                    {
                        isRunning = false;
                        SetStatus(ProcedureStatus.Stopped, string.Empty);
                        return false;
                    }
                }
                else
                {
                    // 2、定时执行转换
                    int nextStateValue = transitioner.TransTo();
                    if (nextStateValue > 0)
                    {
                        var nextExecutor = StateMachine.ExecutorMetrics.FindStateExecutionItem(nextStateValue);
                        if (nextExecutor == null || StateMachine.EnumMetrics.IsEnd(nextExecutor.State))
                        {
                            isRunning = false;
                            SetStatus(ProcedureStatus.Completed, string.Empty);// 标记为完成
                            break;
                        }
                        else
                        {
                            StateMachine.SetCurrentExecutorItem(nextExecutor);
                        }
                    }
                    // 3、检测超时
                    int timeout = transitioner.TimeOut;
                    if (timeout > 0)
                    {
                        var dtNow = TimeHelper.GetNow();
                        if (dtNow - transitioner.StartTransTime > timeout)
                        {
                            isRunning = false;
                            var stateInfo = StateMachine.GetCurrentStateInfo();
                            SetStatus(ProcedureStatus.Timeout, $"工序[{Name}] 状态[{stateInfo?.Name}]({stateInfo?.Title}) 执行后置转换判定器超时。");
                            return false;
                        }
                    }
                }
            }
            SetStatus(ProcedureStatus.Error, $"工序[{Name}] WaitForVariableChangeAfter 内部错误。");
            return false;
        }

        // 暂停
        public void Pause()
        {
            SetStatus(ProcedureStatus.Paused, string.Empty); // 标记为暂停
            commandEvent.Set(); // Signal the command event to handle pause
        }

        // 恢复
        public void Resume()
        {
            if (procedureStatus == ProcedureStatus.Paused)
            {
                SetStatus(ProcedureStatus.Executing, string.Empty); // 标记暂停后恢复执行，为执行中
                pauseEvent.Set(); // Release the pause
            }
        }

        // 停止
        public void Stop()
        {
            isRunning = false;
            SetStatus(ProcedureStatus.Stopped, string.Empty); // 标记为停止
            commandEvent.Set(); // Signal the command event to handle stop
            pauseEvent.Set(); // Ensure the machine can exit from pause state if it was paused
        }

        public bool IsRunning()
        {
            return isRunning;
        }

        public int Timeout { get; set; }

        public long StartTime { get; private set; }

        // 获取工序状态
        public ProcedureStatus GetProcedureStatus()
        {
            return procedureStatus;
        }

        // 获取工序状态信息
        public (ProcedureStatus, long, string) GetProcedureStatusInfo()
        {
            return (procedureStatus, procedureStatusTime, lastError);
        }

    }

    public class ProcedureStatusChangedEventArgs : EventArgs
    {
        public long Id { get; private set; }
        public string Name { get; private set; }
        public ProcedureStatus OldStatus { get; private set; }
        public ProcedureStatus NewStatus { get; private set; }
        public string Error { get; private set; }

        public ProcedureStatusChangedEventArgs(long id, string name, ProcedureStatus oldStatus, ProcedureStatus newStatus, string err)
        {
            Id = id;
            Name = name;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Error = err;
        }
    }


    public class ProcedureConfig
    {
        // Procedure 的Id
        public long Id { get; set; }

        // Procedure 的名称
        public string Name { get; set; }

        public ProcedureConfig(long id, string name)
        {
            Id = id;
            Name = name;
        }
    }

}
