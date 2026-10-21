using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class NetHelper
    {
        public static bool ValidateIPv4(string ipString)
        {
            var ipAddress = ipString;
            if (string.IsNullOrEmpty(ipAddress))
                return false;
            string[] parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int num) && (num < 0 || num > 255))
                {
                    return false;
                }
            }
            return System.Net.IPAddress.TryParse(ipAddress, out _);
        }

        public static bool ValidatePort(string portString)
        {
            if (String.IsNullOrWhiteSpace(portString))
            {
                return false;
            }
            bool bOk = false;
            try
            {
                var v = int.Parse(portString);
                bOk = v > 0 && v < 65535;
            }
            catch (Exception)
            {
            }
            return bOk;
        }


        public static bool ValidatePort(decimal port)
        {
            return port > 0 && port < 65535;
        }
    }

}
