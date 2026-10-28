using System;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageSessionTests
    {
        public static void Run()
        {
            FIMImageBuffer first = new FIMImageBuffer(1, 1, FIMPixelFormat.Gray8, new byte[] { 10 });
            FIMImageBuffer second = new FIMImageBuffer(1, 1, FIMPixelFormat.Gray8, new byte[] { 20 });
            MVIImageSession session = new MVIImageSession();
            int changes = 0;
            session.Changed += (sender, args) => { changes++; };
            long stale = session.BeginOriginalLoad();
            long latest = session.BeginOriginalLoad();
            Check(!session.TryCompleteOriginalLoad(stale, first), "过期异步图必须拒绝");
            Check(session.ActiveImage == null && changes == 0, "拒绝旧图不得发事件");
            Check(session.TryCompleteOriginalLoad(latest, second), "当前异步结果必须接受");
            Check(session.ActiveImage == second && changes == 1, "图像生效与Observer事件");
            long oldToken = session.BeginOriginalLoad();
            session.SetOriginal(first);
            Check(!session.TryCompleteOriginalLoad(oldToken, second) &&
                session.ActiveImage == first, "同步加载令牌应阻止旧异步图覆盖");
            session.SetResult(second);
            Check(session.HasResult && session.ActiveSlot == MVIImageSlot.Original, "原图和结果槽");
            session.Select(MVIImageSlot.Result);
            Check(session.ActiveImage == second, "结果图切换");
            session.Select(MVIImageSlot.Result);
            Check(changes == 4, "重复选择不发变更");
            long resultVersion = session.SourceVersion;
            Check(session.ClearResult() &&
                session.Original == first && session.Result == null &&
                session.ActiveImage == first &&
                session.ActiveSlot == MVIImageSlot.Original &&
                session.SourceVersion == resultVersion + 1 && changes == 5,
                "清理派生结果应保留原图、切回原图、只产生一次版本与通知");
            Check(!session.ClearResult() &&
                session.SourceVersion == resultVersion + 1 && changes == 5,
                "重复清理派生结果不得增加代次或发Observer通知");
            // 即使当前选择原图，清理Result也不能误清原图或改变选择。
            session.SetResult(second);
            resultVersion = session.SourceVersion;
            Check(session.ClearResult() && session.Original == first &&
                session.ActiveSlot == MVIImageSlot.Original &&
                session.ActiveImage == first && session.SourceVersion == resultVersion + 1,
                "原图激活时清理结果槽保持原图及代次语义");
            session.Clear();
            Check(session.ActiveImage == null && !session.HasResult &&
                session.ActiveSlot == MVIImageSlot.Original, "清理会话");
            bool failed = false;
            try { session.Select(MVIImageSlot.Result); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed, "无图禁止选择");
            Console.WriteLine("PASS MVI ImageSession SourceVersion与Observer");
        }

        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("MVI图像会话验收失败：" + reason);
        }
    }
}
