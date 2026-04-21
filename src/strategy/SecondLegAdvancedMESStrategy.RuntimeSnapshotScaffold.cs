using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public sealed class RuntimeCoverageSnapshot
    {
        public int WorkingOrderCount { get; set; }
        public int WorkingPrimaryEntryOrderCount { get; set; }
        public int WorkingProtectiveOrderCount { get; set; }
        public int WorkingProtectiveOrderQuantityTotal { get; set; }
        public int OwnedProtectiveOrderCount { get; set; }
        public int CompatibleProtectiveOrderCount { get; set; }
        public string ProtectiveCoverageDisposition { get; set; }

        public RuntimeCoverageSnapshot()
        {
            ProtectiveCoverageDisposition = string.Empty;
        }
    }

    public sealed class RuntimeTradeIdentitySnapshot
    {
        public string CurrentTradeId { get; set; }
        public string ActiveTradeId { get; set; }
        public string PersistedTradeId { get; set; }
        public string CountedTradeSessionId { get; set; }
        public int TradeCount { get; set; }
        public int TrackedFillQuantity { get; set; }

        public RuntimeTradeIdentitySnapshot()
        {
            CurrentTradeId = string.Empty;
            ActiveTradeId = string.Empty;
            PersistedTradeId = string.Empty;
            CountedTradeSessionId = string.Empty;
        }
    }

    public sealed class RuntimeFinalizationSnapshot
    {
        public string ExitState { get; set; }
        public bool IsFinalizingTrade { get; set; }
        public bool SuppressAllOrderSubmissions { get; set; }
        public bool TradeOpen { get; set; }
        public bool ControllerStopPlaced { get; set; }
        public bool StopSubmissionPending { get; set; }
        public bool StopSubmitInFlight { get; set; }
        public bool StateRestorationInProgress { get; set; }
        public bool FlattenInFlight { get; set; }
        public bool FlattenRejectPending { get; set; }
        public bool StaleChildCleanupPending { get; set; }
        public string FinalCancelSweepDisposition { get; set; }
        public int CancelRequestCount { get; set; }
        public int EntryCancelRequestCount { get; set; }
        public int ProtectiveCancelRequestCount { get; set; }
        public int ProtectiveSubmitRequestCount { get; set; }
        public int FlattenRequestCount { get; set; }
        public int PreservedProtectiveOrderCount { get; set; }

        public RuntimeFinalizationSnapshot()
        {
            ExitState = string.Empty;
            FinalCancelSweepDisposition = string.Empty;
        }
    }

    public sealed class RuntimeSnapshotScaffold
    {
        public DateTime CapturedAtUtc { get; set; }
        public int StrategyQuantity { get; set; }
        public int AccountQuantity { get; set; }
        public bool DurableLiveTradeContext { get; set; }
        public IList<string> DurableLiveTradeContextSources { get; set; }
        public bool FlatResetDeferred { get; set; }
        public bool ProtectiveCoverageAmbiguous { get; set; }
        public string RecoveryResolution { get; set; }
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
        public DateTime ProtectiveReplaceStartedAtUtc { get; set; }
        public bool AdoptDeferPending { get; set; }
        public string AdoptDeferReason { get; set; }
        public bool SessionControlActive { get; set; }
        public string SessionControlReason { get; set; }
        public bool SessionNeutralizationPending { get; set; }
        public bool StateRestorationInProgress { get; set; }
        public bool OrphanedOrdersScanComplete { get; set; }
        public bool EntryAllowed { get; set; }
        public RuntimeCoverageSnapshot Coverage { get; set; }
        public RuntimeTradeIdentitySnapshot TradeIdentity { get; set; }
        public RuntimeFinalizationSnapshot Finalization { get; set; }
        public IList<string> Tokens { get; set; }

        public RuntimeSnapshotScaffold()
        {
            DurableLiveTradeContextSources = new List<string>();
            RecoveryResolution = string.Empty;
            ProtectiveReplaceDisposition = string.Empty;
            ProtectiveReplaceContext = string.Empty;
            ProtectiveReplaceReason = string.Empty;
            ProtectiveReplaceSourceOrderId = string.Empty;
            ProtectiveReplaceTargetOrderId = string.Empty;
            ProtectiveReplaceOco = string.Empty;
            ProtectiveReplaceStartedAtUtc = DateTime.MinValue;
            AdoptDeferReason = string.Empty;
            SessionControlReason = string.Empty;
            Coverage = new RuntimeCoverageSnapshot();
            TradeIdentity = new RuntimeTradeIdentitySnapshot();
            Finalization = new RuntimeFinalizationSnapshot();
            Tokens = new List<string>();
        }
    }

    public partial class SecondLegAdvancedMESStrategy
    {
        internal RuntimeSnapshotScaffold BuildRuntimeSnapshotScaffold(string reason = "")
        {
            RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();
            RuntimeTradeIdentitySnapshot tradeIdentity = BuildRuntimeTradeIdentitySnapshot();
            RuntimeFinalizationSnapshot finalization = BuildRuntimeFinalizationSnapshot();
            List<string> durableLiveTradeContextSources = BuildDurableLiveTradeContextSources();
            bool entryAllowed = ComputeRuntimeEntryAllowed(durableLiveTradeContextSources.Count > 0);

            return new RuntimeSnapshotScaffold
            {
                CapturedAtUtc = DateTime.UtcNow,
                StrategyQuantity = GetRuntimeStrategyQuantity(),
                AccountQuantity = _accountPositionQuantity,
                DurableLiveTradeContext = durableLiveTradeContextSources.Count > 0,
                DurableLiveTradeContextSources = durableLiveTradeContextSources,
                FlatResetDeferred = _flatResetDeferred,
                ProtectiveCoverageAmbiguous = _protectiveCoverageAmbiguous,
                RecoveryResolution = _recoveryResolution ?? string.Empty,
                OrphanAdoptionPending = _orphanAdoptionPending,
                ExplicitCoverageLossPending = _explicitCoverageLossPending,
                ProtectiveReplacePending = _protectiveReplacePending,
                ProtectiveReplaceFailurePending = _protectiveReplaceFailurePending,
                ProtectiveReplaceRejected = _protectiveReplaceRejected,
                ProtectiveReplaceDisposition = _protectiveReplaceDisposition ?? string.Empty,
                ProtectiveReplaceContext = _protectiveReplaceContext ?? string.Empty,
                ProtectiveReplaceReason = _protectiveReplaceReason ?? string.Empty,
                ProtectiveReplaceSourceOrderId = _protectiveReplaceSourceOrderId ?? string.Empty,
                ProtectiveReplaceTargetOrderId = _protectiveReplaceTargetOrderId ?? string.Empty,
                ProtectiveReplaceOco = _protectiveReplaceOco ?? string.Empty,
                ProtectiveReplaceStartedAtUtc = _protectiveReplaceStartedAtUtc,
                AdoptDeferPending = _adoptDeferPending,
                AdoptDeferReason = _adoptDeferReason ?? string.Empty,
                SessionControlActive = _sessionControlActive,
                SessionControlReason = _sessionControlReason ?? string.Empty,
                SessionNeutralizationPending = _sessionNeutralizationPending,
                StateRestorationInProgress = stateRestorationInProgress,
                OrphanedOrdersScanComplete = _orphanedOrdersScanComplete,
                EntryAllowed = entryAllowed,
                Coverage = coverage,
                TradeIdentity = tradeIdentity,
                Finalization = finalization,
                Tokens = BuildRuntimeSnapshotTokens(coverage, finalization, entryAllowed, reason ?? string.Empty),
            };
        }

        private RuntimeCoverageSnapshot BuildRuntimeCoverageSnapshot()
        {
            if (HasHarnessCoverageSnapshotOverride())
                return BuildHarnessCoverageSnapshotOverride();

            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            RuntimeCoverageSnapshot snapshot = new RuntimeCoverageSnapshot
            {
                WorkingOrderCount = state.WorkingOrders.Count,
            };

            foreach (Order order in state.WorkingOrders)
            {
                if (order == null || !OrderStateExtensions.IsWorkingLike(order.OrderState))
                    continue;

                bool isProtective = order.IsProtectiveStop();
                if (isProtective)
                {
                    snapshot.WorkingProtectiveOrderCount++;
                    snapshot.WorkingProtectiveOrderQuantityTotal += Math.Max(0, order.Quantity);
                    if (IsOurStrategyExitOrder(order))
                        snapshot.OwnedProtectiveOrderCount++;
                    else
                        snapshot.CompatibleProtectiveOrderCount++;

                    continue;
                }

                if (IsPrimaryEntryOrder(order))
                    snapshot.WorkingPrimaryEntryOrderCount++;
            }

            MergeBrokerWorkingCoverageSnapshot(snapshot);
            snapshot.ProtectiveCoverageDisposition = BuildProtectiveCoverageDisposition(snapshot);
            return snapshot;
        }

        private void MergeBrokerWorkingCoverageSnapshot(RuntimeCoverageSnapshot snapshot)
        {
            if (snapshot == null || Account?.Orders == null)
                return;

            var seenOrderIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Order workingOrder in _workingOrders)
            {
                string orderId = workingOrder?.OrderId ?? string.Empty;
                if (!string.IsNullOrEmpty(orderId))
                    seenOrderIds.Add(orderId);
            }

            int liveQty = Math.Abs(Position.Quantity);
            foreach (Order order in EnumerateBrokerWorkingOrders())
            {
                string orderId = order.OrderId ?? string.Empty;
                if (!string.IsNullOrEmpty(orderId) && seenOrderIds.Contains(orderId))
                    continue;

                if (!IsCompatibleBrokerProtectiveStopOrder(order, liveQty))
                    continue;

                snapshot.WorkingOrderCount++;
                snapshot.WorkingProtectiveOrderCount++;
                snapshot.WorkingProtectiveOrderQuantityTotal += Math.Max(0, order.Quantity);

                if (IsOurStrategyExitOrder(order))
                    snapshot.OwnedProtectiveOrderCount++;
                else
                    snapshot.CompatibleProtectiveOrderCount++;
            }
        }

        private string BuildProtectiveCoverageDisposition(RuntimeCoverageSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(_protectiveCoverageDisposition))
                return _protectiveCoverageDisposition;

            if (snapshot == null)
                return string.Empty;

            if (snapshot.OwnedProtectiveOrderCount > 0)
                return "covered-owned";

            if (snapshot.CompatibleProtectiveOrderCount > 0)
                return _protectiveCoverageAmbiguous ? "compatible-unattributed" : "covered-compatible";

            if (_stopSubmissionPending || _stopSubmitInFlight)
                return "pending-owned";

            return string.Empty;
        }

        private RuntimeTradeIdentitySnapshot BuildRuntimeTradeIdentitySnapshot()
        {
            return new RuntimeTradeIdentitySnapshot
            {
                CurrentTradeId = currentTradeID ?? string.Empty,
                ActiveTradeId = _activeTradeId ?? string.Empty,
                PersistedTradeId = _persistedTradeId ?? string.Empty,
                CountedTradeSessionId = _countedTradeSessionId ?? string.Empty,
                TradeCount = Math.Max(0, _tradesThisSession),
                TrackedFillQuantity = Math.Max(0, _trackedFillQuantity),
            };
        }

        private RuntimeFinalizationSnapshot BuildRuntimeFinalizationSnapshot()
        {
            return new RuntimeFinalizationSnapshot
            {
                ExitState = _exitState.ToString(),
                IsFinalizingTrade = isFinalizingTrade,
                SuppressAllOrderSubmissions = suppressAllOrderSubmissions,
                TradeOpen = tradeOpen,
                ControllerStopPlaced = controllerStopPlaced,
                StopSubmissionPending = _stopSubmissionPending,
                StopSubmitInFlight = _stopSubmitInFlight,
                StateRestorationInProgress = stateRestorationInProgress,
                FlattenInFlight = _flattenInFlight,
                FlattenRejectPending = _flattenRejectPending,
                StaleChildCleanupPending = _staleChildCleanupPending,
                FinalCancelSweepDisposition = _finalCancelSweepDisposition ?? string.Empty,
                CancelRequestCount = Math.Max(0, _cancelRequestCount),
                EntryCancelRequestCount = Math.Max(0, _entryCancelRequestCount),
                ProtectiveCancelRequestCount = Math.Max(0, _protectiveCancelRequestCount),
                ProtectiveSubmitRequestCount = Math.Max(0, _protectiveSubmitRequestCount),
                FlattenRequestCount = Math.Max(0, _flattenRequestCount),
                PreservedProtectiveOrderCount = Math.Max(0, _preservedProtectiveOrderCount),
            };
        }

        private bool HasDurableLiveTradeContext()
        {
            return BuildDurableLiveTradeContextSources().Count > 0;
        }

        private List<string> BuildDurableLiveTradeContextSources()
        {
            List<string> sources = new List<string>();

            if (GetRuntimeStrategyQuantity() != 0)
                sources.Add("strategy-position");
            if (_accountPositionQuantity != 0)
                sources.Add("account-position");
            if (entryPositionSide != MarketPosition.Flat)
                sources.Add("entry-side");
            if (entryFillPrice > 0.0)
                sources.Add("entry-fill-price");
            if (GetRuntimeStrategyAveragePrice() > 0.0)
                sources.Add("average-entry-price");
            if (_accountAveragePrice > 0.0)
                sources.Add("account-average-price");
            if (!string.IsNullOrEmpty(currentTradeID))
                sources.Add("current-trade-id");
            if (!string.IsNullOrEmpty(_activeTradeId))
                sources.Add("active-trade-id");

            return sources;
        }

        private bool ComputeRuntimeEntryAllowed(bool durableLiveTradeContext)
        {
            bool flattenRecoveryPending = _flattenRejectPending || _staleChildCleanupPending;
            bool recoveryResolutionPending = HasPendingRecoveryResolution();
            bool runtimeHoldPending =
                _protectiveCoverageAmbiguous
                || _orphanAdoptionPending
                || _explicitCoverageLossPending
                || _adoptDeferPending
                || _protectiveReplacePending
                || _protectiveReplaceFailurePending;

            return !stateRestorationInProgress
                && !_sessionControlActive
                && !durableLiveTradeContext
                && _orphanedOrdersScanComplete
                && !flattenRecoveryPending
                && !recoveryResolutionPending
                && !runtimeHoldPending;
        }

        private List<string> BuildRuntimeSnapshotTokens(
            RuntimeCoverageSnapshot coverage,
            RuntimeFinalizationSnapshot finalization,
            bool entryAllowed,
            string reason)
        {
            List<string> tokens = new List<string>();

            if (HasDurableLiveTradeContext())
                tokens.Add("durable-live-context");
            if (_flatResetDeferred)
                tokens.Add("flat-reset-deferred");
            if (!string.IsNullOrEmpty(_recoveryResolution))
                tokens.Add($"recovery-resolution:{_recoveryResolution}");
            if (_protectiveCoverageAmbiguous)
                tokens.Add("coverage-ambiguous");
            if (_orphanAdoptionPending)
                tokens.Add("orphan-adoption-pending");
            if (_explicitCoverageLossPending)
                tokens.Add("explicit-coverage-loss-pending");
            if (_protectiveReplacePending)
                tokens.Add("protective-replace-pending");
            if (_protectiveReplaceFailurePending)
                tokens.Add("protective-replace-failure-pending");
            if (_protectiveReplaceRejected)
                tokens.Add("protective-replace-rejected");
            if (!string.IsNullOrEmpty(_protectiveReplaceDisposition))
                tokens.Add($"protective-replace:{_protectiveReplaceDisposition}");
            if (!string.IsNullOrEmpty(_protectiveReplaceReason))
                tokens.Add($"protective-replace-reason:{_protectiveReplaceReason}");
            if (!string.IsNullOrEmpty(_protectiveReplaceSourceOrderId)
                || !string.IsNullOrEmpty(_protectiveReplaceTargetOrderId)
                || !string.IsNullOrEmpty(_protectiveReplaceOco))
            {
                tokens.Add("protective-replace-lineage");
            }
            if (_stopSubmissionPending)
                tokens.Add("protective-submit-pending");
            if (_stopSubmitInFlight)
                tokens.Add("protective-submit-inflight");
            if (_adoptDeferPending)
                tokens.Add("adopt-defer-pending");
            if (!string.IsNullOrEmpty(_adoptDeferReason))
                tokens.Add($"adopt-defer:{_adoptDeferReason}");
            if (_sessionControlActive)
                tokens.Add("session-control-active");
            if (!string.IsNullOrEmpty(_sessionControlReason))
                tokens.Add($"session-control:{_sessionControlReason}");
            if (_sessionNeutralizationPending)
                tokens.Add("session-neutralization-pending");
            if (!_orphanedOrdersScanComplete)
                tokens.Add("reconnect-grace");
            if (stateRestorationInProgress)
                tokens.Add("state-restoration");
            if (entryAllowed)
                tokens.Add("entry-allowed");
            if (coverage.OwnedProtectiveOrderCount > 0)
                tokens.Add("covered-owned");
            else if (_protectiveCoverageAmbiguous && coverage.CompatibleProtectiveOrderCount > 0)
                tokens.Add("compatible-unattributed");
            else if (coverage.CompatibleProtectiveOrderCount > 0)
                tokens.Add("covered-compatible");
            else if (_stopSubmissionPending || _stopSubmitInFlight)
                tokens.Add("pending-owned");
            if (finalization.FlattenRejectPending)
                tokens.Add("flatten-reject-pending");
            if (finalization.StaleChildCleanupPending)
                tokens.Add("stale-child-cleanup-pending");
            if (!string.IsNullOrEmpty(finalization.FinalCancelSweepDisposition))
                tokens.Add($"final-cancel-sweep:{finalization.FinalCancelSweepDisposition}");
            if (!string.IsNullOrEmpty(reason))
                tokens.Add($"snapshot-reason:{reason}");

            return tokens;
        }
    }
}
