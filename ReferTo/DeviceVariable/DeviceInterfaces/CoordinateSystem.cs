using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{

    public class CoordinateSystem : Device
    {
        public CoordinateSystemConfig _CoordinateSystemConfig { get; set; }
        public CoordinateSystem(CoordinateSystemConfig config) : base(config) { }
    }


    public class CoordinateSystemMethod
    {
        private List<AxisConfig> AxisesList { get; set; }
        private Dictionary<string, ControllerMethod> dicControllerMethods;

        public CoordinateSystemMethod() 
        { 
            AxisesList = new List<AxisConfig>();
            dicControllerMethods = new Dictionary<string, ControllerMethod>();
        }

        public CoordinateSystemMethod(List<AxisConfig> axes)
        {
            AxisesList = new List<AxisConfig>();
            if(axes != null)
                AxisesList.AddRange(axes);
            dicControllerMethods = new Dictionary<string, ControllerMethod>();
        }

        public int GetAxisNum()
        {
            return AxisesList.Count;
        }

        /// <summary>
        /// 轴是否已存在，存在返回列表下标，不存在返回-1
        /// </summary>
        public int AxisExiste(string axisName)
        {
            for (int i = 0; i < AxisesList.Count; i++)
            {
                if (AxisesList[i].Name == axisName)
                {
                    return i;
                }
            }

            return -1;
        }

        public void AddAxis(Axis axis)
        {
            if (axis != null)
            {
                if (AxisExiste(axis.Name) == -1)
                {
                    AxisesList.Add(axis.AxisConfig);
                }
            }
        }

        public void AddAxis(Axis axis, ControllerMethod method)
        {
            if (axis != null && method != null)
            {
                if (AxisExiste(axis.Name) == -1)
                {
                    AxisesList.Add(axis.AxisConfig);
                }
            }
        }

        public void SetAxisControllerMethod(string axisName, ControllerMethod method)
        {
            if (AxisExiste(axisName) != -1)
            {
                dicControllerMethods.Add(axisName, method);
            }
        }

        public bool Move(List<double> pos, MoveType type)
        {
            if (AxisesList.Count > 0)
            {
                if (type == MoveType.Absolute)
                {
                    for (int i = 0; i < AxisesList.Count; i++)
                    {
                        AxisConfig axis = AxisesList[i];
                        ControllerMethod method = GetAxisContrllerMethod(axis.Name);

                        if (method != null)
                        {
                            if (!method.AbsMove((ushort)axis.AxisNo, pos[i], axis.AxProfile.HighVel, axis.AxProfile.Tacc, axis.AxProfile.Tdec, 1))
                                return false;
                        }
                    }
                    return true;
                }
                else if (type == MoveType.Relative)
                {
                    for (int i = 0; i < AxisesList.Count; i++)
                    {
                        AxisConfig axis = AxisesList[i];
                        ControllerMethod method = GetAxisContrllerMethod(axis.Name);

                        if (method != null)
                        {
                            method.AbsMove((ushort)axis.AxisNo, pos[i], axis.AxProfile.HighVel,
                                axis.AxProfile.Tacc, axis.AxProfile.Tdec, 0);
                        }
                    }
                }
                else if (type == MoveType.Interpolation_SingleLine)
                {
                    string contrName = AxisesList[0].ControllerName;
                    for (int i = 1; i < AxisesList.Count; i++)
                    {
                        if (contrName != AxisesList[i].ControllerName)  //插补移动需要所有轴关联同一个控制器
                        {
                            return false;
                        }
                    }

                    ControllerMethod method = GetAxisContrllerMethod(AxisesList[0].Name);
                    List<ushort> axises = new List<ushort>();
                    for (int i = 0; i < AxisesList.Count; i++)
                        axises.Add((ushort)AxisesList[i].AxisNo);
                    return method.InterpolationMove(axises, pos, AxisesList[0].AxProfile.HighVel);
                }
            }
            return false;
        }

        /// <summary>
        /// 检测是否正在运行，正在运行返回true
        /// </summary>
        /// <returns></returns>
        public bool CheckMoving()
        {
            bool res = true;
            if (AxisesList.Count > 0)
            {
                for (int i = 0; i < AxisesList.Count; i++)
                {
                    AxisConfig axis = AxisesList[i];
                    ControllerMethod method = GetAxisContrllerMethod(axis.Name);

                    if (method != null)
                    {
                        res = res || method.CheckMove((ushort)axis.AxisNo);
                    }
                }
            }

            return res;
        }

        private ControllerMethod GetAxisContrllerMethod(string axisName)
        {
            if (dicControllerMethods.Keys.Contains(axisName))
                return dicControllerMethods[axisName];
            return null;
        }

    }

}
