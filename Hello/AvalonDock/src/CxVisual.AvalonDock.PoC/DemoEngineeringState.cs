using System;
using System.Collections.ObjectModel;

namespace CxVisual.DockingPoC
{
    /// <summary>
    /// 工程状态的替身，仅用于验证 Docking 操作不能修改业务 Revision。
    /// 接入 CxVisual 时由 MEM Session/运行态数据替换；严禁把本类作为第二工程真源。
    /// </summary>
    public sealed class DemoEngineeringState
    {
        public int Revision { get; private set; }
        public string ProjectName { get { return "CxVisual 检测线演示工程"; } }
        public ObservableCollection<string> Logs { get; private set; }
        public ObservableCollection<string> Diagnostics { get; private set; }
        public event EventHandler RevisionChanged;

        public DemoEngineeringState()
        {
            Revision = 17;
            Logs = new ObservableCollection<string>();
            Diagnostics = new ObservableCollection<string>();
            AddDiagnostic("INFO | 设计态，可编辑工程；所有设备操作禁用。");
            AddDiagnostic("INFO | 当前只有 DemoEngineeringState，不接真实 MEM/MRK/PLC。");
        }

        public void SimulateEngineeringEdit()
        {
            Revision++;
            AddLog("[工程编辑] Rev " + Revision + "（模拟，不涉及任何设备）。");
            EventHandler handler = RevisionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void AddLog(string message)
        {
            Logs.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            while (Logs.Count > 200) Logs.RemoveAt(0);
        }

        public void AddDiagnostic(string message)
        {
            Diagnostics.Add(message);
            while (Diagnostics.Count > 100) Diagnostics.RemoveAt(0);
        }
    }
}
