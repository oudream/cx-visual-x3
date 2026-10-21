using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace CxDeviceSuite.Core
{
    public interface ICustomStateEvaluator
    {
        bool Evaluate();
    }


    // 自定义判定器类
    // TODO: 还没实现，需要先编译，再找到对应的类
    public class StateEvaluatorCustom : IStateEvaluator
    {
        private IStateEvaluator stateEvaluator;

        public StateEvaluatorCustom(IStateEvaluator stateEvaluator)
        {
            this.stateEvaluator = stateEvaluator;
        }

        public override List<(string deviceName, string variableName)> RequiredVariables()
        {
            return stateEvaluator.RequiredVariables();
        }

        public override bool Evaluate()
        {
            return stateEvaluator.Evaluate();
        }

        public override int GetTimeOutMS()
        {
            return stateEvaluator.GetTimeOutMS();
        }
    }
    
}
