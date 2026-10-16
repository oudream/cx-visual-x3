using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 配置实例的一致诊断视图。专供维护工具/EditorModel，不参与高速任务读取；
    /// 内含版本、待生效字段差异、规则问题和审计完整性。
    /// </summary>
    public sealed class MHSConfigStatus
    {
        internal MHSConfigStatus(FCMConfigIdentity identity, FCMConfigSchema schema,
            FCMConfigRevision revision, FCMConfigChangeSet pending,
            FCMConfigValidationReport saved, FCMConfigValidationReport effective,
            IList<FCMConfigIssue> integrity, int auditCount)
        {
            Identity = identity; Schema = schema; Revision = revision; PendingChanges = pending;
            SavedValidation = saved; EffectiveValidation = effective;
            IntegrityIssues = new List<FCMConfigIssue>(integrity).AsReadOnly();
            AuditCount = auditCount;
        }
        public FCMConfigIdentity Identity { get; private set; }
        public FCMConfigSchema Schema { get; private set; }
        public FCMConfigRevision Revision { get; private set; }
        public bool HasPending { get { return Revision.IsPending; } }
        public FCMConfigChangeSet PendingChanges { get; private set; }
        public FCMConfigValidationReport SavedValidation { get; private set; }
        public FCMConfigValidationReport EffectiveValidation { get; private set; }
        public IList<FCMConfigIssue> IntegrityIssues { get; private set; }
        public bool IsHealthy
        {
            get { return SavedValidation.IsValid && EffectiveValidation.IsValid && IntegrityIssues.Count == 0; }
        }
        public int AuditCount { get; private set; }
    }

    public sealed class MVBConfigLayerHealth
    {
        internal MVBConfigLayerHealth(string name, MHSConfigStatus status)
        { LayerName = name; Status = status; }
        public string LayerName { get; private set; }
        public MHSConfigStatus Status { get; private set; }
    }

    public sealed class MVBConfigCompositeStatus<T> where T : new()
    {
        internal MVBConfigCompositeStatus(MVBConfigResolved<T> snapshot,
            IList<MVBConfigLayerHealth> layers, FCMConfigValidationReport validation)
        {
            Snapshot = snapshot; Layers = new List<MVBConfigLayerHealth>(layers).AsReadOnly();
            Validation = validation;
        }
        public MVBConfigResolved<T> Snapshot { get; private set; }
        public FCMConfigRevisionVector RevisionVector { get { return Snapshot.RevisionVector; } }
        public IList<MVBConfigLayerHealth> Layers { get; private set; }
        public FCMConfigValidationReport Validation { get; private set; }
        public bool IsHealthy { get { return Validation.IsValid && !Layers.Any(x => !x.Status.IsHealthy); } }
    }
}
