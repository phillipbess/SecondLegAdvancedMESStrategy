"""Donor-inspired runtime scaffold contract tests for the M1 port boundary."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

FAMILY_ID = "runtime_scaffold_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
REPO_ROOT = Path(__file__).resolve().parents[2]
RUNTIME_HOST_PATH = REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.RuntimeHost.cs"
TRANSPORT_ADAPTER_PATH = REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.TransportAdapter.cs"
STATE_LIFECYCLE_PATH = REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.StateLifecycle.cs"
CONTROL_LANE_PATH = REPO_ROOT / "src" / "runtime-core" / "SecondLegAdvancedRuntimeControlLane.cs"
SUBMISSION_AUTHORITY_PATH = (
    REPO_ROOT / "src" / "runtime-core" / "SecondLegAdvancedMESStrategy.SubmissionAuthorityScaffold.cs"
)
HOST_CONTRACT_DOC_PATH = REPO_ROOT / "src" / "runtime-core" / "SUBMISSION_AUTHORITY_HOST_CONTRACT.md"
ORDER_MAINTENANCE_PATH = (
    REPO_ROOT / "src" / "runtime-core" / "SecondLegAdvancedMESStrategy.OrderMaintenanceScaffold.cs"
)


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


class RuntimeScaffoldContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_host_shell_keeps_lifecycle_and_trade_reset_scaffolding_explicit(self) -> None:
        runtime_host = _read(RUNTIME_HOST_PATH)
        transport_adapter = _read(TRANSPORT_ADAPTER_PATH)
        lifecycle = _read(STATE_LIFECYCLE_PATH)

        self.assertIn("private void InitializeRuntimeCoreIfNeeded()", runtime_host)
        self.assertIn("private void TriggerFlatten(string reason)", runtime_host)
        self.assertIn("private void SetStateRestorationInProgress(bool enabled, string context)", runtime_host)
        self.assertIn("private void ResetTradeState()", runtime_host)
        self.assertIn("private void SaveStrategyState()", runtime_host)
        self.assertIn("private void RestoreStrategyState()", runtime_host)
        self.assertIn("if (!ShouldPersistStrategyState())", runtime_host)
        self.assertIn("AppendStrategyStateHeaderScaffold(stateLines);", runtime_host)
        self.assertIn("AppendDurableStateSnapshotScaffold(stateLines);", runtime_host)
        self.assertIn("AppendTradeScopedStateSnapshotScaffold(stateLines);", runtime_host)
        self.assertIn("TryParseStateEntries(File.ReadAllLines(stateFilePath), out Dictionary<string, string> stateEntries)", runtime_host)
        self.assertIn("RestoreDurableStateScaffold(stateEntries);", runtime_host)
        self.assertIn("RestoreTradeScopedStateScaffold(stateEntries);", runtime_host)
        self.assertIn("currentTradeID = string.Empty;", runtime_host)
        self.assertIn("_activeTradeId = string.Empty;", runtime_host)
        self.assertIn("_currentExitOco = string.Empty;", runtime_host)
        self.assertIn("_activeEntrySignal = string.Empty;", runtime_host)
        self.assertIn("_currentEntryTag = string.Empty;", runtime_host)
        self.assertIn("controllerStopPlaced = false;", runtime_host)
        self.assertIn("tradeOpen = false;", runtime_host)
        self.assertIn("_exitState = ExitFlowState.Flat;", runtime_host)
        self.assertNotIn("EnterLongStopMarket(", runtime_host)
        self.assertNotIn("EnterShortStopMarket(", runtime_host)
        self.assertNotIn("SetStopLoss(", runtime_host)
        self.assertNotIn("ExitLong(", runtime_host)
        self.assertNotIn("ExitShort(", runtime_host)

        self.assertIn("private TransportResult SubmitPrimaryEntryBridge(PlannedEntry plannedEntry)", transport_adapter)
        self.assertIn("private TransportResult EnsureProtectiveStopBridge(", transport_adapter)
        self.assertIn("private TransportResult ChangeProtectiveStopBridge(Order workingStop, int quantity, double stopPrice)", transport_adapter)
        self.assertIn("private TransportResult SubmitFlattenBridge(string fromEntrySignal, int quantity)", transport_adapter)
        self.assertIn("SubmitOrderUnmanaged(", transport_adapter)
        self.assertIn("ChangeOrder(", transport_adapter)
        self.assertNotIn("SetStopLoss(", transport_adapter)
        self.assertNotIn("EnterLongStopMarket(", transport_adapter)
        self.assertNotIn("EnterShortStopMarket(", transport_adapter)
        self.assertNotIn("ExitLong(", transport_adapter)
        self.assertNotIn("ExitShort(", transport_adapter)

        self.assertIn("protected override void OnStateChange()", lifecycle)
        self.assertIn("else if (State == State.Configure)", lifecycle)
        self.assertIn("_hostShellReady = true;", lifecycle)
        self.assertIn("InitializeStatePersistencePath();", lifecycle)
        self.assertIn("RestoreStrategyState();", lifecycle)
        self.assertIn("stateRestored = true;", lifecycle)
        self.assertIn("protected override void OnBarUpdate()", lifecycle)
        self.assertIn("if (!_hostShellReady)", lifecycle)

    def test_runtime_control_lane_stays_centralized_and_transport_bridged(self) -> None:
        control_lane = _read(CONTROL_LANE_PATH)

        self.assertIn("private sealed class RuntimeTradeManager", control_lane)
        self.assertIn("private sealed class ExitController", control_lane)
        self.assertIn("internal void EnsureProtectiveExit(double stopPx, int qty, string stopTag, string reason)", control_lane)
        self.assertIn("internal void ChangeUnmanaged(Order order, double newStopPrice, string reason)", control_lane)
        self.assertIn("internal FlattenSubmitResult SubmitFlattenMarket(int qty, string signalName, string reason)", control_lane)
        self.assertIn("_strategy.EnqueueEnsureProtectiveExit(stopPrice, _strategy.entryQuantity, \"StopLoss_PrimaryEntry\", \"EntryFill\");", control_lane)
        self.assertIn("_strategy.SubmitPrimaryEntryBridge(plannedEntry);", control_lane)
        self.assertIn("_strategy.EnsureProtectiveStopBridge(", control_lane)
        self.assertIn("_strategy.ChangeProtectiveStopBridge(", control_lane)
        self.assertIn('SubmitFlattenMarket(Math.Abs(_strategy.Position.Quantity), fromEntrySignal, "ExitController");', control_lane)
        self.assertIn("_strategy.EnqueueExitOp(", control_lane)

    def test_submission_authority_scaffold_stays_non_activating_until_host_wiring_lands(self) -> None:
        submission_authority = _read(SUBMISSION_AUTHORITY_PATH)
        host_contract_doc = _read(HOST_CONTRACT_DOC_PATH)

        self.assertIn("public bool IsActivationReady", submission_authority)
        self.assertIn("DescribeMissingActivationHooks()", submission_authority)
        self.assertIn("if (!_host.IsActivationReady)", submission_authority)
        self.assertIn("TODO(host-shell): bind to the strategy's retry-correlation map", submission_authority)
        self.assertIn("[FINALIZE][TODO] Host shell must wire cancel/health/timing hooks before live activation", submission_authority)
        self.assertIn("state.PendingRetryCorrelationBySignal.Clear();", submission_authority)

        self.assertIn("Required host-shell wiring before activation:", host_contract_doc)
        self.assertIn("TODO seams left intentionally:", host_contract_doc)
        self.assertIn("No attempt was made to wire strategy partial methods or replace existing host-shell stubs outside `src/runtime-core`.", host_contract_doc)

    def test_order_maintenance_scaffold_is_bound_but_keeps_activation_deferred(self) -> None:
        runtime_host = _read(RUNTIME_HOST_PATH)
        order_maintenance = _read(ORDER_MAINTENANCE_PATH)

        self.assertIn("private readonly OrderMaintenanceState _orderMaintenanceState = new OrderMaintenanceState();", runtime_host)
        self.assertIn("private OrderMaintenanceScaffold _orderMaintenance;", runtime_host)
        self.assertIn("private OrderMaintenanceScaffold OrderMaintenance =>", runtime_host)
        self.assertIn("OrderMaintenance.ProcessCancellationQueue(state);", runtime_host)
        self.assertIn("OrderMaintenance.SafeCancelOrder(state, order, context);", runtime_host)
        self.assertIn("CancelOrderRequest = CancelOrder,", runtime_host)

        self.assertIn("internal sealed class OrderMaintenanceScaffold", order_maintenance)
        self.assertIn("public bool IsActivationReady", order_maintenance)
        self.assertIn("DescribeMissingActivationHooks()", order_maintenance)
        self.assertIn("[ORDER_MAINT][TODO] Host shell must wire cancel hooks before activation", order_maintenance)
