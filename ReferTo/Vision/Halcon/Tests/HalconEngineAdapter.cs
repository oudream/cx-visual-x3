using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    // 最小引擎适配：用 HDevEngine 跑过程

    public interface IHalconEngine : IDisposable
    {
        void Init(string procDir, string workDir);
        void ExecuteProcedure(string procedure, HTuple inDict, out HTuple outDict, out string errMsg);
    }

    public sealed class HDevHalconEngine : IHalconEngine
    {
        private HDevEngine _engine;
        private string _workDir;

        public void Init(string procDir, string workDir)
        {
            _engine = new HDevEngine();
            _engine.SetProcedurePath(procDir);
            _workDir = workDir;

            if (!string.IsNullOrEmpty(_workDir))
                System.IO.Directory.SetCurrentDirectory(_workDir);
        }

        public void Init(HDevEngine engine)
        {
            _engine = engine;
        }

        public void ExecuteProcedure(string procedureName, HTuple inDict, out HTuple outDict, out string errMsg)
        {
            outDict = null;
            errMsg = string.Empty;

            try
            {
                HDevProcedure hdp = new HDevProcedure(procedureName);
                HDevProcedureCall hdpc = hdp.CreateCall();

                // 统一入口参数名：InDict
                hdpc.SetInputCtrlParamTuple("InDict", inDict);

                // 执行
                hdpc.Execute();

                // 新版错误返回：ErrMsg（非空即失败）
                HTuple hvErrMsg = hdpc.GetOutputCtrlParamTuple("ErrMsg");
                if (hvErrMsg != null && hvErrMsg.Length > 0 && !string.IsNullOrEmpty(hvErrMsg.S))
                    throw new Exception(hvErrMsg.S);

                // 新版输出：OutDict（字典）
                outDict = hdpc.GetOutputCtrlParamTuple("OutDict");
                if (outDict == null || outDict.Length <= 0)
                    throw new Exception("outDict 为空");
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
            }
        }

        public void Dispose()
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}
