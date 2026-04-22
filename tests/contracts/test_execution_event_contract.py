"""Source-level execution identity and restart-lifecycle scaffold checks."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

FAMILY_ID = "execution_event_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
REPO_ROOT = Path(__file__).resolve().parents[2]
HOST_CONTRACT_DOC_PATH = REPO_ROOT / "docs" / "Host_Shell_Contract.md"
PERSISTENCE_SCAFFOLD_PATH = (
    REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.PersistenceScaffold.cs"
)
RUNTIME_HOST_PATH = REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.RuntimeHost.cs"
RUNTIME_SCENARIO_STATE_PATH = (
    REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs"
)
EXECUTION_IDENTITY_PATH = (
    REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.ExecutionIdentityScaffold.cs"
)
ORDER_MAINTENANCE_PATH = (
    REPO_ROOT / "src" / "runtime-core" / "SecondLegAdvancedMESStrategy.OrderMaintenanceScaffold.cs"
)
TRADE_MANAGER_PATH = REPO_ROOT / "src" / "runtime-core" / "SecondLegAdvancedTradeManager.cs"
ORDERS_PATH = REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.Orders.cs"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _method_block(text: str, signature: str) -> str:
    start = text.find(signature)
    if start < 0:
        raise AssertionError(f"Missing signature: {signature}")

    brace_start = text.find("{", start)
    if brace_start < 0:
        raise AssertionError(f"Missing method body for: {signature}")

    depth = 0
    for index in range(brace_start, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[start : index + 1]

    raise AssertionError(f"Unbalanced braces for: {signature}")


class ExecutionEventContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertEqual(family["id"], FAMILY_ID)
        self.assertEqual(family["status"], "implemented")
        self.assertIn("single-source-of-truth", family["goal"])

    def test_execution_lane_keeps_entry_identity_authoritative_and_exit_aware(self) -> None:
        host_contract = _read(HOST_CONTRACT_DOC_PATH)
        runtime_host = _read(RUNTIME_HOST_PATH)
        orders = _read(ORDERS_PATH)
        trade_manager = _read(TRADE_MANAGER_PATH)
        order_maintenance = _read(ORDER_MAINTENANCE_PATH)
        execution_identity = _read(EXECUTION_IDENTITY_PATH)
        lifecycle = _read(REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.StateLifecycle.cs")

        self.assertIn(
            "`OnExecutionUpdate` remains the single source of truth for trade lifecycle state.",
            host_contract,
        )
        self.assertIn('PumpExitOps("OnOrderUpdate");', orders.replace("\r\n", "\n"))
        self.assertIn('PumpExitOps("OnExecutionUpdate.Entry");', orders.replace("\r\n", "\n"))
        self.assertIn('PumpExitOps("OnExecutionUpdate.Flat");', orders.replace("\r\n", "\n"))
        self.assertIn('ValidateStopQuantity("OnOrderUpdate");', orders)
        self.assertIn('bool sessionControlFlattenTriggered = MaybeTriggerSessionControlFlattenOnEntryFill(time, "EntryFill");', orders)
        self.assertIn('ValidateStopQuantity("EntryFill");', orders)
        self.assertIn('ValidateStopQuantity("OnPositionUpdate");', orders)
        self.assertIn("tradeManager?.SubmitPrimaryEntry(_plannedEntry);", orders)
        self.assertIn("private bool HasWorkingPrimaryEntryForActiveSignal()", orders)
        self.assertIn("private bool IsOwnedTradeClosingExecutionOrder(Order order)", orders)
        self.assertIn("private bool CancelWorkingPrimaryEntries(string reason)", orders)
        self.assertIn("private bool IsStrategyProtectiveStopOrder(Order order)", orders)
        self.assertIn('ClearEntrySubmitInFlight("OnOrderUpdate.EntryWorking");', orders)
        self.assertIn('ClearEntrySubmitInFlight($"OnOrderUpdate.Entry{orderState}");', orders)
        self.assertIn('ClearEntrySubmitInFlight("OnExecutionUpdate.EntryFill");', orders)
        self.assertIn("if (_flattenInFlight || isFinalizingTrade)", orders)
        self.assertIn("ResetTradeState();", orders)
        self.assertIn(
            "private void AnchorEntryExecutionState(Execution execution, double fillPrice, string context)",
            runtime_host,
        )
        self.assertIn("private void CountTradeOnFirstEntryFill(Execution execution)", runtime_host)
        self.assertIn("private void SaveStrategyState()", runtime_host)
        self.assertIn("private void ValidateStopQuantity(string context)", runtime_host)
        self.assertIn("private bool MaybeTriggerSessionControlFlattenOnEntryFill(DateTime time, string context)", runtime_host)
        self.assertIn("private void AdvanceRestoreObservation(string context)", runtime_host)
        self.assertIn("private void SetRecoveryResolution(string resolution, string context)", runtime_host)
        self.assertIn("private bool HasPendingRecoveryResolution()", runtime_host)
        self.assertIn("private void RunRuntimeMaintenancePass(string context)", lifecycle)
        self.assertIn("AdvanceRestoreObservation(context);", lifecycle)

        restore_observation = _method_block(runtime_host, "private void AdvanceRestoreObservation(string context)")
        self.assertIn("_explicitCoverageLossPending = false;", restore_observation)
        self.assertIn('SetProtectiveCoverageDisposition("covered-owned", $"{context}.RestoreCovered");', restore_observation)
        self.assertIn('SetRecoveryResolution("covered-owned", $"{context}.RestoreCovered");', restore_observation)
        self.assertIn('SetRecoveryResolution("compatible-broker-coverage", $"{context}.RestoreCompatible");', restore_observation)
        self.assertIn('SetRecoveryResolution("restore-missing-stop", $"{context}.RestoreMissingStop");', restore_observation)
        self.assertIn('SetRecoveryResolution("pending-owned", $"{context}.RestorePending");', restore_observation)

        validate_stop_qty = _method_block(runtime_host, "private void ValidateStopQuantity(string context)")
        self.assertIn("if (liveQty == 0)", validate_stop_qty)
        self.assertIn("if (protectiveQty >= liveQty)", validate_stop_qty)
        self.assertIn("if (IsProtectiveCoveragePending(nowUtc))", validate_stop_qty)
        self.assertIn('RefreshRuntimeSnapshot($"ValidateStopQuantity.{context}.Pending");', validate_stop_qty)
        self.assertIn('EnqueueEnsureProtectiveExit(candidateStop, liveQty, "StopLoss_PrimaryEntry", $"CoverageRepair|{context}");', validate_stop_qty)
        self.assertIn('TriggerFlatten("ProtectiveCoverageMissing");', validate_stop_qty)

        on_execution_update = _method_block(
            orders,
            "protected override void OnExecutionUpdate(",
        )
        on_order_update = _method_block(
            orders,
            "protected override void OnOrderUpdate(",
        )
        on_position_update = _method_block(
            orders,
            "protected override void OnPositionUpdate(",
        )
        self.assertIn("bool preservePendingCoverageDuringReplace =", on_order_update)
        self.assertIn("bool hasActualEntryFill = Math.Max(filled, order.Filled) > 0 || averageFillPrice > 0.0;", on_order_update)
        self.assertIn(
            "&& (_protectiveReplacePending || _stopSubmissionPending || _stopSubmitInFlight);",
            on_order_update,
        )
        self.assertIn("if (!preservePendingCoverageDuringReplace)", on_order_update)
        self.assertIn("if (!hasActualEntryFill", on_order_update)
        self.assertIn("if (orderState != OrderState.Filled)", on_order_update)
        self.assertIn('ValidateStopQuantity("OnOrderUpdate");', on_order_update)
        on_entry_filled_index = on_execution_update.find("exitCtl?.OnEntryFilled(_activeEntrySignal, blendedEntryPrice, entryPositionSide, entryQuantity);")
        validate_index = on_execution_update.find('ValidateStopQuantity("EntryFill");')
        save_index = on_execution_update.find("SaveStrategyState();")
        pump_index = on_execution_update.find('PumpExitOps("OnExecutionUpdate.Entry");')
        clear_submit_index = on_execution_update.find('ClearEntrySubmitInFlight("OnExecutionUpdate.EntryFill");')
        self.assertGreaterEqual(on_entry_filled_index, 0)
        self.assertGreaterEqual(clear_submit_index, 0)
        self.assertGreater(validate_index, clear_submit_index)
        self.assertGreater(validate_index, on_entry_filled_index)
        self.assertGreater(save_index, validate_index)
        self.assertGreater(pump_index, validate_index)
        self.assertIn("&& IsOwnedTradeClosingExecutionOrder(order)", on_execution_update)
        self.assertIn("if (!_flattenInFlight && !isFinalizingTrade)", on_execution_update)

        persistence = _read(PERSISTENCE_SCAFFOLD_PATH)
        self.assertIn('"ExplicitCoverageLossPending",', persistence)
        self.assertIn('"RecoveryResolution",', persistence)
        self.assertIn('"FlattenRejectPending",', persistence)
        self.assertIn('"StaleChildCleanupPending",', persistence)
        self.assertIn('"FinalCancelSweepDisposition",', persistence)
        self.assertIn('"PreservedProtectiveOrderCount",', persistence)
        self.assertIn("_hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal()", on_execution_update)
        self.assertIn("_entryPending = _hasWorkingEntry;", on_execution_update)
        self.assertIn("_tradeJustClosed = false;", on_execution_update)

        owned_trade_closing_predicate = _method_block(orders, "private bool IsOwnedTradeClosingExecutionOrder(Order order)")
        self.assertIn("return IsStrategyProtectiveStopOrder(order) || IsStrategyFlattenOrder(order);", owned_trade_closing_predicate)

        self.assertIn("_hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();", on_position_update)
        self.assertIn("_entryPending = _hasWorkingEntry;", on_position_update)
        self.assertIn("bool hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();", on_position_update)
        self.assertIn("&& !hasWorkingEntry", on_position_update)
        self.assertIn("_tradeJustClosed = false;", on_position_update)
        self.assertIn("if (!_flattenInFlight && !isFinalizingTrade)", on_position_update)

        on_execution_update_trade_manager = _method_block(
            trade_manager,
            "public void OnExecutionUpdate(Execution execution, double price)",
        )
        self.assertIn("if (strategy.TradeManagerIsPrimaryEntryOrder(execution.Order))", on_execution_update_trade_manager)
        self.assertIn("EntryPrice = strategy.Position.AveragePrice > 0 ? strategy.Position.AveragePrice : price;", on_execution_update_trade_manager)
        self.assertIn("EntryTime = execution.Time;", on_execution_update_trade_manager)
        self.assertIn("EntryDirection = execution.MarketPosition;", on_execution_update_trade_manager)

        primary_entry_predicate = _method_block(
            trade_manager,
            "internal bool TradeManagerIsPrimaryEntryOrder(Order order)",
        )
        self.assertIn("return IsPrimaryEntryOrder(order);", primary_entry_predicate)
        self.assertIn("private static bool IsPrimaryEntrySideAction(OrderAction action)", execution_identity)
        self.assertIn("private bool MatchesKnownPrimaryEntrySignal(string signal)", execution_identity)
        owned_stop_predicate = _method_block(orders, "private bool IsStrategyProtectiveStopOrder(Order order)")
        self.assertIn("if (!SecondLegOrderExtensions.IsProtectiveStop(order))", owned_stop_predicate)
        self.assertIn("if (TryGetOwnedProtectiveSignal(order, out string ownedSignal))", owned_stop_predicate)
        self.assertIn("return MatchesOwnedPrimaryEntrySignal(ownedSignal);", owned_stop_predicate)
        self.assertIn("return MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);", owned_stop_predicate)
        self.assertNotIn('name.IndexOf("stop", StringComparison.OrdinalIgnoreCase)', owned_stop_predicate)

        working_exit_count = _method_block(
            order_maintenance,
            "public int WorkingExitCount(OrderMaintenanceState state)",
        )
        self.assertIn(
            "bool isEntryOrder = _host.IsPrimaryEntryOrder != null && _host.IsPrimaryEntryOrder(order);",
            working_exit_count,
        )
        self.assertIn("if (!isEntryOrder)", working_exit_count)

    def test_trade_anchor_recovery_contract_spans_fill_and_restore_seams(self) -> None:
        host_contract = _read(HOST_CONTRACT_DOC_PATH)
        runtime_host = _read(RUNTIME_HOST_PATH)
        persistence = _read(PERSISTENCE_SCAFFOLD_PATH)
        execution_identity = _read(EXECUTION_IDENTITY_PATH)

        self.assertIn("- `currentTradeID`", host_contract)
        self.assertIn("- `entryFillTime`", host_contract)
        self.assertIn("- `SaveStrategyState()`", host_contract)
        self.assertIn("private void EnsureActiveTradeIdFromCurrentTradeId(string reason)", execution_identity)
        self.assertIn("private void CapturePersistedTradeIdentity(string reason)", execution_identity)

        anchor_execution = _method_block(
            runtime_host,
            "private void AnchorEntryExecutionState(Execution execution, double fillPrice, string context)",
        )
        self.assertIn('EnsureActiveTradeIdFromCurrentTradeId(context ?? "AnchorEntryExecutionState");', anchor_execution)
        self.assertIn("entryFillPrice = fillPrice;", anchor_execution)
        self.assertIn("entryPrice = fillPrice;", anchor_execution)
        self.assertIn("avgEntryPrice = fillPrice;", anchor_execution)
        self.assertIn("entryFillTime = execution?.Time ?? DateTime.MinValue;", anchor_execution)

        save_strategy_state = _method_block(runtime_host, "private void SaveStrategyState()")
        self.assertIn('CapturePersistedTradeIdentity("SaveStrategyState");', save_strategy_state)

        restore_strategy_state = _method_block(runtime_host, "private void RestoreStrategyState()")
        self.assertIn("SetReconnectObservationState(false, Math.Abs(Position.Quantity), Position.AveragePrice);", restore_strategy_state)
        self.assertIn('SetStateRestorationInProgress(true, "RestoreStrategyState.LivePosition");', restore_strategy_state)
        self.assertIn("SetReconnectObservationState(true, 0, 0.0);", restore_strategy_state)
        self.assertIn('ClearRestoreFlatPresentationState("RestoreStrategyState.Flat");', restore_strategy_state)

        restore_observation = _method_block(runtime_host, "private void AdvanceRestoreObservation(string context)")
        self.assertIn("if (!stateRestorationInProgress)", restore_observation)
        self.assertIn('RebuildWorkingOrderTruthFromBroker($"{context}.RestoreObservation");', restore_observation)
        self.assertIn("SetReconnectObservationState(true, liveQty, Position.AveragePrice);", restore_observation)
        self.assertIn("bool hasOwnedCoverage = HasWorkingProtectiveCoverage() && SumWorkingProtectiveCoverageQty() >= liveQty;", restore_observation)
        self.assertIn("HasWorkingProtectiveCoverage()", restore_observation)
        self.assertIn('ReconcileCompatibleBrokerCoverageForRecovery($"{context}.RestoreObservation", liveQty)', restore_observation)
        self.assertIn('SetRecoveryHoldState(true, false, false, false, true, "compatible-broker-coverage");', restore_observation)
        self.assertIn('SetProtectiveCoverageDisposition("pending-owned", $"{context}.RestorePending");', restore_observation)
        self.assertIn('ClearRestoreFlatPresentationState($"{context}.RestoreFlat");', restore_observation)

        preserve_restored_recovery = _method_block(runtime_host, "private void PreserveRestoredRecoveryHoldState()")
        self.assertIn("if (HasPendingRecoveryResolution())", preserve_restored_recovery)
        self.assertIn("_recoveryResolution = string.Empty;", preserve_restored_recovery)

        reset_trade_state = _method_block(runtime_host, "private void ResetTradeState()")
        for marker in (
            "currentTradeID = string.Empty;",
            "_activeTradeId = string.Empty;",
            "_activeEntrySignal = string.Empty;",
            "_currentEntryTag = string.Empty;",
            "_currentExitOco = string.Empty;",
        ):
            self.assertIn(marker, reset_trade_state)

        self.assertIn('"CurrentTradeId",', persistence)
        self.assertIn('"ActiveTradeId",', persistence)
        self.assertIn('"CurrentExitOco",', persistence)
        self.assertIn('"ActiveEntrySignal",', persistence)
        self.assertIn('"CurrentEntryTag",', persistence)
        self.assertIn('"ProtectiveCoverageAmbiguous",', persistence)
        self.assertIn('"ProtectiveCoverageDisposition",', persistence)
        self.assertIn('"OrphanedOrdersScanComplete",', persistence)
        self.assertIn('"OrphanAdoptionPending",', persistence)
        self.assertIn('"ProtectiveReplacePending",', persistence)
        self.assertIn('"ProtectiveReplaceFailurePending",', persistence)
        self.assertIn('"ProtectiveReplaceRejected",', persistence)
        self.assertIn('"ProtectiveReplaceDisposition",', persistence)
        self.assertIn('"ProtectiveReplaceContext",', persistence)
        self.assertIn('"ProtectiveReplaceReason",', persistence)
        self.assertIn('"ProtectiveReplaceSourceOrderId",', persistence)
        self.assertIn('"ProtectiveReplaceTargetOrderId",', persistence)
        self.assertIn('"ProtectiveReplaceOco",', persistence)
        self.assertIn('"ProtectiveReplaceStartedAtUtc",', persistence)
        self.assertIn('"AdoptDeferPending",', persistence)
        self.assertIn('"AdoptDeferReason",', persistence)
        self.assertIn('"LastStopStateChangeAtUtc",', persistence)
        self.assertIn('"CoverageGraceUntilUtc",', persistence)
        self.assertIn('"StopSubmitInFlight",', persistence)
        self.assertIn('"StopSubmissionPending",', persistence)
        self.assertIn('"LastStopSubmitAtUtc",', persistence)
        self.assertIn('"LastStopSubmissionAtUtc",', persistence)
        self.assertIn(
            "// - recovery anchors (currentTradeID, _activeTradeId, _currentExitOco,",
            persistence,
        )
        self.assertIn(
            "// TODO: Rebuild _activeTradeId/_currentExitOco/_activeEntrySignal before",
            persistence,
        )

    def test_flat_restart_contract_preserves_durable_blocks_and_clears_transients(self) -> None:
        persistence = _read(PERSISTENCE_SCAFFOLD_PATH)
        runtime_host = _read(RUNTIME_HOST_PATH)
        runtime_scenario_state = _read(RUNTIME_SCENARIO_STATE_PATH)
        execution_identity = _read(EXECUTION_IDENTITY_PATH)

        reset_trade_state = _method_block(runtime_host, "private void ResetTradeState()")
        for marker in (
            "entryPositionSide = MarketPosition.Flat;",
            "entryFillTime = DateTime.MinValue;",
            "entryFillPrice = 0.0;",
            "entryQuantity = 0;",
            "entryPrice = 0.0;",
            "avgEntryPrice = 0.0;",
            "controllerStopPlaced = false;",
            "tradeOpen = false;",
            "_lastStopStateChangeAt = DateTime.MinValue;",
            "_coverageGraceUntil = DateTime.MinValue;",
            "_stopSubmitInFlight = false;",
            "_stopSubmissionPending = false;",
            "_lastStopSubmitAtUtc = DateTime.MinValue;",
            "_lastStopSubmissionAtUtc = DateTime.MinValue;",
            'ClearEntrySubmitInFlight("ResetTradeState");',
            "_flattenInFlight = false;",
            "_submissionRetryCorrelationBySignal.Clear();",
            "_submissionAuthorityEmitOnce.Clear();",
            "ResetRuntimeScenarioState();",
            "_exitState = SecondLegExitFlowState.Flat;",
        ):
            self.assertIn(marker, reset_trade_state)

        self.assertIn("private void ClearEntrySubmitInFlight(string reason)", execution_identity)
        self.assertIn("private void HandleFlatRealtimeRestartScaffold()", execution_identity)
        self.assertIn("bool preserveDurableSafetyLockout = autoDisabled || _globalKillSwitch;", execution_identity)
        self.assertIn('WriteDebugLog("[STATE_REALTIME] Preserving durable safety lockout on flat restart");', execution_identity)
        self.assertIn('ClearEntrySubmitInFlight("State.Realtime.FlatRestart");', execution_identity)

        for durable_marker in (
            "dailyStopHit = false;",
            "dailyLossHit = false;",
            "unknownExitEconomicsBlock = false;",
            "autoDisabled = false;",
            "_globalKillSwitch = false;",
        ):
            self.assertNotIn(durable_marker, reset_trade_state)

        self.assertIn('"DailyStopHit",', persistence)
        self.assertIn('"DailyLossHit",', persistence)
        self.assertIn('"UnknownExitEconomicsBlock",', persistence)
        self.assertIn(
            "// TODO: Preserve durable session/risk controls such as",
            persistence,
        )
        self.assertIn(
            "// dailyStopHit, and dailyLossHit.",
            persistence,
        )

        runtime_scenario_reset = _method_block(
            runtime_scenario_state,
            "private void ResetRuntimeScenarioState()",
        )
        self.assertIn("_flatResetDeferred = false;", runtime_scenario_reset)
        self.assertIn("_protectiveCoverageAmbiguous = false;", runtime_scenario_reset)
        self.assertIn("_trackedFillQuantity = 0;", runtime_scenario_reset)
        self.assertIn(
            "// `_countedTradeSessionId` is a trade-identity field and stays under",
            runtime_scenario_reset,
        )


if __name__ == "__main__":
    unittest.main()
