using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class ReflectionHelper
    {
        public static void SetDoubleBuffer(object obj)
        {
            //DataGridView
            PropertyInfo Property = obj.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Property?.SetValue(obj, true, null);
        }

    }
}
