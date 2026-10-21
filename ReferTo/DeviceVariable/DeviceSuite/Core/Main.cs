using CommonInterfaces;
using CxDeviceSuite.Loader;
using DeviceInterfaces;
using MessageInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VariableInterfaces;
using static CxDeviceSuite.Core.Procedure;

namespace CxDeviceSuite.Core
{
    public static class Main
    {
        // 工程
        private static Project project = null;
        private static Task<TaskStatus> task = null;
        private static Procedure resetProcedure = null; // 一个工程只有一个复位工序

        // 工程状态
        private static ProjectStatus projectStatus = ProjectStatus.None;
        private static long projectStatusTime = 0;
        private static Message projectAsyncMessage;

        // 工程状态变化
        public delegate void ProjectStatusChangedEventHandler(object sender, ProjectStatusChangedEventArgs e);
        public static event ProjectStatusChangedEventHandler ProjectStatusChanged;

        private static bool isRunning;
        private static AutoResetEvent signalMessageEvent = new AutoResetEvent(false);
        private static readonly object lockUpdateMessageList = new object();
        private static List<Message> updateMessageList = new List<Message>();

        // 注册复位工序，一个工程只有一个复位工序
        public static void RegisterResetProcedure(Procedure procedure)
        {
            resetProcedure = procedure;
        }

        private static bool isInit = false;
        // 初始化，只需要调用一次
        public static void Init()
        {
            if (isInit) return;

            // 日志启动
            LogHelper.Run("DeviceSuite");

            // 消息中心启动
            MessageCenter.Run();
            LogHelper.Debug($"消息中心启动完成");

            // 变量管理器启动
            VariableManager.Run();
            LogHelper.Debug($"变量管理器启动完成");

            isInit = true;
        }

        // 从已经加载的工程中加载，这时工程中Coroutine与Procedure已被加载
        public static void LoadDeviceAndDriver(string projectDirectory)
        {
            if (!isInit) Init();
            // 加载不同项目时，先清空所有变量表
            VariableManager.ClearAllVariableTables();
            // 加载不同项目时，先清空所有设备模板
            DeviceTemplateManager.ClearAllTemplates();
            // 加载内部设备模板
            DeviceEquipmentList.RegisterAllDevice();
            // 加载用户自定义设备模板
            DeviceTemplateManager.Load();
            // 加载设备
            DeviceManager.Load();
            // 加载驱动模块
            DriverModuleManager.Load();
        }

        // 从工程目录中加载
        public static void LoadProject(string projectDirectory)
        {
            // 加载：Project Coroutine Procedure
            project = ProjectLoader.LoadProject(projectDirectory);
        }

        public static void LoadProject(Project proj)
        {
            // 加载：Project Coroutine Procedure
            project = proj;
        }

