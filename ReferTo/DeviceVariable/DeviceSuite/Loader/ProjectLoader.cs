using CxDeviceSuite.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Loader
{
    public static class ProjectLoader
    {
        public static Project LoadProject(string projectDirectory)
        {
            // TODO
            // 加载：Project Coroutine Procedure
            return new Project(new ProjectConfig { Id = 0, Name = string.Empty });
        }
    }
}
