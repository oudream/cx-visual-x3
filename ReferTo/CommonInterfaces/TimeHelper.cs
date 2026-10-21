using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class TimeHelper
    {
        public const string DefaultFormatDateTime = "yyyy/MM/dd HH:mm:ss";
        public const string DefaultFormatDateTimeMS = "yyyy/MM/dd HH:mm:ss:fff";

        // 获取当前时间戳
        public static long GetNow()
        {
            //return (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            return new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
        }

        // 时间戳（毫秒数）转日期
        public static string GetDateTimeString(long ms, string format = DefaultFormatDateTime)
        {
            // 将DateTime格式化为字符串
            return GetDateTime(ms).ToString(format);
        }

        public static string GetDateTimeString(DateTime dt, string format = DefaultFormatDateTimeMS)
        {
            // 将DateTime格式化为字符串
            return dt.ToString(format);
        }

        public static DateTime GetDateTime(long ms)
        {
            // 将Unix时间戳转换为DateTime
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime();
            // 将DateTime格式化为字符串
            return dateTime;
        }

        public static string GetNowTimeString()
        {
            DateTime currentTime = DateTime.Now;
            return currentTime.ToString("HH:mm:ss.fff");
        }

        public static long GetMs(DateTime dt)
        {
            //return (dt.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        }

        public static long GetDiffMsFromNow(DateTime dt)
        {
            // 计算两个 DateTime 对象之间的差异
            TimeSpan difference = DateTime.Now - dt;
            // 获取差异的总毫秒数
            return (long)difference.TotalMilliseconds;
        }

        // 今天最后一刻的毫秒时间戳
        public static long GetTodayEndMs()
        {
            DateTime now = DateTime.Now;
            DateTime end = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, 999);
            return GetMs(end);
        }

        public static bool TryParseDateTime(string dateTimeString, out DateTime dateTime)
        {
            // 定义可能的日期时间格式
            string[] formats = new string[]
            {
            "yyyy-MM-dd HH:mm:ss.fff", // 包括毫秒
            "yyyy-MM-dd HH:mm:ss:fff", // 包括毫秒
            "yyyy-MM-dd HH:mm:ss",     // 不包括毫秒
            "yyyy/MM/dd HH:mm:ss:fff", // 使用斜杠分隔符，包括毫秒
            "yyyy/MM/dd HH:mm:ss.fff", // 使用斜杠分隔符，包括毫秒
            "yyyy/MM/dd HH:mm:ss",     // 使用斜杠分隔符，不包括毫秒
            "yyyyMMddHHmmss",
            "yyyyMMdd",
            // 在这里可以根据需要添加更多格式
            };

            // 尝试解析字符串
            return DateTime.TryParseExact(dateTimeString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
        }
    }
}
