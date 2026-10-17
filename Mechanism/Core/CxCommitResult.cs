using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxCommitResult
    {
        internal CxCommitResult(bool success, string errorCode, string message, int revision,
            IList<CxChangeOperation> inverse)
        {
            Success = success; ErrorCode = errorCode; Message = message; Revision = revision;
            InverseOperations = new ReadOnlyCollection<CxChangeOperation>(new List<CxChangeOperation>(inverse));
        }
        public static CxCommitResult Reject(string errorCode, int revision)
        {
            return new CxCommitResult(false, errorCode, errorCode, revision, new CxChangeOperation[0]);
        }
        public static CxCommitResult FromEdit(MTCEditResult<bool> result)
        { return new CxCommitResult(result.Success, result.Success ? "OK" : result.ErrorCode, result.Message, result.Revision, new CxChangeOperation[0]); }
        public bool Success { get; private set; }
        public string ErrorCode { get; private set; }
        public string Message { get; private set; }
        public int Revision { get; private set; }
        public ReadOnlyCollection<CxChangeOperation> InverseOperations { get; private set; }
    }
}
