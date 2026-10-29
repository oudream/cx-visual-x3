using System;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>Professional declaration, not an authorization to execute a device command.</summary>
    public enum MDCOperationEffect
    { Pure = 1, IdempotentExternal = 2, NonIdempotentExternal = 3, Unknown = 4 }

    /// <summary>Receipt is the externally observed fact, not an inferred success flag.</summary>
    public enum MDCOperationReceipt
    { NotSent = 1, ConfirmedNoEffect = 2, ConfirmedApplied = 3, AcknowledgmentUnknown = 4 }

    /// <summary>
    /// D06 static retry policy; no timers, device dispatch, retry counters or
    /// second Runtime state machine. Even Allowed requires MRK/Host revalidation.
    /// </summary>
    public sealed class MDCRecoverySafetyAssessor
    {
        public MDCAssessment AssessRetry(MDCOperationEffect effect,
            MDCOperationReceipt receipt, bool verifiedIdempotency, bool hostApproved)
        {
            if (!Enum.IsDefined(typeof(MDCOperationEffect), effect))
                throw new ArgumentOutOfRangeException("effect");
            if (!Enum.IsDefined(typeof(MDCOperationReceipt), receipt))
                throw new ArgumentOutOfRangeException("receipt");

            // External acknowledgment Unknown is never treated as a failed action.
            if (receipt == MDCOperationReceipt.AcknowledgmentUnknown)
                return One(effect == MDCOperationEffect.NonIdempotentExternal
                    ? MDCVerdict.Rejected : MDCVerdict.Unknown,
                    "MDC_RETRY_RECONCILIATION_REQUIRED",
                    "a real device may already have acted; reconcile before any retry");
            if (receipt == MDCOperationReceipt.ConfirmedApplied)
                return One(MDCVerdict.Rejected, "MDC_RETRY_ALREADY_APPLIED",
                    "already completed external action must not be repeated");
            if (receipt == MDCOperationReceipt.NotSent)
                return One(MDCVerdict.Rejected, "MDC_RETRY_NOT_DISPATCHED",
                    "first dispatch is not a retry");
            if (effect == MDCOperationEffect.Unknown)
                return One(MDCVerdict.Unknown, "MDC_RETRY_EFFECT_UNKNOWN",
                    "professional Owner has not established side-effect semantics");
            if (effect == MDCOperationEffect.Pure)
                return One(MDCVerdict.Allowed, "MDC_RETRY_PURE_STATIC",
                    "pure computation may be retried under Runtime policy");
            if (!hostApproved)
                return One(MDCVerdict.Unknown, "MDC_RETRY_HOST_AUTHORIZATION",
                    "Host must authorize this operation at execution time");
            if (effect == MDCOperationEffect.NonIdempotentExternal)
                return One(MDCVerdict.Unknown, "MDC_RETRY_EXTERNAL_OWNER_APPROVAL",
                    "professional Owner must confirm that an external side effect cannot recur");
            if (!verifiedIdempotency)
                return One(MDCVerdict.Unknown, "MDC_RETRY_IDEMPOTENCY_UNPROVEN",
                    "declaring an operation idempotent is not enough without evidence");
            return One(MDCVerdict.Allowed, "MDC_RETRY_POLICY_ELIGIBLE",
                "statically eligible, subject to fresh Host/device authorization and actual MRK execution");
        }

        private static MDCAssessment One(MDCVerdict verdict, string code, string reason)
        {
            return new MDCAssessment(new[] {
                new MDCConstraintOutcome(code, verdict, reason)
            });
        }
    }
}
