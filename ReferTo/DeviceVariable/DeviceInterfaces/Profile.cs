using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class Profile
    {
        /// <summary>
        /// 低速，jog、回零低速
        /// </summary>
        public double LowVel { get; set; }
        /// <summary>
        /// 高速，点位运动、回零高速、插补时使用的速度
        /// </summary>
        public double HighVel { get; set; }
        /// <summary>
        /// 加速度时间，单位s
        /// </summary>
        public double Tacc { get; set; }
        /// <summary>
        /// 减速度时间，单位s
        /// </summary>
        public double Tdec { get; set; }
        /// <summary>
        /// S段时间，单位s
        /// </summary>
        public double STime { get; set; }
        /// <summary>
        /// 速度参数是否在范围内
        /// </summary>
        public bool IsParamWithIn()
        {
            if (Tacc < 0.001 || Tacc > 3)
                return false;
            if (Tdec < 0.001 || Tdec > 3)
                return false;
            if (STime < 0.001 || Tacc > 3)
                return false;
            if (HighVel <= 0 || LowVel <= 0)
                return false;

            return true;
        }

        public Profile GetDefaultProfile()
        {
            LowVel = 1;
            HighVel = 5;
            Tacc = 0.1;
            Tdec = 0.1;
            STime = 0.1;
            return this;
        }
    }
}
