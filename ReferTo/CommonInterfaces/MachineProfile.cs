using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class MachineProfile
    {
        // 机械码（硬件CPU、硬盘码……的Hash码）
        public static string MechanicalHashCode { get; set; } = string.Empty;

        // 机械号
        public static uint MechanicalNumber { get; private set; } = 1101;

        public const string LineTitle = "线别";

        public const uint LineStart = 0;

        // 有多少条线（探测器、光源）
        // 最大支持 5 条线
        public const int LineCountMax = 5;
        // 默认 2 条线
        public const int LineCountDefault = 1;

        private static int _lineCount = LineCountDefault;

        // 当前启用的 LineKey（轻量级容器）
        private static uint[] _lineNumbers =Enumerable.Range(0, LineCountDefault)
              .Select(i => (uint)i)
              .ToArray();


        // 对外只读
        public static IReadOnlyList<uint> LineNumbers => _lineNumbers;

        public static int LineCount
        {
            get => _lineCount;
            private set
            {
                if (_lineCount == value)
                    return;

                // 防御：限制范围
                if (value < 1 || value > LineCountMax)
                    return;

                _lineCount = value;

                // ⭐ 直接按值生成，不再调用函数
                _lineNumbers = new uint[value];
                for (uint i = 0 + LineStart; i < value + LineStart; i++)
                {
                    _lineNumbers[i] = i;
                }
            }
        }

        public const string S_KEY_LineEnabledCount = "LineEnabledCount";

        public static string GetMechanicalId()
        {
            string cpuId = GetCpuId();
            string diskId = GetDiskId();
            //string macAddress = GetMacAddress();

            // 合并硬件信息生成唯一标识
            //string rawMachineId = $"{cpuId}-{diskId}-{macAddress}";
            string rawMachineId = $"{cpuId}-{diskId}";

            // 对标识进行哈希处理
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawMachineId));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
            }
        }

        private static string GetCpuId()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
            {
                foreach (var item in searcher.Get())
                {
                    return item["ProcessorId"]?.ToString() ?? "UNKNOWN";
                }
            }
            return "UNKNOWN";
        }

        private static string GetMotherboardId()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
            {
                foreach (var item in searcher.Get())
                {
                    return item["SerialNumber"]?.ToString() ?? "UNKNOWN";
                }
            }
            return "UNKNOWN";
        }

        private static string GetDiskId()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive"))
            {
                foreach (var item in searcher.Get())
                {
                    return item["SerialNumber"]?.ToString() ?? "UNKNOWN";
                }
            }
            return "UNKNOWN";
        }

        // YXeVHNGqEAEDoCmY19XIvyI/v83Q151UuqTxnJ77z1,1201,k1=v1,k2=v2
        public static bool GetVerifyKey()
        {
            string cpuId = GetCpuId();
            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive"))
            {
                foreach (var item in searcher.Get())
                {
                    string diskId = item["SerialNumber"]?.ToString() ?? "UNKNOWN";

                    string rawMachineId = $"{cpuId}-{diskId}";

                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawMachineId));
                        string Userkey = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();

                        var subId = Userkey.Substring(0, 10);
                        MechanicalHashCode = subId;

                        // 读取并解析 license.dat 文件
                        var licenseData = FileSystemHelper.ReadAllText(ConfigHelper.ConfigFilePath("license.dat"));
                        if (string.IsNullOrWhiteSpace(licenseData))
                        {
                            return false; // 如果文件为空，则验证失败
                        }

                        // 按逗号分割 license 和 machineNum
                        var licenseParts = licenseData.Split(',');
                        if (licenseParts.Length < 2)
                        {
                            return false; // 如果格式不正确，则验证失败
                        }

                        string license = licenseParts[0].Trim();
                        if (!uint.TryParse(licenseParts[1].Trim(), out uint machineNumber))
                        {
                            return false; // 如果 machineNum 不是有效的 uint，则验证失败
                        }
                        MechanicalNumber = machineNumber;

                        if (licenseParts.Length > 2)
                        {
                            var kvDict = new Dictionary<string, string>();
                            for (int i = 2; i < licenseParts.Length; i++)
                            {
                                var kv = licenseParts[i].Split('=');
                                if (kv.Length == 2)
                                {
                                    kvDict[kv[0].Trim()] = kv[1].Trim();
                                }
                            }

                            // 你可以根据需求使用 kvDict，例如打印或赋值到某些属性
                            // 示例：如果有特殊 key
                            if (kvDict.TryGetValue(S_KEY_LineEnabledCount, out var sLineEnabledCount))
                            {
                                if (int.TryParse(sLineEnabledCount, out int lineEnabledCount))
                                {
                                    if (lineEnabledCount > 0 && lineEnabledCount <= 3)
                                    {
                                        LineCount = lineEnabledCount;
                                    }
                                }
                            }
                        }
                        var pwd = GeneratePassword(subId);
                        if (license == pwd)
                        {
                            return true;
                        }

                    }
                }
            }
            return false;
        }


        private static string GetMacAddress()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT MACAddress FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL"))
            {
                foreach (var item in searcher.Get())
                {
                    return item["MACAddress"]?.ToString() ?? "UNKNOWN";
                }
            }
            return "UNKNOWN";
        }

        public static string GeneratePassword(string machineId)
        {
            // 使用机器ID生成绑定密码
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
                var b64 = Convert.ToBase64String(hashBytes);
                var reverse = StringHelper.Reverse(b64);
                return reverse.Substring(1, reverse.Length - 2);
            }
        }
    }
}