        // 开一个线程来“检查消息变化”，
        // 消息是用户发过来的命令（完整执行Project、继续执行Project、复位、急停、其它工序错误或超时），
        // 根据这些消息来执行相关流程。
        public static void Run()
        {
            LogHelper.Debug($"系统主线程运行中...");
            // 启动全部驱动模块
            // DriverModuleManager.StartReadDynamicProperty();

            MessageCenter.OnMessageChanged += MessageCenter_OnMessageChanged;
           
            //
            isRunning = true;
            new Thread(() =>
            {
                try
                {
                    while (isRunning)
                    {
                        // 等待一小段时间或直到收到更新信号
                        if (signalMessageEvent.WaitOne(TimeSpan.FromMilliseconds(100)))
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
                                    LogHelper.Debug($"系统主线程 -> 处理消息[{message.Topic}]");
                                    HandleMessage(message, project);
                                }
                            }
                        }
                        else
                        {
                            if (projectStatus == ProjectStatus.Reset)
                            {
                                if (resetProcedure != null)
                                {
                                    var procedureStatus = resetProcedure.GetProcedureStatus();
                                    if (procedureStatus == ProcedureStatus.Error || procedureStatus == ProcedureStatus.Timeout)
                                    {
                                        SetProjectStatus(ProjectStatus.Error, projectAsyncMessage);
                                    }
                                    else if (procedureStatus == ProcedureStatus.Completed || procedureStatus == ProcedureStatus.Stopped)
                                    {
                                        SetProjectStatus(ProjectStatus.Completed, projectAsyncMessage);
                                    }
                                    else
                                    {
                                        // 检测超时
                                        if (TimeHelper.GetNow() - resetProcedure.StartTime > resetProcedure.Timeout)
                                        {
                                            LogHelper.Error($"复位工序超时 timeout: {resetProcedure.Timeout}");
                                            SetProjectStatus(ProjectStatus.Error, projectAsyncMessage);
                                        }
                                    }
                                }
                            }
                            else if (projectStatus == ProjectStatus.WaitFirstValue)
                            {
                                if (task != null && task.IsCompleted)
                                {
                                    SetProjectStatus(ProjectStatus.None, projectAsyncMessage);
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

                MessageCenter.OnMessageChanged -= MessageCenter_OnMessageChanged;
                LogHelper.Debug($"系统主线程停止。");
            })
            { IsBackground = true }.Start();

            // 启动全部协程，但工序还没有开始
            var count = project.GetCoroutineCount();
            for (int i = 0; i < count; i++)
            {
                var coroutine = project.GetCoroutine(i);
                coroutine.Run();
            }
        }

        public static void Stop()
        {
            var count = project.GetCoroutineCount();
            for (int i = 0; i < count; i++)
            {
                var coroutine = project.GetCoroutine(i);
                coroutine.Stop();
            }
            isRunning = false;
        }

        private static void LogException(Exception ex)
        {
            LogHelper.Error($"系统主线程异常: {ex.Message}");
        }

        private static void MessageCenter_OnMessageChanged(string topic, Message message)
        {
            lock (lockUpdateMessageList)
            {
                updateMessageList.Add(message);
            }
            signalMessageEvent.Set();
        }

        private static void HandleMessage(Message message, Project project)
        {
            // 急停命令，用户发出
            // 执行条件：任何状态下
            if (message.Topic == MessageTopics.EmergencyStopCommand)
            {
                var count = project.GetCoroutineCount();
                for (int i = 0; i < count; i++)
                {
                    var coroutine = project.GetCoroutine(i);
                    coroutine.StopCurrentProcedure();
                }
                SetProjectStatus(ProjectStatus.Stopped, message);
            }
            // 等首次变量，用户发出
            // 执行条件：不在状态（WaitFirstValue、Reset、Started）下
            else if (message.Topic == MessageTopics.WaitVariableFirstValue)
            {
                if (projectStatus != ProjectStatus.WaitFirstValue && projectStatus != ProjectStatus.Reset && projectStatus != ProjectStatus.Started)
                {
                    var timeout = message.ReservedInt1;
                    task = WaitAllVariableUpdatedValue(TimeSpan.FromMilliseconds(timeout));
                    SetProjectAsyncMessage(ProjectStatus.WaitFirstValue, message);
                }
            }
            // 复位工序，用户发出
            // 执行条件：不在状态（WaitFirstValue、Reset、Started）下
            else if (message.Topic == MessageTopics.ResetProcedure)
            {
                if (projectStatus != ProjectStatus.WaitFirstValue && projectStatus != ProjectStatus.Reset && projectStatus != ProjectStatus.Started)
                {
                    StartResetProcedure(message);
                    SetProjectAsyncMessage(ProjectStatus.Reset, message);
                }
            }
            // 开始，用户发出
            // 执行条件：不在状态（WaitFirstValue、Reset、Started）下
            else if (message.Topic == MessageTopics.StartFromFirst)
            {
                if (projectStatus != ProjectStatus.WaitFirstValue && projectStatus != ProjectStatus.Reset && projectStatus != ProjectStatus.Started)
                {
                    var count = project.GetCoroutineCount();
                    for (int i = 0; i < count; i++)
                    {
                        var coroutine = project.GetCoroutine(i);
                        coroutine.StartFromFirst();
                    }
                    SetProjectAsyncMessage(ProjectStatus.Started, message);
                }
            }
            // 继续开始，用户发出
            // 执行条件：不在状态（WaitFirstValue、Reset、Started）下
            else if (message.Topic == MessageTopics.StartByContinue)
            {
                if (projectStatus != ProjectStatus.WaitFirstValue && projectStatus != ProjectStatus.Reset && projectStatus != ProjectStatus.Started)
                {
                    var count = project.GetCoroutineCount();
                    for (int i = 0; i < count; i++)
                    {
                        var coroutine = project.GetCoroutine(i);
                        coroutine.StartByContinue();
                    }
                    SetProjectAsyncMessage(ProjectStatus.Started, message);
                }
            }

            // 协程完成，协程发出
            if (message.Topic == MessageTopics.CoroutineComplete)
            {
                // 判断是不是全部完成
                var isComplete = true;
                for (int i = 0; i < project.GetCoroutineCount(); i++)
                {
                    var coroutine = project.GetCoroutine(i);
                    if (!coroutine.IsComplete())
                    {
                        isComplete = false;
                        break;
                    }
                }
                if (isComplete)
                {
                    SetProjectStatus(ProjectStatus.Completed, message);
                }
            }
            // 状态判定超时，协程发出
            else if (message.Topic == MessageTopics.StatusJudgementTimeout)
            {
                SetProjectStatus(ProjectStatus.Error, message);
            }
            // 工序错误，协程发出
            else if (message.Topic == MessageTopics.ProcedureError)
            {
                SetProjectStatus(ProjectStatus.Error, message);
            }
        }

        // 执行从第一个工序开始
        public static void StartResetProcedure(Message message)
        {
            if (resetProcedure == null)
            {
                LogHelper.Error("复位工序执行错误，复位工序为空。");
                return;
            }
            var timeout = message.ReservedInt1;
            if (timeout > 0)
            {
                resetProcedure.Timeout = (int)timeout;
            }
            resetProcedure.Start();
        }

        private static void SetProjectStatus(ProjectStatus status, Message message)
        {
            var oldStatus = projectStatus;
            var newStatus = status;
            projectStatus = status;
            projectStatusTime = TimeHelper.GetNow();
            // 发送状态变化消息
            if (oldStatus != newStatus)
            {
                LogHelper.Debug($"系统主线程 -> 项目状态改变[ {oldStatus} 变为 {newStatus}]");
                if (oldStatus == ProjectStatus.Reset && newStatus == ProjectStatus.None)
                {
                    // 重置完成
                    LogHelper.Information($"系统主线程 -> 重置完成: {message}");
                }
                if (oldStatus == ProjectStatus.WaitFirstValue && newStatus == ProjectStatus.None)
                {
                    // 等待首次变量更新完成
                    LogHelper.Information($"系统主线程 -> 等待首次变量更新完成: {message}");
                }
                ProjectStatusChangedEventArgs args = new ProjectStatusChangedEventArgs(oldStatus, newStatus, message);
                ProjectStatusChanged?.Invoke(null, args);
            }
        }

        private static void SetProjectAsyncMessage(ProjectStatus status, Message message)
        {
            projectAsyncMessage = message;
            SetProjectStatus(status, message);
        }

        // 等待首次所有变量更新
        public static async Task<TaskStatus> WaitAllVariableUpdatedValue(TimeSpan timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            string notUpdatedVariableDetails = string.Empty;
            try
            {
                await Task.Run(() =>
                {
                    while (true)
                    {
                        var (allUpdated, variableName, deviceName, moduleName) = DriverModuleManager.AllVariablesHaveInitialAndUpdatedValues();
                        if (allUpdated)
                        {
                            break; // 所有变量都已更新
                        }
                        if (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            // 未更新变量的详细信息，需要根据实际情况填充模块和设备名称
                            notUpdatedVariableDetails = $"模块: {moduleName}, 设备: {deviceName}, 属性: {variableName}";
                            // 超时抛出异常，并带有未更新变量的详细信息
                            throw new OperationCanceledException(notUpdatedVariableDetails);
                        }
                        Thread.Sleep(100); // 简短等待减少负载
                    }
                }, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                LogHelper.Error($"等待变量更新超时. 首个未更新变量: {ex.Message}");
                return TaskStatus.Error; // 返回失败状态和未更新变量的详细信息
            }

            return TaskStatus.Completed; // 如果所有变量都已更新，返回成功状态
        }

        // 获取当前项目的状态
        public static ProjectStatus GetProjectStatus()
        {
            return projectStatus;
        }

        // 获取当前项目的状态时间
        public static long GetProjectStatusTime()
        {
            return projectStatusTime;
        }

    }

    /// <summary>
    /// 代表项目的各种状态。
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// 表示没有任何状态，即项目尚未开始或状态未定义。
        /// </summary>
        None = 0,

        /// <summary>
        /// 表示等待首次变量更新
        /// </summary>
        WaitFirstValue = 1,

        /// <summary>
        /// 表示项目已复位到初始状态，准备重新开始。
        /// </summary>
        Reset = 2,

        /// <summary>
        /// 表示项目已开始执行。
        /// </summary>
        Started = 3,

        /// <summary>
        /// 表示项目已成功完成。
        /// </summary>
        Completed = 4,

        /// <summary>
        /// 表示项目遇到错误。
        /// </summary>
        Error = 5,

        /// <summary>
        /// 表示项目已停止执行。
        /// </summary>
        Stopped = 6
    }

    public class ProjectStatusChangedEventArgs : EventArgs
    {
        public ProjectStatus OldStatus { get; private set; }
        public ProjectStatus NewStatus { get; private set; }
        public Message Message { get; private set; }

        public ProjectStatusChangedEventArgs(ProjectStatus oldStatus, ProjectStatus newStatus, Message message)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Message = message;
        }
    }
}
