using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        // Local harness-adapter scaffold for the first runtime scenario pack.
        // This stays source-driven and intentionally avoids activating real NT8
        // order authority while we prepare the external harness integration.

        private int? _harnessStrategyQuantity;
        private double? _harnessStrategyAveragePrice;
        private bool _harnessCoverageOverrideEnabled;
        private int _harnessOwnedProtectiveOrderCount;
        private int _harnessCompatibleProtectiveOrderCount;
        private int _harnessWorkingProtectiveOrderCount;
        private int _harnessWorkingProtectiveOrderQuantityTotal;
        private bool _harnessHistorical;

        internal RuntimeSnapshotScaffold HarnessSnapshot => RuntimeSnapshot;
        internal RuntimeHarnessSnapshotProjection HarnessPack1Snapshot => HarnessProjectedSnapshot;

        internal void HarnessEnterHistorical(string context = "")
        {
            _harnessHistorical = true;
            SetStateRestorationInProgress(true, context ?? "Harness.EnterHistorical");
            RefreshRuntimeSnapshot(context ?? "Harness.EnterHistorical");
        }

        internal void HarnessEnterRealtime(string context = "")
        {
            _harnessHistorical = false;
            HandleFlatRealtimeRestartScaffold();
            RefreshRuntimeSnapshot(context ?? "Harness.EnterRealtime");
        }

        internal void HarnessSetAccountPosition(int quantity, double averagePrice, string context = "")
        {
            SetReconnectObservationState(_orphanedOrdersScanComplete, quantity, averagePrice);
            RefreshRuntimeSnapshot(context ?? "Harness.SetAccountPosition");
        }

        internal void HarnessSetStrategyPosition(int quantity, double averagePrice, string context = "")
        {
            _harnessStrategyQuantity = quantity;
            _harnessStrategyAveragePrice = averagePrice;
            entryPositionSide = quantity == 0 ? NinjaTrader.Cbi.MarketPosition.Flat : entryPositionSide;
            RefreshRuntimeSnapshot(context ?? "Harness.SetStrategyPosition");
        }

        internal void HarnessSetOrphanedOrdersScanComplete(bool complete, string context = "")
        {
            SetReconnectObservationState(complete, _accountPositionQuantity, _accountAveragePrice);
            RefreshRuntimeSnapshot(context ?? "Harness.SetOrphanedOrdersScanComplete");
        }

        internal void HarnessSetProtectiveCoverageAmbiguous(bool ambiguous, string context = "")
        {
            _protectiveCoverageAmbiguous = ambiguous;
            RefreshRuntimeSnapshot(context ?? "Harness.SetProtectiveCoverageAmbiguous");
        }

        internal void HarnessSetFlatResetDeferred(bool deferred, string context = "")
        {
            _flatResetDeferred = deferred;
            RefreshRuntimeSnapshot(context ?? "Harness.SetFlatResetDeferred");
        }

        internal void HarnessSetCoverageSnapshot(
            int ownedProtectiveOrderCount,
            int compatibleProtectiveOrderCount,
            int workingProtectiveOrderCount,
            int workingProtectiveOrderQuantityTotal,
            string context = "")
        {
            _harnessCoverageOverrideEnabled = true;
            _harnessOwnedProtectiveOrderCount = Math.Max(0, ownedProtectiveOrderCount);
            _harnessCompatibleProtectiveOrderCount = Math.Max(0, compatibleProtectiveOrderCount);
            _harnessWorkingProtectiveOrderCount = Math.Max(0, workingProtectiveOrderCount);
            _harnessWorkingProtectiveOrderQuantityTotal = Math.Max(0, workingProtectiveOrderQuantityTotal);
            SetProtectiveCoverageDisposition(
                string.Empty,
                context ?? "Harness.SetCoverageSnapshot");
            RefreshRuntimeSnapshot(context ?? "Harness.SetCoverageSnapshot");
        }

        internal void HarnessClearCoverageSnapshotOverride(string context = "")
        {
            _harnessCoverageOverrideEnabled = false;
            _harnessOwnedProtectiveOrderCount = 0;
            _harnessCompatibleProtectiveOrderCount = 0;
            _harnessWorkingProtectiveOrderCount = 0;
            _harnessWorkingProtectiveOrderQuantityTotal = 0;
            SetProtectiveCoverageDisposition(
                string.Empty,
                context ?? "Harness.ClearCoverageSnapshotOverride");
            RefreshRuntimeSnapshot(context ?? "Harness.ClearCoverageSnapshotOverride");
        }

        internal void HarnessEvaluateReconnectCoverage(string context = "")
        {
            RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();
            bool hasCoverage =
                coverage.OwnedProtectiveOrderCount > 0 || coverage.CompatibleProtectiveOrderCount > 0;
            bool orphanAdoptionPending = HasDurableLiveTradeContext()
                && !hasCoverage
                && !_protectiveCoverageAmbiguous
                && !_adoptDeferPending
                && !_protectiveReplacePending
                && !_protectiveReplaceFailurePending;

            SetProtectiveCoverageDisposition(
                BuildProtectiveCoverageDisposition(coverage),
                context ?? "Harness.EvaluateReconnectCoverage");
            SetRecoveryHoldState(
                orphanAdoptionPending,
                _protectiveReplacePending,
                _protectiveReplaceFailurePending,
                _protectiveReplaceRejected,
                _adoptDeferPending,
                _adoptDeferReason);
            RefreshRuntimeSnapshot(context ?? "Harness.EvaluateReconnectCoverage");
        }

        internal void HarnessStartTrade(string tradeId, string context = "")
        {
            currentTradeID = tradeId ?? string.Empty;
            _activeTradeId = currentTradeID;
            RefreshRuntimeSnapshot(context ?? "Harness.StartTrade");
        }

        internal void HarnessRecordPrimaryEntryFill(
            int cumulativeFilledQuantity,
            double averagePrice,
            string tradeId = "",
            string context = "")
        {
            if (!string.IsNullOrEmpty(tradeId))
            {
                currentTradeID = tradeId;
                _activeTradeId = tradeId;
            }

            EnsureActiveTradeIdFromCurrentTradeId(context ?? "Harness.RecordPrimaryEntryFill");
            if (!string.IsNullOrEmpty(currentTradeID)
                && !string.Equals(_countedTradeSessionId, currentTradeID, StringComparison.Ordinal))
            {
                _tradesThisSession++;
                _countedTradeSessionId = currentTradeID;
            }

            entryFillPrice = averagePrice;
            entryPrice = averagePrice;
            avgEntryPrice = averagePrice;
            TrackCumulativeFillQuantity(
                cumulativeFilledQuantity,
                context ?? "Harness.RecordPrimaryEntryFill");
            _harnessStrategyQuantity = cumulativeFilledQuantity;
            _harnessStrategyAveragePrice = averagePrice;
            RefreshRuntimeSnapshot(context ?? "Harness.RecordPrimaryEntryFill");
        }

        internal void HarnessPersistTradeState(string context = "")
        {
            CapturePersistedTradeIdentity(context ?? "Harness.PersistTradeState");
            SetStateRestorationInProgress(true, context ?? "Harness.PersistTradeState");
            RefreshRuntimeSnapshot(context ?? "Harness.PersistTradeState");
        }

        internal void HarnessRestorePersistedTradeState(string context = "")
        {
            if (!string.IsNullOrEmpty(_persistedTradeId))
            {
                currentTradeID = _persistedTradeId;
                _activeTradeId = _persistedTradeId;
            }

            RefreshRuntimeSnapshot(context ?? "Harness.RestorePersistedTradeState");
        }

        internal void HarnessSetStateRestorationInProgress(bool enabled, string context = "")
        {
            SetStateRestorationInProgress(enabled, context ?? "Harness.SetStateRestorationInProgress");
            RefreshRuntimeSnapshot(context ?? "Harness.SetStateRestorationInProgress");
        }

        internal void HarnessResetIntentCounters(string context = "")
        {
            SetRuntimeIntentCounters(0, 0, 0, 0, 0);
            SetFinalCancelSweepDisposition(string.Empty, 0, context ?? "Harness.ResetIntentCounters");
            RefreshRuntimeSnapshot(context ?? "Harness.ResetIntentCounters");
        }

        internal void HarnessSetIntentCounters(
            int cancelRequestCount,
            int entryCancelRequestCount,
            int protectiveCancelRequestCount,
            int protectiveSubmitRequestCount,
            int flattenRequestCount,
            string context = "")
        {
            SetRuntimeIntentCounters(
                cancelRequestCount,
                entryCancelRequestCount,
                protectiveCancelRequestCount,
                protectiveSubmitRequestCount,
                flattenRequestCount);
            RefreshRuntimeSnapshot(context ?? "Harness.SetIntentCounters");
        }

        internal void HarnessSetFinalCancelSweep(
            string disposition,
            int preservedProtectiveOrderCount,
            int cancelRequestCount = 0,
            int protectiveCancelRequestCount = 0,
            string context = "")
        {
            SetRuntimeIntentCounters(
                cancelRequestCount,
                _entryCancelRequestCount,
                protectiveCancelRequestCount,
                _protectiveSubmitRequestCount,
                _flattenRequestCount);
            SetFinalCancelSweepDisposition(
                disposition ?? string.Empty,
                preservedProtectiveOrderCount,
                context ?? "Harness.SetFinalCancelSweep");
            RefreshRuntimeSnapshot(context ?? "Harness.SetFinalCancelSweep");
        }

        internal void HarnessTerminate(string context = "")
        {
            RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();
            bool durableLiveTradeContext = HasDurableLiveTradeContext();
            int protectiveOrderCount = Math.Max(0, coverage.WorkingProtectiveOrderCount);

            if (protectiveOrderCount > 0 && durableLiveTradeContext)
            {
                SetFinalCancelSweepDisposition(
                    "skip",
                    protectiveOrderCount,
                    context ?? "Harness.Terminate");
                RefreshRuntimeSnapshot(context ?? "Harness.Terminate");
                return;
            }

            if (protectiveOrderCount > 0)
            {
                SetRuntimeIntentCounters(
                    _cancelRequestCount + protectiveOrderCount,
                    _entryCancelRequestCount,
                    _protectiveCancelRequestCount + protectiveOrderCount,
                    _protectiveSubmitRequestCount,
                    _flattenRequestCount);
                SetFinalCancelSweepDisposition(
                    "cancel",
                    0,
                    context ?? "Harness.Terminate");
                RefreshRuntimeSnapshot(context ?? "Harness.Terminate");
                return;
            }

            SetFinalCancelSweepDisposition(
                string.Empty,
                0,
                context ?? "Harness.Terminate");
            RefreshRuntimeSnapshot(context ?? "Harness.Terminate");
        }

        internal void HarnessRestoreProjectedSnapshot(
            RuntimeHarnessSnapshotProjection projection,
            string context = "")
        {
            if (projection == null)
                return;

            _harnessStrategyQuantity = projection.StrategyQuantity;
            _harnessStrategyAveragePrice = 0.0;
            entryFillPrice = 0.0;
            entryPrice = 0.0;
            avgEntryPrice = 0.0;
            entryPositionSide = projection.StrategyQuantity == 0
                ? NinjaTrader.Cbi.MarketPosition.Flat
                : NinjaTrader.Cbi.MarketPosition.Long;

            _flatResetDeferred = projection.FlatResetDeferred;
            _protectiveCoverageAmbiguous = projection.ProtectiveCoverageAmbiguous;
            _explicitCoverageLossPending = false;

            SetReconnectObservationState(
                projection.OrphanedOrdersScanComplete,
                projection.AccountQuantity,
                0.0);
            SetRecoveryHoldState(
                projection.OrphanAdoptionPending,
                projection.ProtectiveReplacePending,
                projection.ProtectiveReplaceFailurePending,
                projection.ProtectiveReplaceRejected,
                projection.AdoptDeferPending,
                NormalizeProjectionString(projection.AdoptDeferReason));
            SetSessionControlState(
                projection.SessionControlActive,
                NormalizeProjectionString(projection.SessionControlReason),
                projection.SessionNeutralizationPending);
            SetFlattenRecoveryState(
                projection.FlattenRejectPending,
                projection.StaleChildCleanupPending,
                "HarnessProjection");
            SetRuntimeIntentCounters(
                projection.CancelRequestCount,
                projection.EntryCancelRequestCount,
                projection.ProtectiveCancelRequestCount,
                projection.ProtectiveSubmitRequestCount,
                projection.FlattenRequestCount);
            SetFinalCancelSweepDisposition(
                NormalizeProjectionString(projection.FinalCancelSweepDisposition),
                projection.PreservedProtectiveOrderCount,
                context ?? "Harness.RestoreProjectedSnapshot");

            _harnessCoverageOverrideEnabled = true;
            _harnessOwnedProtectiveOrderCount = Math.Max(0, projection.OwnedProtectiveOrderCount);
            _harnessCompatibleProtectiveOrderCount = Math.Max(0, projection.CompatibleProtectiveOrderCount);
            _harnessWorkingProtectiveOrderCount = Math.Max(0, projection.WorkingProtectiveOrderCount);
            _harnessWorkingProtectiveOrderQuantityTotal =
                Math.Max(0, projection.WorkingProtectiveOrderQuantityTotal);
            SetProtectiveCoverageDisposition(
                NormalizeProjectionString(projection.ProtectiveCoverageDisposition),
                context ?? "Harness.RestoreProjectedSnapshot");

            currentTradeID = NormalizeProjectionString(projection.CurrentTradeId);
            _activeTradeId = NormalizeProjectionString(projection.ActiveTradeId);
            _persistedTradeId = NormalizeProjectionString(projection.PersistedTradeId);
            _countedTradeSessionId = NormalizeProjectionString(projection.CountedTradeSessionId);
            _tradesThisSession = Math.Max(0, projection.TradeCount);
            _trackedFillQuantity = 0;
            TrackCumulativeFillQuantity(
                projection.TrackedFillQuantity,
                context ?? "Harness.RestoreProjectedSnapshot");
            SetStateRestorationInProgress(
                projection.StateRestorationInProgress,
                context ?? "Harness.RestoreProjectedSnapshot");

            RefreshRuntimeSnapshot(context ?? "Harness.RestoreProjectedSnapshot");
        }

        private int GetRuntimeStrategyQuantity()
        {
            return _harnessStrategyQuantity ?? Position.Quantity;
        }

        private double GetRuntimeStrategyAveragePrice()
        {
            return _harnessStrategyAveragePrice ?? avgEntryPrice;
        }

        private bool HasHarnessCoverageSnapshotOverride()
        {
            return _harnessCoverageOverrideEnabled;
        }

        private RuntimeCoverageSnapshot BuildHarnessCoverageSnapshotOverride()
        {
            RuntimeCoverageSnapshot snapshot = new RuntimeCoverageSnapshot
            {
                WorkingOrderCount = _harnessWorkingProtectiveOrderCount,
                WorkingPrimaryEntryOrderCount = 0,
                WorkingProtectiveOrderCount = _harnessWorkingProtectiveOrderCount,
                WorkingProtectiveOrderQuantityTotal = _harnessWorkingProtectiveOrderQuantityTotal,
                OwnedProtectiveOrderCount = _harnessOwnedProtectiveOrderCount,
                CompatibleProtectiveOrderCount = _harnessCompatibleProtectiveOrderCount,
            };

            snapshot.ProtectiveCoverageDisposition = BuildProtectiveCoverageDisposition(snapshot);
            return snapshot;
        }

        private static string NormalizeProjectionString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
    }
}
