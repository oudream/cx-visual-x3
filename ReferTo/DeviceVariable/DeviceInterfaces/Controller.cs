using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    /// <summary>
    /// 外部设备接口
    /// </summary>
    public class Controller : Device
    {
        public ControllerConfig _EquipmentConfig { get; }
       

        public Controller(ControllerConfig config):base(config)
        {
            _EquipmentConfig = config;

            
        }

    }


    public abstract class ControllerMethod : DeviceMethod
    {
        protected ControllerConfig contrConfig;
      
        /// <summary>
        /// 初始化
        /// </summary>
        public abstract string Initial();
        /// <summary>
        /// 关闭
        /// </summary>
        public abstract string Close();

        #region 轴相关函数
        /// <summary>
        /// jog移动
        /// </summary>
        public abstract bool JogMove(ushort axis, double speed, double tacc, double tdec, int dir);
        /// <summary>
        /// 点位运动
        /// </summary>
        public abstract bool AbsMove(ushort axis, double pos, double speed, double tacc, double tdec, int mode);
        /// <summary>
        /// 回零运动
        /// </summary>
        public abstract bool Home(ushort axis, double speed, double tacc, double tdec, int mode, double offset = 0);
        /// <summary>
        /// 停止，模式表示停止方式，默认正常停止，急停模式为1
        /// </summary>
        public abstract bool Stop(ushort axis, int mode = 0);

        /// <summary>
        /// 刷新感应器、位置等状态
        /// </summary>
        public abstract void UpdateStatus(Axis axis);
        /// <summary>
        /// 判断复位动作是否完成
        /// </summary>
        public abstract bool CheckResetDone(ushort axis);
        /// <summary>
        /// 检测轴是否在运动，返回true表示正在运动
        /// </summary>
        public abstract bool CheckMove(ushort axis);


        #endregion

        #region 插补相关函数

        public abstract bool InterpolationMove(List<ushort> axis, List<double> pos, double speed, int mode = 1);

        public abstract bool InterpolationMove_Conti(List<ushort> axis, List<List<double>> pos, double speed);

        #endregion

        #region IO相关函数

        public abstract bool WriteDO(int bit, int val);
        public abstract BitStatus ReadDO(int bit);
        public abstract BitStatus ReadDI(int bit);

        #endregion

        public ControllerMethod(ControllerConfig config):base()
        {
            contrConfig = config;
        }
    }
}
