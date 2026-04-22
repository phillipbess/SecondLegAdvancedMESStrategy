"""Source-level scaffold contract tests for exit authority in the runtime port."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.nt8_contract_helpers import iter_source_files, read_doc_file, read_runtime_file, read_strategy_file

FAMILY_ID = "exit_authority_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
CONTROL_LANE_NAME = "SecondLegAdvancedRuntimeControlLane.cs"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class ExitAuthorityContractTests(unittest.TestCase):
    def test_manifest_still_tracks_this_family_until_manifest_ownership_moves(self) -> None:
        family = _family_definition()

        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_changeorder_stays_outside_runtime_port_except_for_control_lane_followup(self) -> None:
        allowed_change_order_files = {CONTROL_LANE_NAME, "SecondLegAdvancedMESStrategy.TransportAdapter.cs"}
        offenders: list[str] = []
        for path in iter_source_files():
            if path.name in allowed_change_order_files:
                continue

            if "ChangeOrder(" in path.read_text(encoding="utf-8"):
                offenders.append(path.name)

        control_lane = read_runtime_file(CONTROL_LANE_NAME)

        self.assertEqual(offenders, [])
        self.assertIn("internal void ChangeUnmanaged(Order order, double newStopPrice, string reason)", control_lane)
        self.assertIn("_strategy.ChangeProtectiveStopBridge(", control_lane)
        self.assertIn('EnsureProtectiveExit(newStopPrice, Math.Abs(_strategy.Position.Quantity), "StopLoss_PrimaryEntry", reason);', control_lane)
        self.assertIn("private void DeferProtectiveEnsure(double stopPx, int qty, string stopTag, string reason, string deferredReason)", control_lane)
        self.assertIn("if (!SubmitProtectiveReplace(", control_lane)
        self.assertIn('"ExitController.ChangeUnmanaged.Replace"', control_lane)
        self.assertIn('DeferProtectiveEnsure(newStopPrice, EffectiveProtectiveQuantity(order.Quantity), order.Name ?? "StopLoss_PrimaryEntry", reason, "DEFERRED_REPLACE");', control_lane)

    def test_protective_exit_requests_route_through_exit_controller_and_host_exit_queue(self) -> None:
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        transport_adapter = read_strategy_file("SecondLegAdvancedMESStrategy.TransportAdapter.cs")
        control_lane = read_runtime_file(CONTROL_LANE_NAME)
        host_contract = read_doc_file("Host_Shell_Contract.md")

        self.assertIn("private ExitController exitCtl;", runtime_host)
        self.assertIn("exitCtl ??= new ExitController(this);", runtime_host)
        self.assertIn("private void EnqueueEnsureProtectiveExit(double price, int quantity, string tag, string reason, int delayMs = 0)", runtime_host)
        self.assertIn('string label = $"ENSURE_PROTECTIVE {reason}";', runtime_host)
        self.assertIn("if (delayMs > 0)", runtime_host)
        self.assertIn("EnqueueExitOp(label, ensureProtective, delayMs);", runtime_host)
        self.assertIn("EnqueueExitOp(label, ensureProtective);", runtime_host)
        self.assertIn("TryBuildProtectiveRetry(reason, out int attempt, out int maxAttempts, out int nextDelayMs, out string nextReason)", runtime_host)
        self.assertIn("exitCtl?.EnsureProtectiveExit(price, quantity, tag, reason);", runtime_host)

        self.assertIn("internal void EnsureProtectiveExit(double stopPx, int qty, string stopTag, string reason)", control_lane)
        self.assertIn("private bool SubmitProtectiveReplace(", control_lane)
        self.assertIn("private void DeferProtectiveEnsure(double stopPx, int qty, string stopTag, string reason, string deferredReason)", control_lane)
        self.assertIn("private string ResolveProtectiveSignalName()", control_lane)
        self.assertIn("_strategy.BeginProtectiveReplaceLineage(existing, freshOco, replaceContext, reason);", control_lane)
        self.assertIn("if (_strategy._protectiveReplacePending)", control_lane)
        self.assertIn('DeferProtectiveEnsure(stopPx, effQty, stopTag, reason, "DEFERRED_REPLACE");', control_lane)
        self.assertIn('_strategy.EnqueueEnsureProtectiveExit(stopPrice, _strategy.entryQuantity, "StopLoss_PrimaryEntry", "EntryFill");', control_lane)
        self.assertIn("_strategy.EnsureProtectiveStopBridge(", control_lane)
        self.assertIn('_strategy.RefreshRuntimeSnapshot("ExitController.EnsureProtectiveExit");', control_lane)
        self.assertIn("_strategy._stopSubmitInFlight = true;", control_lane)
        self.assertIn("_strategy._stopSubmissionPending = true;", control_lane)
        self.assertIn("_strategy.ChangeProtectiveStopBridge(", control_lane)
        self.assertIn("if (changeResult.IsPendingAck)", control_lane)
        self.assertIn('_strategy.SetProtectiveCoverageDisposition("pending-owned", "ExitController.ChangeUnmanaged.PendingAck");', control_lane)
        self.assertIn('_strategy.RefreshRuntimeSnapshot("ExitController.ChangeUnmanaged.PendingAck");', control_lane)
        self.assertIn('_strategy.RefreshRuntimeSnapshot("ExitController.ChangeUnmanaged");', control_lane)
        self.assertIn('"ExitController.EnsureProtectiveExit.SideMismatch"', control_lane)
        self.assertIn('"ExitController.EnsureProtectiveExit.QuantityMismatch"', control_lane)
        self.assertIn("internal void SubmitPrimaryEntry(PlannedEntry plannedEntry)", control_lane)
        self.assertIn("_strategy.SubmitPrimaryEntryBridge(plannedEntry);", control_lane)
        self.assertIn("private TransportResult SubmitPrimaryEntryBridge(PlannedEntry plannedEntry)", transport_adapter)
        self.assertIn("private TransportResult EnsureProtectiveStopBridge(", transport_adapter)
        self.assertIn("private TransportResult ChangeProtectiveStopBridge(Order workingStop, int quantity, double stopPrice)", transport_adapter)
        self.assertIn("Requested = 2,", transport_adapter)
        self.assertIn("internal bool IsPendingAck => Kind == TransportResultKind.Requested;", transport_adapter)
        self.assertIn("return TransportResult.Requested(", transport_adapter)
        self.assertIn("SubmitOrderUnmanaged(", transport_adapter)
        self.assertIn("ChangeOrder(", transport_adapter)
        self.assertNotIn("EnterLongStopMarket(", runtime_host)
        self.assertNotIn("EnterShortStopMarket(", runtime_host)
        self.assertNotIn("SetStopLoss(", runtime_host)
        self.assertIn("OnExecutionUpdate -> tradeManager -> exitCtl -> coverage -> flatten/cancel -> SaveStrategyState/ResetTradeState", host_contract)

    def test_flatten_requests_stay_on_the_exit_control_lane(self) -> None:
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        transport_adapter = read_strategy_file("SecondLegAdvancedMESStrategy.TransportAdapter.cs")
        control_lane = read_runtime_file(CONTROL_LANE_NAME)

        self.assertIn('public void Flatten(string reason = "ExitController")', control_lane)
        self.assertIn("RequestFlatten(reason);", control_lane)
        self.assertIn("public void RequestFlatten(string reason)", control_lane)
        self.assertIn("_strategy.TriggerFlatten(reason);", control_lane)
        self.assertIn("internal void SubmitFlatten(string fromEntrySignal)", control_lane)
        self.assertIn('SubmitFlattenMarket(Math.Abs(_strategy.Position.Quantity), fromEntrySignal, "ExitController");', control_lane)
        self.assertIn(
            "internal SecondLegExitControllerFlattenSubmitResult SubmitFlattenMarket(int qty, string signalName, string reason)",
            control_lane,
        )
        self.assertIn("private void MarkFlattenSubmitFailure(string reason, string signalName, int quantity, string detail)", control_lane)
        self.assertIn('string ownedSignal = !string.IsNullOrEmpty(signalName) ? signalName : ResolveProtectiveSignalName();', control_lane)
        self.assertIn("TransportResult flattenResult = _strategy.SubmitFlattenBridge(ownedSignal, effQty);", control_lane)
        self.assertIn('_strategy.SetFlattenRecoveryState(true, false);', control_lane)
        self.assertIn('_strategy.RefreshRuntimeSnapshot("ExitController.SubmitFlattenMarket.Failed");', control_lane)
        self.assertIn('_strategy.SetFlattenRecoveryState(false, false);', control_lane)
        self.assertIn('[RUNTIME_EXIT_CTL][DROP] flatten submit while flat', control_lane)
        self.assertIn("private TransportResult SubmitFlattenBridge(string fromEntrySignal, int quantity)", transport_adapter)
        self.assertIn("SubmitOrderUnmanaged(0, action, OrderType.Market, quantity, 0, 0, string.Empty, signalName);", transport_adapter)
        self.assertNotIn("ExitLong(", runtime_host)
        self.assertNotIn("ExitShort(", runtime_host)

        self.assertIn("private void TriggerFlatten(string reason)", runtime_host)
        self.assertIn("BeginAtomicFinalization(reason);", runtime_host)
        self.assertIn("_exitState = SecondLegExitFlowState.Flattening;", runtime_host)
        self.assertIn('WriteDebugLog($"[FLATTEN] duplicate ignored | reason={reason}");', runtime_host)

    def test_flatten_reprice_is_child_action_aware_or_explicitly_deferred(self) -> None:
        control_lane = read_runtime_file(CONTROL_LANE_NAME)
        host_contract = read_doc_file("Host_Shell_Contract.md")

        child_action_aware_markers = [
            "child.OrderAction switch",
            "OrderAction.Sell or OrderAction.SellShort",
            "OrderAction.Buy or OrderAction.BuyToCover",
        ]
        has_child_action_reprice = all(marker in control_lane for marker in child_action_aware_markers)

        if has_child_action_reprice:
            for marker in child_action_aware_markers:
                self.assertIn(marker, control_lane)
            return

        self.assertIn("internal void ChangeUnmanaged(Order order, double newStopPrice, string reason)", control_lane)
        self.assertIn("_strategy.ChangeProtectiveStopBridge(", control_lane)
        self.assertIn("if (!SubmitProtectiveReplace(", control_lane)
        self.assertIn('"ExitController.ChangeUnmanaged.Replace"', control_lane)
        self.assertIn("Unmanaged NT8 transport now remains isolated behind a dedicated host transport adapter partial.", host_contract)


if __name__ == "__main__":
    unittest.main()
