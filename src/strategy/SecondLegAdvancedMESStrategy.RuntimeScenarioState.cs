using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        // Harness-facing runtime state contract.
        // These fields are intentionally separate from the entry thesis so the
        // external runtime harness can reason about restart/reconnect/coverage
        // behavior without binding to second-leg setup logic.

        private bool _flatResetDeferred;
        private bool _protectiveCoverageAmbiguous;
        private bool _orphanAdoptionPending;
        private bool _explicitCoverageLossPending;
        private bool _protectiveReplacePending;
        private bool _protectiveReplaceFailurePending;
        private bool _protectiveReplaceRejected;
        private string _protectiveReplaceDisposition = string.Empty;
        private string _protectiveReplaceContext = string.Empty;
        private string _protectiveReplaceReason = string.Empty;
        private string _protectiveReplaceSourceOrderId = string.Empty;
        private string _protectiveReplaceTargetOrderId = string.Empty;
        private string _protectiveReplaceOco = string.Empty;
        private DateTime _protectiveReplaceStartedAtUtc = DateTime.MinValue;
        private bool _adoptDeferPending;
        private string _adoptDeferReason = string.Empty;
        private bool _orphanedOrdersScanComplete = true;
        private int _accountPositionQuantity;
        private double _accountAveragePrice;
        private bool _sessionControlActive;
        private string _sessionControlReason = string.Empty;
        private bool _sessionNeutralizationPending;
        private bool _flattenRejectPending;
        private bool _staleChildCleanupPending;
        private string _protectiveCoverageDisposition = string.Empty;
        private string _finalCancelSweepDisposition = string.Empty;
        private string _countedTradeSessionId = string.Empty;
        private int _trackedFillQuantity;
        private int _cancelRequestCount;
        private int _entryCancelRequestCount;
        private int _protectiveCancelRequestCount;
        private int _protectiveSubmitRequestCount;
        private int _flattenRequestCount;
        private int _preservedProtectiveOrderCount;

        private void ResetRuntimeScenarioState()
        {
            _flatResetDeferred = false;
            _protectiveCoverageAmbiguous = false;
            _orphanAdoptionPending = false;
            _explicitCoverageLossPending = false;
            _protectiveReplacePending = false;
            _protectiveReplaceFailurePending = false;
            _protectiveReplaceRejected = false;
            _protectiveReplaceDisposition = string.Empty;
            _protectiveReplaceContext = string.Empty;
            _protectiveReplaceReason = string.Empty;
            _protectiveReplaceSourceOrderId = string.Empty;
            _protectiveReplaceTargetOrderId = string.Empty;
            _protectiveReplaceOco = string.Empty;
            _protectiveReplaceStartedAtUtc = DateTime.MinValue;
            _adoptDeferPending = false;
            _adoptDeferReason = string.Empty;
            _orphanedOrdersScanComplete = true;
            _accountPositionQuantity = 0;
            _accountAveragePrice = 0.0;
            _sessionControlActive = false;
            _sessionControlReason = string.Empty;
            _sessionNeutralizationPending = false;
            _flattenRejectPending = false;
            _staleChildCleanupPending = false;
            _protectiveCoverageDisposition = string.Empty;
            _finalCancelSweepDisposition = string.Empty;
            _trackedFillQuantity = 0;
            _cancelRequestCount = 0;
            _entryCancelRequestCount = 0;
            _protectiveCancelRequestCount = 0;
            _protectiveSubmitRequestCount = 0;
            _flattenRequestCount = 0;
            _preservedProtectiveOrderCount = 0;
            // `_countedTradeSessionId` is a trade-identity field and stays under
            // trade lifecycle ownership instead of being blindly reset here.
        }

        private void SetProtectiveCoverageDisposition(string disposition, string context)
        {
            _protectiveCoverageDisposition = disposition ?? string.Empty;
            WriteDebugLog(
                $"[HARNESS_STATE] protectiveCoverageDisposition={_protectiveCoverageDisposition} ctx={context}");
            WriteRiskEvent(
                "PROTECTIVE_COVERAGE",
                $"disposition={_protectiveCoverageDisposition}",
                $"ctx={context}");
        }

        private void SetSessionControlState(bool active, string reason, bool neutralizationPending)
        {
            _sessionControlActive = active;
            _sessionControlReason = active ? (reason ?? string.Empty) : string.Empty;
            _sessionNeutralizationPending = neutralizationPending;
        }

        private void SetRecoveryHoldState(
            bool orphanAdoptionPending,
            bool protectiveReplacePending,
            bool protectiveReplaceFailurePending,
            bool protectiveReplaceRejected,
            bool adoptDeferPending,
            string adoptDeferReason)
        {
            _orphanAdoptionPending = orphanAdoptionPending;
            _protectiveReplacePending = protectiveReplacePending;
            _protectiveReplaceFailurePending = protectiveReplaceFailurePending;
            _protectiveReplaceRejected = protectiveReplaceRejected;
            _adoptDeferPending = adoptDeferPending;
            _adoptDeferReason = adoptDeferPending ? (adoptDeferReason ?? string.Empty) : string.Empty;
        }

        private void SetProtectiveReplaceLineageState(
            string disposition,
            string context,
            string reason,
            string sourceOrderId,
            string targetOrderId,
            string oco,
            DateTime startedAtUtc)
        {
            _protectiveReplaceDisposition = disposition ?? string.Empty;
            _protectiveReplaceContext = string.IsNullOrEmpty(_protectiveReplaceDisposition)
                ? string.Empty
                : (context ?? string.Empty);
            _protectiveReplaceReason = string.IsNullOrEmpty(_protectiveReplaceDisposition)
                ? string.Empty
                : (reason ?? string.Empty);
            _protectiveReplaceSourceOrderId = sourceOrderId ?? string.Empty;
            _protectiveReplaceTargetOrderId = targetOrderId ?? string.Empty;
            _protectiveReplaceOco = oco ?? string.Empty;
            _protectiveReplaceStartedAtUtc = string.IsNullOrEmpty(_protectiveReplaceDisposition)
                ? DateTime.MinValue
                : startedAtUtc;
        }

        private void SetReconnectObservationState(
            bool orphanedOrdersScanComplete,
            int accountPositionQuantity,
            double accountAveragePrice)
        {
            _orphanedOrdersScanComplete = orphanedOrdersScanComplete;
            _accountPositionQuantity = accountPositionQuantity;
            _accountAveragePrice = accountAveragePrice;
        }

        private void SetFlattenRecoveryState(
            bool flattenRejectPending,
            bool staleChildCleanupPending,
            string context = "")
        {
            _flattenRejectPending = flattenRejectPending;
            _staleChildCleanupPending = staleChildCleanupPending;
            WriteRiskEvent(
                "FLATTEN_RECOVERY",
                $"flattenRejectPending={_flattenRejectPending}",
                $"staleChildCleanupPending={_staleChildCleanupPending}",
                $"ctx={context}");
        }

        private void SetRuntimeIntentCounters(
            int cancelRequestCount,
            int entryCancelRequestCount,
            int protectiveCancelRequestCount,
            int protectiveSubmitRequestCount,
            int flattenRequestCount)
        {
            _cancelRequestCount = Math.Max(0, cancelRequestCount);
            _entryCancelRequestCount = Math.Max(0, entryCancelRequestCount);
            _protectiveCancelRequestCount = Math.Max(0, protectiveCancelRequestCount);
            _protectiveSubmitRequestCount = Math.Max(0, protectiveSubmitRequestCount);
            _flattenRequestCount = Math.Max(0, flattenRequestCount);
        }

        private void SetFinalCancelSweepDisposition(
            string disposition,
            int preservedProtectiveOrderCount,
            string context)
        {
            _finalCancelSweepDisposition = disposition ?? string.Empty;
            _preservedProtectiveOrderCount = Math.Max(0, preservedProtectiveOrderCount);
            WriteDebugLog(
                $"[HARNESS_STATE] finalCancelSweepDisposition={_finalCancelSweepDisposition} preservedProtectiveOrderCount={_preservedProtectiveOrderCount} ctx={context}");
        }
    }
}
