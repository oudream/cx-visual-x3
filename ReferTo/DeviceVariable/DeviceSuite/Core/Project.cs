using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    public class Project
    {
        public ProjectConfig Config { get; set; }
        private List<Coroutine> coroutines;
        public long Id { get; private set; }
        public string Name { get; private set; }

        public Project(ProjectConfig config)
        {
            Config = config;
            Id = Config.Id;
            Name = Config.Name;
            coroutines = new List<Coroutine>();
        }

        // 添加 Coroutine
        public void AddCoroutine(Coroutine coroutine)
        {
            coroutines.Add(coroutine);
        }

        // 删除 Coroutine
        public bool RemoveCoroutine(Coroutine coroutine)
        {
            return coroutines.Remove(coroutine);
        }

        // 修改 Coroutine
        public bool UpdateCoroutine(int index, Coroutine newCoroutine)
        {
            if (index >= 0 && index < coroutines.Count)
            {
                coroutines[index] = newCoroutine;
                return true;
            }
            return false;
        }

        // 查询 Coroutine
        public Coroutine GetCoroutine(int index)
        {
            if (index >= 0 && index < coroutines.Count)
            {
                return coroutines[index];
            }
            return null;
        }

        public int GetCoroutineCount()
        {
            return coroutines.Count;
        }

        //// 按时间范围查询 Coroutine
        //public List<Coroutine> FindCoroutinesByTimeRange(DateTime startTime, DateTime endTime)
        //{
        //    return coroutines.Where(c => c.StartTime >= startTime && c.StartTime <= endTime).ToList();
        //}
    }


    public class ProjectConfig
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ProjectDirectoryName { get; set; }
        public string Description { get; set; }
        public DateTime CreateTime { get; set; }
    }

}
