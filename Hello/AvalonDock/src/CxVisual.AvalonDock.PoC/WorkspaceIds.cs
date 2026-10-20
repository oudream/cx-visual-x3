using System;
using System.Collections.Generic;

namespace CxVisual.DockingPoC
{
    /// <summary>UI 内容身份，不是 MEM 对象 ID。</summary>
    public static class WorkspaceIds
    {
        public const string Resources = "tool.resources";
        public const string Objects = "tool.objects";
        public const string Toolbox = "tool.toolbox";
        public const string Properties = "tool.properties";
        public const string Diagnostics = "tool.diagnostics";
        public const string Logs = "tool.logs";
        public const string Configuration = "doc.configuration";
        public const string Visual = "doc.visual";
        public const string Flow = "doc.flow";

        public static readonly string[] Tools =
            { Resources, Objects, Toolbox, Properties, Diagnostics, Logs };
        public static readonly string[] Documents =
            { Configuration, Visual, Flow };

        public static bool IsKnown(string id)
        {
            return Array.IndexOf(Tools, id) >= 0 || Array.IndexOf(Documents, id) >= 0;
        }

        public static bool IsTool(string id)
        {
            return Array.IndexOf(Tools, id) >= 0;
        }

        public static string TitleOf(string id)
        {
            switch (id)
            {
                case Resources: return "资源树";
                case Objects: return "对象树";
                case Toolbox: return "工具箱";
                case Properties: return "属性 / 绑定";
                case Diagnostics: return "诊断";
                case Logs: return "日志";
                case Configuration: return "资源配置";
                case Visual: return "图元编辑";
                case Flow: return "流程编排";
                default: throw new ArgumentException("未知 ContentId", "id");
            }
        }
    }
}
