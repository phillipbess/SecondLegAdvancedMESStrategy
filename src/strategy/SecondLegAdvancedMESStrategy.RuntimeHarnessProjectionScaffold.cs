using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies
{
    // Canonical projection seam for external runtime-harness consumption.
    // This file may flatten existing local runtime truth, but it must not
    // invent parallel runtime state or bypass the host-owned snapshot model.
    public sealed class RuntimeHarnessSnapshotProjection
    {
        public System.DateTime UtcNow { get; set; }
        public int StrategyQuantity { get; set; }
        public int AccountQuantity { get; set; }
        public bool DurableLiveTradeContext { get; set; }
        public IList<string> DurableLiveTradeContextSources { get; set; }
        public string RecoveryResolution { get; set; }
        public bool ProtectiveCoverageAmbiguous { get; set; }
        public bool OrphanAdoptionPending { get; set; }
        public bool ExplicitCoverageLossPending { get; set; }
        public bool ProtectiveReplacePending { get; set; }
        public bool ProtectiveReplaceFailurePending { get; set; }
        public bool ProtectiveReplaceRejected { get; set; }
        public string ProtectiveReplaceDisposition { get; set; }
        public string ProtectiveReplaceContext { get; set; }
        public string ProtectiveReplaceReason { get; set; }
        public string ProtectiveReplaceSourceOrderId { get; set; }
        public string ProtectiveReplaceTargetOrderId { get; set; }
        public string ProtectiveReplaceOco { get; set; }
        public System.DateTime ProtectiveReplaceStartedAtUtc { get; set; }
        public bool AdoptDeferPending { get; set; }
        public string AdoptDeferReason { get; set; }
        public bool SessionControlActive { get; set; }
        public string SessionControlReason { get; set; }
        public bool SessionNeutralizationPending { get; set; }
        public bool FlattenRejectPending { get; set; }
        public bool StaleChildCleanupPending { get; set; }
        public bool FlatResetDeferred { get; set; }
        public bool StateRestorationInProgress { get; set; }
        public bool OrphanedOrdersScanComplete { get; set; }
        public bool EntryAllowed { get; set; }
        public string ExitState { get; set; }
        public bool TradeOpen { get; set; }
        public bool ControllerStopPlaced { get; set; }
        public bool StopSubmissionPending { get; set; }
        public bool StopSubmitInFlight { get; set; }
        public int WorkingOrderCount { get; set; }
        public int WorkingPrimaryEntryOrderCount { get; set; }
        public int WorkingProtectiveOrderCount { get; set; }
        public int WorkingProtectiveOrderQuantityTotal { get; set; }
        public int OwnedProtectiveOrderCount { get; set; }
        public int CompatibleProtectiveOrderCount { get; set; }
        public string ProtectiveCoverageDisposition { get; set; }
        public string FinalCancelSweepDisposition { get; set; }
        public int CancelRequestCount { get; set; }
        public int EntryCancelRequestCount { get; set; }
        public int ProtectiveCancelRequestCount { get; set; }
        public int ProtectiveSubmitRequestCount { get; set; }
        public int FlattenRequestCount { get; set; }
        public int PreservedProtectiveOrderCount { get; set; }
        public string CurrentTradeId { get; set; }
        public string ActiveTradeId { get; set; }
        public string PersistedTradeId { get; set; }
        public string CountedTradeSessionId { get; set; }
        public int TradeCount { get; set; }
        public int TrackedFillQuantity { get; set; }
        public IList<string> Tokens { get; set; }

        public RuntimeHarnessSnapshotProjection()
        {
            DurableLiveTradeContextSources = new List<string>();
            RecoveryResolution = string.Empty;
            ProtectiveReplaceDisposition = string.Empty;
            ProtectiveReplaceContext = string.Empty;
            ProtectiveReplaceReason = string.Empty;
            ProtectiveReplaceSourceOrderId = string.Empty;
            ProtectiveReplaceTargetOrderId = string.Empty;
            ProtectiveReplaceOco = string.Empty;
            ProtectiveReplaceStartedAtUtc = System.DateTime.MinValue;
            AdoptDeferReason = string.Empty;
            SessionControlReason = string.Empty;
            ExitState = string.Empty;
            ProtectiveCoverageDisposition = string.Empty;
            FinalCancelSweepDisposition = string.Empty;
            CurrentTradeId = string.Empty;
            ActiveTradeId = string.Empty;
            PersistedTradeId = string.Empty;
            CountedTradeSessionId = string.Empty;
            Tokens = new List<string>();
        }
    }

    public partial class SecondLegAdvancedMESStrategy
    {
        internal RuntimeHarnessSnapshotProjection HarnessProjectedSnapshot =>
            BuildRuntimeHarnessSnapshotProjection("HarnessProjectedSnapshot");

        internal RuntimeHarnessSnapshotProjection BuildRuntimeHarnessSnapshotProjection(string reason = "")
        {
            RuntimeSnapshotScaffold snapshot = BuildRuntimeSnapshotScaffold(reason ?? string.Empty);
            return new RuntimeHarnessSnapshotProjection
            {
                UtcNow = snapshot.CapturedAtUtc,
                StrategyQuantity = snapshot.StrategyQuantity,
                AccountQuantity = snapshot.AccountQuantity,
                DurableLiveTradeContext = snapshot.DurableLiveTradeContext,
                DurableLiveTradeContextSources = new List<string>(snapshot.DurableLiveTradeContextSources),
                RecoveryResolution = snapshot.RecoveryResolution ?? string.Empty,
                ProtectiveCoverageAmbiguous = snapshot.ProtectiveCoverageAmbiguous,
                OrphanAdoptionPending = snapshot.OrphanAdoptionPending,
                ExplicitCoverageLossPending = snapshot.ExplicitCoverageLossPending,
                ProtectiveReplacePending = snapshot.ProtectiveReplacePending,
                ProtectiveReplaceFailurePending = snapshot.ProtectiveReplaceFailurePending,
                ProtectiveReplaceRejected = snapshot.ProtectiveReplaceRejected,
                ProtectiveReplaceDisposition = snapshot.ProtectiveReplaceDisposition ?? string.Empty,
                ProtectiveReplaceContext = snapshot.ProtectiveReplaceContext ?? string.Empty,
                ProtectiveReplaceReason = snapshot.ProtectiveReplaceReason ?? string.Empty,
                ProtectiveReplaceSourceOrderId = snapshot.ProtectiveReplaceSourceOrderId ?? string.Empty,
                ProtectiveReplaceTargetOrderId = snapshot.ProtectiveReplaceTargetOrderId ?? string.Empty,
                ProtectiveReplaceOco = snapshot.ProtectiveReplaceOco ?? string.Empty,
                ProtectiveReplaceStartedAtUtc = snapshot.ProtectiveReplaceStartedAtUtc,
                AdoptDeferPending = snapshot.AdoptDeferPending,
                AdoptDeferReason = snapshot.AdoptDeferReason ?? string.Empty,
                SessionControlActive = snapshot.SessionControlActive,
                SessionControlReason = snapshot.SessionControlReason ?? string.Empty,
                SessionNeutralizationPending = snapshot.SessionNeutralizationPending,
                FlattenRejectPending = snapshot.Finalization.FlattenRejectPending,
                StaleChildCleanupPending = snapshot.Finalization.StaleChildCleanupPending,
                FlatResetDeferred = snapshot.FlatResetDeferred,
                StateRestorationInProgress = snapshot.StateRestorationInProgress,
                OrphanedOrdersScanComplete = snapshot.OrphanedOrdersScanComplete,
                EntryAllowed = snapshot.EntryAllowed,
                ExitState = snapshot.Finalization.ExitState ?? string.Empty,
                TradeOpen = snapshot.Finalization.TradeOpen,
                ControllerStopPlaced = snapshot.Finalization.ControllerStopPlaced,
                StopSubmissionPending = snapshot.Finalization.StopSubmissionPending,
                StopSubmitInFlight = snapshot.Finalization.StopSubmitInFlight,
                WorkingOrderCount = snapshot.Coverage.WorkingOrderCount,
                WorkingPrimaryEntryOrderCount = snapshot.Coverage.WorkingPrimaryEntryOrderCount,
                WorkingProtectiveOrderCount = snapshot.Coverage.WorkingProtectiveOrderCount,
                WorkingProtectiveOrderQuantityTotal = snapshot.Coverage.WorkingProtectiveOrderQuantityTotal,
                OwnedProtectiveOrderCount = snapshot.Coverage.OwnedProtectiveOrderCount,
                CompatibleProtectiveOrderCount = snapshot.Coverage.CompatibleProtectiveOrderCount,
                ProtectiveCoverageDisposition = snapshot.Coverage.ProtectiveCoverageDisposition ?? string.Empty,
                FinalCancelSweepDisposition = snapshot.Finalization.FinalCancelSweepDisposition ?? string.Empty,
                CancelRequestCount = snapshot.Finalization.CancelRequestCount,
                EntryCancelRequestCount = snapshot.Finalization.EntryCancelRequestCount,
                ProtectiveCancelRequestCount = snapshot.Finalization.ProtectiveCancelRequestCount,
                ProtectiveSubmitRequestCount = snapshot.Finalization.ProtectiveSubmitRequestCount,
                FlattenRequestCount = snapshot.Finalization.FlattenRequestCount,
                PreservedProtectiveOrderCount = snapshot.Finalization.PreservedProtectiveOrderCount,
                CurrentTradeId = snapshot.TradeIdentity.CurrentTradeId ?? string.Empty,
                ActiveTradeId = snapshot.TradeIdentity.ActiveTradeId ?? string.Empty,
                PersistedTradeId = snapshot.TradeIdentity.PersistedTradeId ?? string.Empty,
                CountedTradeSessionId = snapshot.TradeIdentity.CountedTradeSessionId ?? string.Empty,
                TradeCount = snapshot.TradeIdentity.TradeCount,
                TrackedFillQuantity = snapshot.TradeIdentity.TrackedFillQuantity,
                Tokens = new List<string>(snapshot.Tokens),
            };
        }
    }
}
