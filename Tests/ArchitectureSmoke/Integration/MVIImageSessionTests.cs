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
            // ARCH-D03：晚到的派生图不得跨原图版本/清理/新加载回写。
            long pendingResult = session.SourceVersion;
            long beforeReject = session.SourceVersion;
            int eventsBeforeReject = changes;
            session.BeginOriginalLoad();
            Check(!session.TrySetResult(pendingResult, second) &&
                session.Result == null && session.Original == first &&
                changes == eventsBeforeReject,
                "开始新原图加载后旧Result不得回写或发通知");
            long loadToken = session.SourceVersion;
            Check(session.TryCompleteOriginalLoad(loadToken, second),
                "新原图须能成功提交");
            Check(!session.TrySetResult(beforeReject, first) &&
                session.Result == null, "旧源版本不能覆盖新原图");
            long currentResultVersion = session.SourceVersion;
            int beforeCurrentCommit = changes;
            Check(session.TrySetResult(currentResultVersion, first) &&
                session.Result == first &&
                session.SourceVersion == currentResultVersion + 1 &&
                changes == beforeCurrentCommit + 1,
                "当前计算结果必须原子提交且只发一次通知");
            Check(!session.TrySetResult(currentResultVersion, second) &&
                session.Result == first &&
                session.SourceVersion == currentResultVersion + 1 &&
                changes == beforeCurrentCommit + 1,
                "同版本晚到第二份结果不得覆盖先提交结果");
            long versionBeforeClear = session.SourceVersion;
            session.Clear();
            Check(!session.TrySetResult(versionBeforeClear, first) &&
                session.Original == null && session.Result == null,
                "清空会话后旧Result不得重新出现");
            Check(!session.TrySetResult(session.SourceVersion, first),
                "无原图时不能提交孤立的结果");
            bool nullRejected = false;
            try { session.TrySetResult(session.SourceVersion, null); }
            catch (ArgumentNullException) { nullRejected = true; }
            Check(nullRejected, "异步结果不允许null");
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
