using System;
using System.Collections.Generic;

namespace HalconDriverModule
{
    /// <summary>
    /// 人工判定结果（SRes13）兼容辅助类。
    /// 新格式在保留 B/O/D 旧内容的前提下，追加 U: 区域明细扩展段。
    /// </summary>
    public static class UserDefectContentHelper
    {
        private const string UserAreaPrefix = "U:";
        private const string UserAreaSplitToken = "|U:";

        public class SRes13ParseResult
        {
            public string GluePathContent { get; set; } = string.Empty;
            public ProductDetectionResultDetail GluePathDetail { get; set; } = new ProductDetectionResultDetail();
            public List<NGResultAreaDetail> UserAreas { get; set; } = new List<NGResultAreaDetail>();
        }

        /// <summary>
        /// 将旧 B/O/D 内容与 U: 区域扩展拼装为 SRes13。
        /// </summary>
        public static string ComposeSRes13(string gluePathContent, List<NGResultAreaDetail> userAreas)
        {
            var gluePath = gluePathContent ?? string.Empty;
            if (userAreas == null || userAreas.Count == 0)
            {
                return gluePath;
            }

            var userAreaContent = VisionDetectionResultContentHelper.SerializeDetectionAreaDetail(userAreas);
            if (string.IsNullOrEmpty(userAreaContent))
            {
                return gluePath;
            }

            if (string.IsNullOrEmpty(gluePath))
            {
                return $"{UserAreaPrefix}{userAreaContent}";
            }

            return $"{gluePath}{UserAreaSplitToken}{userAreaContent}";
        }

        /// <summary>
        /// 解析 SRes13，优先输出 U: 区域扩展，同时保留旧 B/O/D 解析结果。
        /// </summary>
        public static SRes13ParseResult ParseSRes13(string sRes13)
        {
            var result = new SRes13ParseResult();
            if (string.IsNullOrEmpty(sRes13))
            {
                return result;
            }

            SplitSRes13(sRes13, out var gluePathContent, out var userAreaContent);

            result.GluePathContent = gluePathContent;
            result.GluePathDetail = VisionDetectionResultContentHelper.DeserializeDetectionResultDetail(gluePathContent);

            if (!string.IsNullOrEmpty(userAreaContent))
            {
                result.UserAreas = VisionDetectionResultContentHelper.DeserializeDetectionAreaDetail(userAreaContent);
            }

            return result;
        }

        private static void SplitSRes13(string sRes13, out string gluePathContent, out string userAreaContent)
        {
            gluePathContent = string.Empty;
            userAreaContent = string.Empty;

            if (string.IsNullOrEmpty(sRes13))
            {
                return;
            }

            if (sRes13.StartsWith(UserAreaPrefix, StringComparison.Ordinal))
            {
                userAreaContent = sRes13.Substring(UserAreaPrefix.Length);
                return;
            }

            int idx = sRes13.IndexOf(UserAreaSplitToken, StringComparison.Ordinal);
            if (idx < 0)
            {
                gluePathContent = sRes13;
                return;
            }

            gluePathContent = sRes13.Substring(0, idx);
            userAreaContent = sRes13.Substring(idx + UserAreaSplitToken.Length);
        }
    }
}
