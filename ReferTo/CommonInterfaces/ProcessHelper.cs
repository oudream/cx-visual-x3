using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class ProcessHelper
    {
        public static string GetProcessFullPath(string procName)
        {
            try
            {
                // 通过名称获取进程列表
                Process[] processes = Process.GetProcessesByName(procName);

                if (processes.Length > 0)
                {
                    // 这里仅取第一个同名进程
                    Process proc = processes[0];

                    // 获取进程可执行文件完整路径
                    string fullPath = proc.MainModule?.FileName;

                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        // 获取所在目录
                        //string directory = Path.GetDirectoryName(fullPath);

                        //Console.WriteLine($"{procName} 全路径: {fullPath}");
                        //Console.WriteLine($"{procName} 所在目录: {directory}");

                        return fullPath;
                    }
                    else
                    {
                        //Console.WriteLine($"无法获取进程 {procName} 的可执行文件全路径。");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"获取进程 {procName} 信息时出错: {ex.Message}");
            }
            return null;
        }

        // 需要的权限比较高，一般是取不到的
        // ============================================================
        // 公开 API —— 拿进程真实工作目录，取不到则返回 "N/A"
        // ============================================================
        public static string GetWorkingDirectory(Process p)
        {
            // 1️⃣ 先用 Win32_ProcessStartup.WorkingDirectory
            // 如果你的进程里真的手动 chdir，那 Windows 并不会把新目录同步回 Win32_ProcessStartup。此时只能通过注入或远程读 PEB 来取（高权限 + 复杂 P/Invoke）
            var wd = GetWorkingDirectoryFromWmiStartup(p.Id);
            if (!string.IsNullOrWhiteSpace(wd))
                return wd;

            // 2️⃣ Fallback：exe 所在目录（99% 情况下等同启动目录）
            try { return Path.GetDirectoryName(p.MainModule?.FileName) ?? "N/A"; }
            catch { return "N/A"; }
        }

        // ============================================================
        // 内部：通过关联查询拿 Win32_ProcessStartup.WorkingDirectory
        // ============================================================
        private static string GetWorkingDirectoryFromWmiStartup(int pid)
        {
            try
            {
                // ASSOCIATORS 查询：把进程实例映射到对应的 Win32_ProcessStartup
                string query = $"ASSOCIATORS OF {{Win32_Process.Handle='{pid}'}} " +
                               "WHERE ResultClass = Win32_ProcessStartup";

                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        // Win32_ProcessStartup.WorkingDirectory 可能为空
                        var wd = obj["WorkingDirectory"] as string;
                        if (!string.IsNullOrWhiteSpace(wd))
                            return wd;
                    }
                }
            }
            catch (ManagementException) { /* WMI 访问失败，跳过 */ }
            catch (UnauthorizedAccessException) { /* 权限不足，跳过 */ }
            catch { /* 其它异常，跳过 */ }

            return null;    // 让外层去做 fallback
        }


        // 杀死进程
        public static string KillProcess(string processName)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes)
                {
                    process.Kill();
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"关闭进程 {processName} 时出错: {ex.Message}";
            }
        }

        // 杀死进程
        public static string KillProcess(Process process)
        {
            try
            {
                process.Kill();
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"关闭进程 {process?.ProcessName} 时出错: {ex.Message}";
            }
        }

        // 此函数是阻塞，比较耗时，大概3秒
        [Obfuscation(Exclude = true)]
        public static string GetSystemAndProcessInfo(string[] processNames, out StringBuilder sbSystemInfo)
        {
            sbSystemInfo = new StringBuilder();
            try
            {
                //var processNames = new[] { "infer_demo1", "infer_demo2" };

                var commandList = new[]
                {
                    new { Title = "systeminfo", Filename = "cmd.exe", Args = "/c systeminfo", Encoding = Encoding.GetEncoding("GB2312") },
                    new { Title = "nvidia-smi", Filename = "cmd.exe", Args = "/c nvidia-smi", Encoding = Encoding.GetEncoding("GB2312") }
                };

                List<Task<string>> tasks = new List<Task<string>>();

                // 当前进程信息
                tasks.Add(Task.Run(() =>
                {
                    var sb = new StringBuilder();
                    AppendProcessInfo(sb, "当前进程", Process.GetCurrentProcess());
                    //sb.AppendLine($"[当前进程] 进程名: {current.ProcessName}.exe PID: {current.Id}" +
                    //    $", 内存: {current.WorkingSet64 / 1024 / 1024} MB 句柄数: {current.HandleCount}");
                    return sb.ToString();
                }));

                // 多个推理进程
                foreach (var procName in processNames)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var sb = new StringBuilder();
                        var procs = Process.GetProcessesByName(procName);
                        if (procs.Length == 0)
                        {
                            sb.AppendLine($"[{procName}.exe] 未找到进程");
                        }
                        else
                        {
                            foreach (var p in procs)
                            {
                                AppendProcessInfo(sb, procName, p);
                                //sb.AppendLine($"[当前进程] 进程名: {procName}.exe PID: {p.Id}" +
                                //    $", 内存: {p.WorkingSet64 / 1024 / 1024} MB, 句柄数: {p.HandleCount}");
                            }
                        }
                        return sb.ToString();
                    }));
                }

                // 磁盘空间
                tasks.Add(Task.Run(() =>
                {
                    var sb = new StringBuilder();
                    sb.Append("[磁盘空间]  ");
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (!drive.IsReady) continue;            // 跳过未就绪的光驱等
                        long total = drive.TotalSize;
                        long free = drive.AvailableFreeSpace;
                        long used = total - free;

                        sb.Append(
                            $"   {drive.Name,-6} Total : {BufferHelper.FormatBytes(total),8}  " +
                            $"Used : {BufferHelper.FormatBytes(used),8}  " +
                            $"Free : {BufferHelper.FormatBytes(free),8}  ");
                    }
                    sb.AppendLine();
                    return sb.ToString();
                }));

                // 命令执行（统一处理）
                foreach (var cmd in commandList)
                {
                    tasks.Add(Task.Run(() =>
                        $"[{cmd.Title}]\n" + RunCommand(cmd.Filename, cmd.Args, cmd.Encoding)));
                }

                var stopwatch = Stopwatch.StartNew();
                Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));
                stopwatch.Stop();

                foreach (var t in tasks)
                {
                    sbSystemInfo.AppendLine(t.Result);
                }

                sbSystemInfo.AppendLine($"[AI错误详情] 完成，耗时：{stopwatch.ElapsedMilliseconds} ms");

                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void AppendProcessInfo(StringBuilder sb, string tag, Process p)
        {
            try
            {
                // ① 可执行完整路径
                string exePath;
                try { exePath = p.MainModule?.FileName ?? "N/A"; }
                catch { exePath = "N/A"; }

                // ② 真实运行时工作目录
                //string workDir = GetWorkingDirectory(p);

                sb.AppendLine(
                    $"[{tag}] 进程名: {p.ProcessName}.exe PID: {p.Id}" +
                    $", 内存: {p.WorkingSet64 / 1024 / 1024} MB" +
                    $", 私有: {p.PrivateMemorySize64 / 1024 / 1024} MB" +
                    $", 句柄数: {p.HandleCount}" +
                    $", 线程数: {p.Threads.Count}" +
                    $", CPU 时长: {p.TotalProcessorTime:hh\\:mm\\:ss}" +
                    $", 启动: {p.StartTime:yyyy-MM-dd HH:mm:ss}" +
                    $", Path: {exePath}");
                    //$", WorkDir: {workDir}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[{tag}] 读取进程信息出错: {ex.Message}");
            }
        }

        // 帮助函数：执行命令行并捕获输出
        private static string RunCommand(string filename, string args, Encoding encoding = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = encoding ?? Encoding.GetEncoding("GBK"), // 使用 GBK 编码
                };

                using (var p = Process.Start(psi))
                {
                    var output = p.StandardOutput.ReadToEnd();
                    var err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return output + (string.IsNullOrWhiteSpace(err) ? "" : "\n[stderr]\n" + err);
                }
            }
            catch (Exception ex)
            {
                return $"命令执行失败: {ex.Message}";
            }
        }
    }
}
