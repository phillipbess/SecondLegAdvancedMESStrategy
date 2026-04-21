using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        // Ownership note:
        // - RuntimeHost.cs remains the entrypoint owner for SaveStrategyState(),
        //   RestoreStrategyState(), and ResetTradeState().
        // - This file only defines the donor-shaped persistence contract and
        //   helper stubs that the RuntimeHost placeholders can delegate into later.
        //
        // Reset scope notes:
        // - Durable state survives restart/recovery and should only be cleared on
        //   explicit shutdown/disable flows or after authoritative reconciliation.
        // - Trade-scoped state is reset after a completed trade and must not leak
        //   into the next setup/trade lifecycle.
        // - Derived setup state stays owned by ResetSetupState(reason) in
        //   StateLifecycle.cs so pattern detection and session rollover semantics
        //   remain isolated from broker-authoritative recovery.

        private const string StrategyStateFieldsHeaderMarker = "# StateFields:";
        private const string StrategyStateFormatVersionMarker = "# StateFormatVersion:";
        private const int StrategyStateFormatVersion = 1;

        private static readonly string[] DocumentedDurableStateKeys =
        {
            "SessionAnchor",
            "TradesThisSession",
            "ConsecutiveLosses",
            "SessionRealizedR",
            "DailyStopHit",
            "DailyLossHit",
            "UnknownExitEconomicsBlock",
            "AutoDisabled",
            "GlobalKillSwitch",
            "CurrentTradeId",
            "ActiveTradeId",
            "CurrentExitOco",
            "ActiveEntrySignal",
            "CurrentEntryTag",
            "OcoCounter",
            "EntryPositionSide",
            "EntryFillTime",
            "EntryFillPrice",
            "EntryQuantity",
            "EntryPrice",
            "AvgEntryPrice",
            "InitialPositionSize",
            "TradeRiskPerContract",
            "InitialStopPrice",
            "InitialTradeRisk",
            "WorkingStopPrice",
            "HasWorkingStop",
            "CurrentControllerStopPrice",
        };

        private static readonly string[] DocumentedTradeScopedStateKeys =
        {
            "TradeOpen",
            "ControllerStopPlaced",
            "ProtectiveCoverageAmbiguous",
            "ProtectiveCoverageDisposition",
            "RecoveryResolution",
            "OrphanedOrdersScanComplete",
            "OrphanAdoptionPending",
            "ExplicitCoverageLossPending",
            "ProtectiveReplacePending",
            "ProtectiveReplaceFailurePending",
            "ProtectiveReplaceRejected",
            "ProtectiveReplaceDisposition",
            "ProtectiveReplaceContext",
            "ProtectiveReplaceReason",
            "ProtectiveReplaceSourceOrderId",
            "ProtectiveReplaceTargetOrderId",
            "ProtectiveReplaceOco",
            "ProtectiveReplaceStartedAtUtc",
            "AdoptDeferPending",
            "AdoptDeferReason",
            "LastStopStateChangeAtUtc",
            "CoverageGraceUntilUtc",
            "StopSubmitInFlight",
            "StopSubmissionPending",
            "LastStopSubmitAtUtc",
            "LastStopSubmissionAtUtc",
            "ExitState",
            "FlattenRejectPending",
            "StaleChildCleanupPending",
            "FinalCancelSweepDisposition",
            "PreservedProtectiveOrderCount",
            "HasWorkingEntry",
            "IsFinalizingTrade",
            "SuppressAllOrderSubmissions",
            "StateRestorationInProgress",
        };

        private bool ShouldPersistStrategyState()
        {
            return State == State.Realtime;
        }

        private void InitializeStatePersistencePath()
        {
            string accountName = Account?.DisplayName ?? Account?.Name ?? "UnknownAccount";
            string instrumentName = Instrument?.MasterInstrument?.Name ?? "UnknownInstrument";
            string safeAccountName = SanitizeStatePathSegment(accountName);
            string safeInstrumentName = SanitizeStatePathSegment(instrumentName);

            stateFilePath = Path.Combine(
                NinjaTrader.Core.Globals.UserDataDir,
                "strategies",
                $"SecondLegAdvanced_{safeAccountName}_{safeInstrumentName}_state.txt");
        }

        private void AppendStrategyStateHeaderScaffold(ICollection<string> lines)
        {
            // TODO: Keep header comments explicit so donor-port diffs stay readable.
            if (lines == null)
                return;

            string[] allFields = DocumentedDurableStateKeys
                .Concat(DocumentedTradeScopedStateKeys)
                .Concat(new[]
                {
                    "PersistedTradeId",
                    "CountedTradeSessionId",
                    "TrackedFillQuantity",
                })
                .ToArray();

            lines.Add($"{StrategyStateFieldsHeaderMarker} {string.Join(", ", allFields)}");
            lines.Add($"{StrategyStateFormatVersionMarker} {StrategyStateFormatVersion}");
        }

        private void AppendDurableStateSnapshotScaffold(ICollection<string> lines)
        {
            // TODO: Capture restart-authoritative state only:
            // - session/risk gates (_sessionAnchor, _tradesThisSession,
            //   _consecutiveLosses, _sessionRealizedR)
            // - recovery anchors (currentTradeID, _activeTradeId, _currentExitOco,
            //   _activeEntrySignal, _currentEntryTag, _ocoCounter)
            // - live position snapshot (entryPositionSide, entryFillTime,
            //   entryFillPrice, entryQuantity, entryPrice, avgEntryPrice,
            //   initialPositionSize, tradeRiskPerContract, initialStopPrice,
            //   initialTradeRisk)
            // - protection state (workingStopPrice, hasWorkingStop,
            //   currentControllerStopPrice)
            // - durable trading blocks (dailyStopHit, dailyLossHit,
            //   unknownExitEconomicsBlock, autoDisabled, _globalKillSwitch)
            // TODO: Do not persist indicator caches or setup-detection scratch state.
            if (lines == null)
                return;

            lines.Add($"SessionAnchor={FormatStateDate(_sessionAnchor)}");
            lines.Add($"TradesThisSession={_tradesThisSession}");
            lines.Add($"ConsecutiveLosses={_consecutiveLosses}");
            lines.Add($"SessionRealizedR={FormatStateDouble(_sessionRealizedR)}");
            lines.Add($"DailyStopHit={dailyStopHit}");
            lines.Add($"DailyLossHit={dailyLossHit}");
            lines.Add($"UnknownExitEconomicsBlock={unknownExitEconomicsBlock}");
            lines.Add($"AutoDisabled={autoDisabled}");
            lines.Add($"GlobalKillSwitch={_globalKillSwitch}");
            lines.Add($"CurrentTradeId={currentTradeID ?? string.Empty}");
            lines.Add($"ActiveTradeId={_activeTradeId ?? string.Empty}");
            lines.Add($"CurrentExitOco={_currentExitOco ?? string.Empty}");
            lines.Add($"ActiveEntrySignal={_activeEntrySignal ?? string.Empty}");
            lines.Add($"CurrentEntryTag={_currentEntryTag ?? string.Empty}");
            lines.Add($"OcoCounter={_ocoCounter}");
            lines.Add($"EntryPositionSide={entryPositionSide}");
            lines.Add($"EntryFillTime={FormatStateDate(entryFillTime)}");
            lines.Add($"EntryFillPrice={FormatStateDouble(entryFillPrice)}");
            lines.Add($"EntryQuantity={entryQuantity}");
            lines.Add($"EntryPrice={FormatStateDouble(entryPrice)}");
            lines.Add($"AvgEntryPrice={FormatStateDouble(avgEntryPrice)}");
            lines.Add($"InitialPositionSize={initialPositionSize}");
            lines.Add($"TradeRiskPerContract={FormatStateDouble(tradeRiskPerContract)}");
            lines.Add($"InitialStopPrice={FormatStateDouble(initialStopPrice)}");
            lines.Add($"InitialTradeRisk={FormatStateDouble(initialTradeRisk)}");
            lines.Add($"WorkingStopPrice={FormatStateDouble(workingStopPrice)}");
            lines.Add($"HasWorkingStop={hasWorkingStop}");
            lines.Add($"CurrentControllerStopPrice={FormatStateDouble(currentControllerStopPrice)}");
            lines.Add($"PersistedTradeId={_persistedTradeId ?? string.Empty}");
            lines.Add($"CountedTradeSessionId={_countedTradeSessionId ?? string.Empty}");
            lines.Add($"TrackedFillQuantity={_trackedFillQuantity}");
        }

        private void AppendTradeScopedStateSnapshotScaffold(ICollection<string> lines)
        {
            // TODO: Capture only trade-local flags that must be reconstructed before
            // unmanaged recovery resumes:
            // - tradeOpen, controllerStopPlaced, _exitState, _hasWorkingEntry,
            //   isFinalizingTrade, suppressAllOrderSubmissions,
            //   stateRestorationInProgress
            // TODO: Keep this separate from durable session/risk gates so
            // ResetTradeState can clear trade state without wiping session controls.
            if (lines == null)
                return;

            lines.Add($"TradeOpen={tradeOpen}");
            lines.Add($"ControllerStopPlaced={controllerStopPlaced}");
            lines.Add($"ProtectiveCoverageAmbiguous={_protectiveCoverageAmbiguous}");
            lines.Add($"ProtectiveCoverageDisposition={_protectiveCoverageDisposition ?? string.Empty}");
            lines.Add($"RecoveryResolution={_recoveryResolution ?? string.Empty}");
            lines.Add($"OrphanedOrdersScanComplete={_orphanedOrdersScanComplete}");
            lines.Add($"OrphanAdoptionPending={_orphanAdoptionPending}");
            lines.Add($"ExplicitCoverageLossPending={_explicitCoverageLossPending}");
            lines.Add($"ProtectiveReplacePending={_protectiveReplacePending}");
            lines.Add($"ProtectiveReplaceFailurePending={_protectiveReplaceFailurePending}");
            lines.Add($"ProtectiveReplaceRejected={_protectiveReplaceRejected}");
            lines.Add($"ProtectiveReplaceDisposition={_protectiveReplaceDisposition ?? string.Empty}");
            lines.Add($"ProtectiveReplaceContext={_protectiveReplaceContext ?? string.Empty}");
            lines.Add($"ProtectiveReplaceReason={_protectiveReplaceReason ?? string.Empty}");
            lines.Add($"ProtectiveReplaceSourceOrderId={_protectiveReplaceSourceOrderId ?? string.Empty}");
            lines.Add($"ProtectiveReplaceTargetOrderId={_protectiveReplaceTargetOrderId ?? string.Empty}");
            lines.Add($"ProtectiveReplaceOco={_protectiveReplaceOco ?? string.Empty}");
            lines.Add($"ProtectiveReplaceStartedAtUtc={FormatStateDate(_protectiveReplaceStartedAtUtc)}");
            lines.Add($"AdoptDeferPending={_adoptDeferPending}");
            lines.Add($"AdoptDeferReason={_adoptDeferReason ?? string.Empty}");
            lines.Add($"LastStopStateChangeAtUtc={FormatStateDate(_lastStopStateChangeAt)}");
            lines.Add($"CoverageGraceUntilUtc={FormatStateDate(_coverageGraceUntil)}");
            lines.Add($"StopSubmitInFlight={_stopSubmitInFlight}");
            lines.Add($"StopSubmissionPending={_stopSubmissionPending}");
            lines.Add($"LastStopSubmitAtUtc={FormatStateDate(_lastStopSubmitAtUtc)}");
            lines.Add($"LastStopSubmissionAtUtc={FormatStateDate(_lastStopSubmissionAtUtc)}");
            lines.Add($"ExitState={_exitState}");
            lines.Add($"FlattenRejectPending={_flattenRejectPending}");
            lines.Add($"StaleChildCleanupPending={_staleChildCleanupPending}");
            lines.Add($"FinalCancelSweepDisposition={_finalCancelSweepDisposition ?? string.Empty}");
            lines.Add($"PreservedProtectiveOrderCount={_preservedProtectiveOrderCount}");
            lines.Add($"HasWorkingEntry={_hasWorkingEntry}");
            lines.Add($"IsFinalizingTrade={isFinalizingTrade}");
            lines.Add($"SuppressAllOrderSubmissions={suppressAllOrderSubmissions}");
            lines.Add($"StateRestorationInProgress={stateRestorationInProgress}");
        }

        private void RestoreDurableStateScaffold(IReadOnlyDictionary<string, string> stateEntries)
        {
            // TODO: Parse documented durable keys first, then apply
            // backward-compatible defaults for older state versions.
            // TODO: Rebuild _activeTradeId/_currentExitOco/_activeEntrySignal before
            // any ExitController recovery path runs.
            // TODO: Only enable restoration flow if broker/account state confirms a
            // live position that still needs protection.
            if (stateEntries == null)
                return;

            _sessionAnchor = ParseStateDate(stateEntries, "SessionAnchor");
            _tradesThisSession = ParseStateInt(stateEntries, "TradesThisSession", _tradesThisSession);
            _consecutiveLosses = ParseStateInt(stateEntries, "ConsecutiveLosses", _consecutiveLosses);
            _sessionRealizedR = ParseStateDouble(stateEntries, "SessionRealizedR", _sessionRealizedR);
            dailyStopHit = ParseStateBool(stateEntries, "DailyStopHit", dailyStopHit);
            dailyLossHit = ParseStateBool(stateEntries, "DailyLossHit", dailyLossHit);
            unknownExitEconomicsBlock = ParseStateBool(stateEntries, "UnknownExitEconomicsBlock", unknownExitEconomicsBlock);
            autoDisabled = ParseStateBool(stateEntries, "AutoDisabled", autoDisabled);
            _globalKillSwitch = ParseStateBool(stateEntries, "GlobalKillSwitch", _globalKillSwitch);
            currentTradeID = ParseStateString(stateEntries, "CurrentTradeId", currentTradeID);
            _activeTradeId = ParseStateString(stateEntries, "ActiveTradeId", _activeTradeId);
            _currentExitOco = ParseStateString(stateEntries, "CurrentExitOco", _currentExitOco);
            _activeEntrySignal = ParseStateString(stateEntries, "ActiveEntrySignal", _activeEntrySignal);
            _currentEntryTag = ParseStateString(stateEntries, "CurrentEntryTag", _currentEntryTag);
            _ocoCounter = ParseStateInt(stateEntries, "OcoCounter", _ocoCounter);
            entryPositionSide = ParseStateEnum(stateEntries, "EntryPositionSide", entryPositionSide);
            entryFillTime = ParseStateDate(stateEntries, "EntryFillTime");
            entryFillPrice = ParseStateDouble(stateEntries, "EntryFillPrice", entryFillPrice);
            entryQuantity = ParseStateInt(stateEntries, "EntryQuantity", entryQuantity);
            entryPrice = ParseStateDouble(stateEntries, "EntryPrice", entryPrice);
            avgEntryPrice = ParseStateDouble(stateEntries, "AvgEntryPrice", avgEntryPrice);
            initialPositionSize = ParseStateInt(stateEntries, "InitialPositionSize", initialPositionSize);
            tradeRiskPerContract = ParseStateDouble(stateEntries, "TradeRiskPerContract", tradeRiskPerContract);
            initialStopPrice = ParseStateDouble(stateEntries, "InitialStopPrice", initialStopPrice);
            initialTradeRisk = ParseStateDouble(stateEntries, "InitialTradeRisk", initialTradeRisk);
            workingStopPrice = ParseStateDouble(stateEntries, "WorkingStopPrice", workingStopPrice);
            hasWorkingStop = ParseStateBool(stateEntries, "HasWorkingStop", hasWorkingStop);
            currentControllerStopPrice = ParseStateDouble(stateEntries, "CurrentControllerStopPrice", currentControllerStopPrice);
            _persistedTradeId = ParseStateString(stateEntries, "PersistedTradeId", _persistedTradeId);
            _countedTradeSessionId = ParseStateString(stateEntries, "CountedTradeSessionId", _countedTradeSessionId);
            _trackedFillQuantity = ParseStateInt(stateEntries, "TrackedFillQuantity", _trackedFillQuantity);

            if (string.IsNullOrEmpty(currentTradeID) && !string.IsNullOrEmpty(_persistedTradeId))
                currentTradeID = _persistedTradeId;

            EnsureActiveTradeIdFromCurrentTradeId("RestoreDurableState");
        }

        private void RestoreTradeScopedStateScaffold(IReadOnlyDictionary<string, string> stateEntries)
        {
            // TODO: Restore only flags that are safe to trust before
            // OnOrderUpdate/OnPositionUpdate reconcile live state.
            // TODO: Leave setup-state ownership in ResetSetupState(reason) to avoid
            // cross-session contamination.
            // TODO: Avoid reviving stale working-order assumptions without broker
            // confirmation.
            if (stateEntries == null)
                return;

            tradeOpen = ParseStateBool(stateEntries, "TradeOpen", tradeOpen);
            controllerStopPlaced = ParseStateBool(stateEntries, "ControllerStopPlaced", controllerStopPlaced);
            _protectiveCoverageAmbiguous = ParseStateBool(
                stateEntries,
                "ProtectiveCoverageAmbiguous",
                _protectiveCoverageAmbiguous);
            _protectiveCoverageDisposition = ParseStateString(
                stateEntries,
                "ProtectiveCoverageDisposition",
                _protectiveCoverageDisposition);
            _recoveryResolution = ParseStateString(
                stateEntries,
                "RecoveryResolution",
                _recoveryResolution);
            _orphanedOrdersScanComplete = ParseStateBool(
                stateEntries,
                "OrphanedOrdersScanComplete",
                _orphanedOrdersScanComplete);
            _orphanAdoptionPending = ParseStateBool(
                stateEntries,
                "OrphanAdoptionPending",
                _orphanAdoptionPending);
            _explicitCoverageLossPending = ParseStateBool(
                stateEntries,
                "ExplicitCoverageLossPending",
                _explicitCoverageLossPending);
            _protectiveReplacePending = ParseStateBool(
                stateEntries,
                "ProtectiveReplacePending",
                _protectiveReplacePending);
            _protectiveReplaceFailurePending = ParseStateBool(
                stateEntries,
                "ProtectiveReplaceFailurePending",
                _protectiveReplaceFailurePending);
            _protectiveReplaceRejected = ParseStateBool(
                stateEntries,
                "ProtectiveReplaceRejected",
                _protectiveReplaceRejected);
            _protectiveReplaceDisposition = ParseStateString(
                stateEntries,
                "ProtectiveReplaceDisposition",
                _protectiveReplaceDisposition);
            _protectiveReplaceContext = ParseStateString(
                stateEntries,
                "ProtectiveReplaceContext",
                _protectiveReplaceContext);
            _protectiveReplaceReason = ParseStateString(
                stateEntries,
                "ProtectiveReplaceReason",
                _protectiveReplaceReason);
            _protectiveReplaceSourceOrderId = ParseStateString(
                stateEntries,
                "ProtectiveReplaceSourceOrderId",
                _protectiveReplaceSourceOrderId);
            _protectiveReplaceTargetOrderId = ParseStateString(
                stateEntries,
                "ProtectiveReplaceTargetOrderId",
                _protectiveReplaceTargetOrderId);
            _protectiveReplaceOco = ParseStateString(
                stateEntries,
                "ProtectiveReplaceOco",
                _protectiveReplaceOco);
            _protectiveReplaceStartedAtUtc = ParseStateDate(stateEntries, "ProtectiveReplaceStartedAtUtc");
            _adoptDeferPending = ParseStateBool(
                stateEntries,
                "AdoptDeferPending",
                _adoptDeferPending);
            _adoptDeferReason = ParseStateString(
                stateEntries,
                "AdoptDeferReason",
                _adoptDeferReason);
            _lastStopStateChangeAt = ParseStateDate(stateEntries, "LastStopStateChangeAtUtc");
            _coverageGraceUntil = ParseStateDate(stateEntries, "CoverageGraceUntilUtc");
            _stopSubmitInFlight = ParseStateBool(stateEntries, "StopSubmitInFlight", _stopSubmitInFlight);
            _stopSubmissionPending = ParseStateBool(
                stateEntries,
                "StopSubmissionPending",
                _stopSubmissionPending);
            _lastStopSubmitAtUtc = ParseStateDate(stateEntries, "LastStopSubmitAtUtc");
            _lastStopSubmissionAtUtc = ParseStateDate(stateEntries, "LastStopSubmissionAtUtc");
            _exitState = ParseStateEnum(stateEntries, "ExitState", _exitState);
            _flattenRejectPending = ParseStateBool(
                stateEntries,
                "FlattenRejectPending",
                _flattenRejectPending);
            _staleChildCleanupPending = ParseStateBool(
                stateEntries,
                "StaleChildCleanupPending",
                _staleChildCleanupPending);
            _finalCancelSweepDisposition = ParseStateString(
                stateEntries,
                "FinalCancelSweepDisposition",
                _finalCancelSweepDisposition);
            _preservedProtectiveOrderCount = ParseStateInt(
                stateEntries,
                "PreservedProtectiveOrderCount",
                _preservedProtectiveOrderCount);
            _hasWorkingEntry = ParseStateBool(stateEntries, "HasWorkingEntry", _hasWorkingEntry);
            isFinalizingTrade = ParseStateBool(stateEntries, "IsFinalizingTrade", isFinalizingTrade);
            suppressAllOrderSubmissions = ParseStateBool(
                stateEntries,
                "SuppressAllOrderSubmissions",
                suppressAllOrderSubmissions);
            stateRestorationInProgress = ParseStateBool(
                stateEntries,
                "StateRestorationInProgress",
                stateRestorationInProgress);
            if (string.IsNullOrEmpty(_recoveryResolution) && stateRestorationInProgress)
            {
                if (_protectiveReplacePending)
                    _recoveryResolution = "replace-pending";
                else if (_protectiveReplaceFailurePending)
                    _recoveryResolution = "replace-failed";
                else if (_protectiveReplaceRejected)
                    _recoveryResolution = "replace-rejected";
                else if (_orphanAdoptionPending || _adoptDeferPending || _protectiveCoverageAmbiguous)
                    _recoveryResolution = "compatible-broker-coverage";
                else if (_explicitCoverageLossPending)
                    _recoveryResolution = "restore-missing-stop";
                else if (!string.IsNullOrEmpty(_protectiveCoverageDisposition))
                    _recoveryResolution = _protectiveCoverageDisposition;
                else
                    _recoveryResolution = "restore-live-position";
            }
            if (!_adoptDeferPending)
                _adoptDeferReason = string.Empty;
            if (string.IsNullOrEmpty(_protectiveReplaceDisposition))
            {
                _protectiveReplaceContext = string.Empty;
                _protectiveReplaceReason = string.Empty;
                _protectiveReplaceSourceOrderId = string.Empty;
                _protectiveReplaceTargetOrderId = string.Empty;
                _protectiveReplaceOco = string.Empty;
                _protectiveReplaceStartedAtUtc = DateTime.MinValue;
            }
        }

        private void ResetTradeStateScaffold()
        {
            // TODO: Clear trade-scoped runtime state and recovery anchors for the
            // next trade.
            // TODO: Preserve durable session/risk controls such as
            // _tradesThisSession, _consecutiveLosses, _sessionRealizedR,
            // dailyStopHit, and dailyLossHit.
            // TODO: After trade reset, let ResetSetupState(reason) own the
            // pattern-detection reset so bar-flow semantics stay unchanged.
            _persistedTradeId = string.Empty;
            _countedTradeSessionId = string.Empty;
            _trackedFillQuantity = 0;
            _recoveryResolution = string.Empty;
            stateRestorationInProgress = false;
        }

        private void ResetDurableStateForDisableScaffold()
        {
            // TODO: Use this only for full strategy shutdown/disable flows where
            // persisted restart state must be discarded.
            // TODO: Clearing durable state here is intentionally different from
            // ResetTradeStateScaffold().
            _sessionAnchor = DateTime.MinValue;
            _tradesThisSession = 0;
            _consecutiveLosses = 0;
            _sessionRealizedR = 0.0;
            dailyStopHit = false;
            dailyLossHit = false;
            unknownExitEconomicsBlock = false;
            autoDisabled = false;
            _globalKillSwitch = false;
            currentTradeID = string.Empty;
            _activeTradeId = string.Empty;
            _currentExitOco = string.Empty;
            _activeEntrySignal = string.Empty;
            _currentEntryTag = string.Empty;
            _ocoCounter = 0;
            entryPositionSide = MarketPosition.Flat;
            entryFillTime = DateTime.MinValue;
            entryFillPrice = 0.0;
            entryQuantity = 0;
            entryPrice = 0.0;
            avgEntryPrice = 0.0;
            initialPositionSize = 0;
            tradeRiskPerContract = 0.0;
            initialStopPrice = 0.0;
            initialTradeRisk = 0.0;
            workingStopPrice = 0.0;
            hasWorkingStop = false;
            currentControllerStopPrice = 0.0;
            _lastStopStateChangeAt = DateTime.MinValue;
            _coverageGraceUntil = DateTime.MinValue;
            _stopSubmitInFlight = false;
            _stopSubmissionPending = false;
            _lastStopSubmitAtUtc = DateTime.MinValue;
            _lastStopSubmissionAtUtc = DateTime.MinValue;
            _protectiveCoverageAmbiguous = false;
            _protectiveCoverageDisposition = string.Empty;
            _recoveryResolution = string.Empty;
            _persistedTradeId = string.Empty;
            _countedTradeSessionId = string.Empty;
            _trackedFillQuantity = 0;
            stateRestored = false;
        }

        private static string SanitizeStatePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Trim();
        }

        private static string FormatStateDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string FormatStateDate(DateTime value)
        {
            return value == DateTime.MinValue ? string.Empty : value.ToString("O", CultureInfo.InvariantCulture);
        }

        private static bool TryParseStateEntries(IEnumerable<string> lines, out Dictionary<string, string> stateEntries)
        {
            stateEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (lines == null)
                return false;

            foreach (string rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int separatorIndex = rawLine.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                string key = rawLine.Substring(0, separatorIndex).Trim();
                string value = rawLine.Substring(separatorIndex + 1).Trim();
                if (key.Length == 0)
                    continue;

                stateEntries[key] = value;
            }

            return stateEntries.Count > 0;
        }

        private static string ParseStateString(
            IReadOnlyDictionary<string, string> stateEntries,
            string key,
            string defaultValue = "")
        {
            return stateEntries != null && stateEntries.TryGetValue(key, out string value)
                ? value ?? string.Empty
                : defaultValue ?? string.Empty;
        }

        private static bool ParseStateBool(
            IReadOnlyDictionary<string, string> stateEntries,
            string key,
            bool defaultValue)
        {
            if (stateEntries != null
                && stateEntries.TryGetValue(key, out string value)
                && bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private static int ParseStateInt(
            IReadOnlyDictionary<string, string> stateEntries,
            string key,
            int defaultValue)
        {
            if (stateEntries != null
                && stateEntries.TryGetValue(key, out string value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private static double ParseStateDouble(
            IReadOnlyDictionary<string, string> stateEntries,
            string key,
            double defaultValue)
        {
            if (stateEntries != null
                && stateEntries.TryGetValue(key, out string value)
                && double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private static DateTime ParseStateDate(
            IReadOnlyDictionary<string, string> stateEntries,
            string key)
        {
            if (stateEntries != null
                && stateEntries.TryGetValue(key, out string value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }

        private static TEnum ParseStateEnum<TEnum>(
            IReadOnlyDictionary<string, string> stateEntries,
            string key,
            TEnum defaultValue) where TEnum : struct
        {
            if (stateEntries != null
                && stateEntries.TryGetValue(key, out string value)
                && Enum.TryParse(value, true, out TEnum parsed))
            {
                return parsed;
            }

            return defaultValue;
        }
    }
}
