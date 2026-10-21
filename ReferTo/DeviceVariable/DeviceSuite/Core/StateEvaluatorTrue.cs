using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    public class StateEvaluatorTrue : IStateEvaluator
    {
        private List<(string deviceName, string variableName)> requiredVariables = new List<(string deviceName, string variableName)>();

        public override List<(string deviceName, string variableName)> RequiredVariables()
        {
            return requiredVariables;
        }

        public override bool Evaluate()
        {
            return true;
        }

        public override int GetTimeOutMS()
        {
            return 0;
        }

    }
}
