using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    // VariableTable 的壳
    public class PointSheet
    {
        public List<PointItem> Points { get; private set; }

        public PointSheet()
        {
            Points = new List<PointItem>();
        }

        public void AddPointItem(PointItem point)
        {
            Points.Add(point);
        }

        public bool RemovePointItem(string deviceName, string variableName)
        {
            var item = Points.FirstOrDefault(p => p.DeviceName == deviceName && p.VariableName == variableName);
            return item != null && Points.Remove(item);
        }

        public bool UpdatePointItem(string deviceName, string variableName, PointItem newPoint)
        {
            var index = Points.FindIndex(p => p.DeviceName == deviceName && p.VariableName == variableName);
            if (index != -1)
            {
                Points[index] = newPoint;
                return true;
            }
            return false;
        }

        // 按设备名查询
        public List<PointItem> FindPointsByDeviceName(string deviceName)
        {
            return Points.Where(p => p.DeviceName == deviceName).ToList();
        }

        // 按属性名查询
        public List<PointItem> FindPointsByVariableName(string variableName)
        {
            return Points.Where(p => p.VariableName == variableName).ToList();
        }

        // 按属性名查询
        public List<PointItem> FindPointsByTag(string tag)
        {
            return Points.Where(p => p.HasTag(tag)).ToList();
        }

        // 按设备名、属性名查询
        public PointItem FindPoint(string deviceName, string variableName)
        {
            for (int i = 0; i < Points.Count; i++)
            {
                if (Points[i].DeviceName == deviceName && Points[i].VariableName == variableName)
                {
                    return Points[i];
                }
            }
            return null; // 如果遍历完都没有找到，返回 null
        }

        // 获取设备名列表
        public List<string> GetDeviceNameList()
        {
            return Points.Select(p => p.DeviceName).Distinct().ToList();
        }

    }

}
