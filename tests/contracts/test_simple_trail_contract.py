"""Source-level contract tests for the donor-style simple trail playback exit."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.nt8_contract_helpers import read_doc_file, read_runtime_file, read_strategy_file

FAMILY_ID = "simple_trail_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class SimpleTrailContractTests(unittest.TestCase):
    def test_manifest_tracks_simple_trail_family(self) -> None:
        family = _family_definition()

        self.assertIn(family["status"], {"scaffold_assertions", "implemented"})
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_properties_define_donor_style_simple_trail_surface(self) -> None:
        properties = read_strategy_file("SecondLegAdvancedMESStrategy.Properties.cs")

        self.assertIn('public bool TrailEnabled { get; set; } = true;', properties)
        self.assertIn('public double TrailTriggerPoints { get; set; } = 15.0;', properties)
        self.assertIn('public double TrailLockPoints { get; set; } = 8.0;', properties)
        self.assertIn('public double TrailDistancePoints { get; set; } = 10.0;', properties)
        self.assertNotIn("InitialTargetR", properties)
        self.assertNotIn("MoveToBreakevenAtR", properties)
        self.assertNotIn("TrailAfterR", properties)

    def test_orders_submit_stop_only_and_manage_simple_trail_monotonically(self) -> None:
        orders = read_strategy_file("SecondLegAdvancedMESStrategy.Orders.cs")
        bar_flow = read_strategy_file("SecondLegAdvancedMESStrategy.BarFlow.cs")
        entry = read_strategy_file("SecondLegAdvancedMESStrategy.EntryAnalysis.cs")
        market_flow = read_strategy_file("SecondLegAdvancedMESStrategy.MarketFlow.cs")
        runtime_lane = read_runtime_file("SecondLegAdvancedRuntimeControlLane.cs")
        transport_adapter = read_strategy_file("SecondLegAdvancedMESStrategy.TransportAdapter.cs")

        self.assertIn("tradeManager?.SubmitPrimaryEntry(_plannedEntry);", orders)
        self.assertIn("internal void SubmitPrimaryEntry(PlannedEntry plannedEntry)", runtime_lane)
        self.assertIn("_strategy.SubmitPrimaryEntryBridge(plannedEntry);", runtime_lane)
        self.assertIn("private bool SubmitProtectiveReplace(", runtime_lane)
        self.assertIn("private void DeferProtectiveEnsure(double stopPx, int qty, string stopTag, string reason, string deferredReason)", runtime_lane)
        self.assertIn("private string ResolveProtectiveSignalName()", runtime_lane)
        self.assertIn("private TransportResult SubmitPrimaryEntryBridge(PlannedEntry plannedEntry)", transport_adapter)
        self.assertNotIn("SetProfitTarget(", orders)
        self.assertIn("_simpleTrailArmed = false;", orders)
        self.assertIn("private bool UpdateManagedTradeProtection(double? marketPrice = null)", orders)
        self.assertIn("if (!_simpleTrailArmed && _bestFavorablePrice >= triggerPrice)", orders)
        self.assertIn("if (!_simpleTrailArmed && _bestFavorablePrice <= triggerPrice)", orders)
        self.assertIn("Math.Max(updatedStop, Math.Max(lockStop, trailStop))", orders)
        self.assertIn("Math.Min(updatedStop, Math.Min(lockStop, trailStop))", orders)
        self.assertIn('if (!MaySubmitOrders("SubmitPlannedEntry"))', orders)
        self.assertIn('RecordEntryBlock("ProtectiveStopRejected",', orders)
        self.assertIn('TriggerFlatten("ProtectiveStopRejected");', orders)
        self.assertIn("UpdateManagedTradeProtection();", orders)
        self.assertIn("marketDataUpdate.MarketDataType == MarketDataType.Last", market_flow)
        self.assertIn("marketDataUpdate.Price > 0.0", market_flow)
        self.assertIn("UpdateManagedTradeProtection(marketDataUpdate.Price);", market_flow)
        self.assertIn('ValidateStopQuantity("OnMarketData.Last");', market_flow)
        self.assertIn('EvaluateRealtimeReconnectGrace("OnMarketData.Last");', market_flow)
        self.assertIn('BeginReconnectGrace("MarketDataResume");', market_flow)
        self.assertIn('PumpExitOps("OnMarketData");', market_flow)
        self.assertIn("case SecondLegSetupState.ManagingTrade:", bar_flow)
        self.assertIn("HandleActiveTradeLifecycle();", bar_flow)
        self.assertIn("private TransportResult EnsureProtectiveStopBridge(", transport_adapter)
        self.assertIn("private TransportResult ChangeProtectiveStopBridge(Order workingStop, int quantity, double stopPrice)", transport_adapter)
        self.assertIn("SubmitOrderUnmanaged(", transport_adapter)
        self.assertIn("ChangeOrder(", transport_adapter)
        self.assertIn("if (changeResult.IsPendingAck)", runtime_lane)
        self.assertIn('_strategy.RefreshRuntimeSnapshot("ExitController.ChangeUnmanaged.PendingAck");', runtime_lane)
        self.assertIn('"ExitController.EnsureProtectiveExit.QuantityMismatch"', runtime_lane)
        self.assertIn('"ExitController.EnsureProtectiveExit.SideMismatch"', runtime_lane)
        self.assertIn('"ExitController.ChangeUnmanaged.Replace"', runtime_lane)
        self.assertIn('"DEFERRED_REPLACE"', runtime_lane)
        self.assertIn("private void MarkFlattenSubmitFailure(string reason, string signalName, int quantity, string detail)", runtime_lane)
        self.assertIn("TransportResult flattenResult = _strategy.SubmitFlattenBridge(ownedSignal, effQty);", runtime_lane)
        self.assertEqual(transport_adapter.count("SetStopLoss("), 0)
        lifecycle = read_strategy_file("SecondLegAdvancedMESStrategy.StateLifecycle.cs")
        self.assertIn('RunRuntimeMaintenancePass("OnBarUpdate");', lifecycle)
        self.assertIn("PumpExitOps(context);", lifecycle)
        self.assertIn('BeginReconnectGrace("State.Realtime");', lifecycle)

    def test_structure_room_gate_is_r_based_not_target_fallback_based(self) -> None:
        entry = read_strategy_file("SecondLegAdvancedMESStrategy.EntryAnalysis.cs")
        plan_doc = read_doc_file("Implementation_Plan.md")

        self.assertIn("private bool EvaluateStructureRoom(double entryPrice, double stopPrice, SecondLegBias bias)", entry)
        self.assertNotIn("targetPrice", entry)
        self.assertIn("return room >= requiredRoom;", entry)
        self.assertIn("return longRoom >= requiredRoom;", entry)
        self.assertIn("simple trail arm/lock/distance management", plan_doc)


if __name__ == "__main__":
    unittest.main()
