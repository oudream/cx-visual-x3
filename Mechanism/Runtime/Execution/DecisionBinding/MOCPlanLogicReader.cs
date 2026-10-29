using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>Per-bind read-only lowering source. Loads the canonical Core codec bytes
    /// frozen in a Decision Plan, never a second editable graph or an MRK scheduler.
    /// Exact Content membership is enforced for nested calls and SourceMap traversal.</summary>
    internal sealed class MOCPlanLogicReader
    {
        private readonly Dictionary<Guid,MOLWiringDefinition> _wired =
            new Dictionary<Guid,MOLWiringDefinition>();
        private readonly Dictionary<Guid,MOLLogicDefinition> _structured =
            new Dictionary<Guid,MOLLogicDefinition>();

        internal MOCPlanLogicReader(MDCDeclarativePlan plan)
        {
            if(plan==null)throw new ArgumentNullException("plan");
            plan.RequireIntegrity();
            if(plan.Contents.Count==0 || plan.Contents.Count>1024 ||
                !plan.Contents.Any(x=>x.ContentId==plan.RootContentId))
                throw new InvalidOperationException("moc.plan.incomplete_closure");
            foreach(MDCPlanContent content in plan.Contents)
            {
                if(content==null || content.ContentId==Guid.Empty ||
                    _wired.ContainsKey(content.ContentId) ||
                    _structured.ContainsKey(content.ContentId))
                    throw new InvalidOperationException("moc.plan.invalid_content");
                if(content.IsWiring)
                    _wired.Add(content.ContentId,MOLWiringCodec.Deserialize(
                        content.CopyCanonicalLogicPayload(),content.SchemaVersion));
                else
                    _structured.Add(content.ContentId,MOLLogicCodec.Deserialize(
                        content.CopyCanonicalLogicPayload(),content.SchemaVersion));
            }
        }

        internal MOLWiringDefinition Wiring(MOCContentView content)
        {
            if(content==null)throw new InvalidOperationException("moc.plan.content_missing");
            MOLWiringDefinition result;
            if(_wired.TryGetValue(content.Id,out result))return result;
            if(_structured.ContainsKey(content.Id))return null;
            throw new InvalidOperationException("moc.plan.content_outside_closure:"+
                content.Id.ToString("N"));
        }

        internal MOLLogicDefinition Structured(MOCContentView content)
        {
            if(content==null)throw new InvalidOperationException("moc.plan.content_missing");
            MOLLogicDefinition result;
            if(_structured.TryGetValue(content.Id,out result))return result;
            if(_wired.ContainsKey(content.Id))return null;
            throw new InvalidOperationException("moc.plan.content_outside_closure:"+
                content.Id.ToString("N"));
        }
    }
}
