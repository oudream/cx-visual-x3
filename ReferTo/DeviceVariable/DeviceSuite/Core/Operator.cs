using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    public class CustomOperatorManager
    {
        // 存储和检索自定义算子代码，这里使用简单的字典作为示例
        private Dictionary<string, string> customOperators = new Dictionary<string, string>();

        public void AddOrUpdateOperator(string key, string code)
        {
            customOperators[key] = code;
        }

        public string GetOperatorCode(string key)
        {
            return customOperators.ContainsKey(key) ? customOperators[key] : null;
        }
    }

}
