"""Source-level contract tests for the stripped-down v1 entry surface."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.nt8_contract_helpers import read_strategy_file

FAMILY_ID = "edge_filters_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class EdgeFiltersContractTests(unittest.TestCase):
    def test_manifest_tracks_edge_filters_family(self) -> None:
        family = _family_definition()

        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_property_surface_matches_stripped_v1_contract(self) -> None:
        properties = read_strategy_file("SecondLegAdvancedMESStrategy.Properties.cs")

        required_markers = [
            "public double MinImpulseAtrMultiple { get; set; } = 1.25;",
            "public double MaxStopAtrMultiple { get; set; } = 1.50;",
            "public int MaxTriggerBars { get; set; } = 3;",
            "public bool StructureFilterEnabled { get; set; } = true;",
            "public bool UsePriorDayHighLow { get; set; } = true;",
            "public bool UseOpeningRange { get; set; } = true;",
            "public int OpeningRangeMinutes { get; set; } = 30;",
            "public int SwingLookbackBars { get; set; } = 20;",
            "public int CooldownBarsAfterLoss { get; set; } = 5;",
        ]

        for marker in required_markers:
            self.assertIn(marker, properties)

        forbidden_markers = [
            "FastEmaPeriod",
            "ImpulseBars",
            "StrongBodyPct",
            "MinStrongBars",
            "HigherTimeframeEnabled",
            "HigherTimeframeMinutes",
            "TrendFilterMode",
            "RelativeVolumeEnabled",
            "RvolLookbackSessions",
            "MinImpulseRvol",
            "MinConfirmRvol",
            "UseOpeningBiasFilter",
            "OpeningBiasMode",
            "UseOvernightHighLow",
            "UseVwapStructure",
            "MinRoomToStructureAtr",
            "MinImpulseScore",
            "StrongLegAtrThreshold",
            "MinDirectionalClosePct",
            "RequirePriorSwingBreak",
            "ConfirmClv",
            "ConfirmMinAtrMultiple",
            "PullbackRetracementStrongMax",
            "RequireSecondLegFailure",
        ]

        for marker in forbidden_markers:
            self.assertNotIn(marker, properties)

    def test_runtime_sources_match_stripped_v1_logic(self) -> None:
        state_lifecycle = read_strategy_file("SecondLegAdvancedMESStrategy.StateLifecycle.cs")
        entry_analysis = read_strategy_file("SecondLegAdvancedMESStrategy.EntryAnalysis.cs")
        advanced_context = read_strategy_file("SecondLegAdvancedMESStrategy.AdvancedContext.cs")
        closed_bar_adapter = read_strategy_file("SecondLegAdvancedMESStrategy.ClosedBarAdapter.cs")
        bar_flow = read_strategy_file("SecondLegAdvancedMESStrategy.BarFlow.cs")
        orders = read_strategy_file("SecondLegAdvancedMESStrategy.Orders.cs")

        self.assertIn("UpdateAdvancedSessionContext();", state_lifecycle)
        self.assertIn("if (hasStrategyLookback && IsClosedPrimaryBarPass())", state_lifecycle)
        self.assertIn('RunClosedBarStrategyPass();', state_lifecycle)
        self.assertIn('RunRuntimeMaintenancePass("OnBarUpdate");', state_lifecycle)
        self.assertIn("Calculate = Calculate.OnEachTick;", state_lifecycle)
        self.assertIn("private void EnterBlockedState(string reason)", state_lifecycle)
        self.assertIn("private void ReleaseBlockedState()", state_lifecycle)
        self.assertIn("private void AdvanceResetState()", state_lifecycle)
        self.assertIn("ClearSetupState(reason, SecondLegSetupState.Reset);", state_lifecycle)
        self.assertIn("ClearSetupState(reason, SecondLegSetupState.Blocked);", state_lifecycle)
        self.assertIn("AdvanceClosedBarSessionFlags();", state_lifecycle)
        self.assertIn("protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)", read_strategy_file("SecondLegAdvancedMESStrategy.MarketFlow.cs"))
        self.assertIn("private int ClosedBarBarsAgo()", closed_bar_adapter)
        self.assertIn("private int ClosedBarIndex(int barsAgoOffset = 0)", closed_bar_adapter)
        self.assertIn("private int ClosedBarCount()", closed_bar_adapter)
        self.assertIn("private DateTime ClosedBarTime(int barsAgoOffset = 0)", closed_bar_adapter)
        self.assertIn("private double ClosedBarFastEma(int barsAgoOffset = 0)", closed_bar_adapter)
        self.assertIn("private double ClosedBarAtrValue(int barsAgoOffset = 0)", closed_bar_adapter)
        self.assertIn("private int ClosedBarTimeHhmm(int barsAgoOffset = 0)", closed_bar_adapter)
        self.assertIn("private void ResetClosedBarSessionFlags()", closed_bar_adapter)
        self.assertIn("private void AdvanceClosedBarSessionFlags()", closed_bar_adapter)
        self.assertIn("private bool ClosedBarIsFirstBarOfSession()", closed_bar_adapter)
        self.assertIn("private bool ClosedBarStartsRthSession()", advanced_context)
        self.assertIn("private bool IsRthBar(int hhmm)", advanced_context)
        self.assertIn("if (IsFirstTickOfBar)", closed_bar_adapter)
        self.assertIn("return _closedBarWasFirstBarOfSession;", closed_bar_adapter)

        self.assertIn("bool longTrendValid = ClosedBarClose() > _emaSlowValue", entry_analysis)
        self.assertIn("bool shortTrendValid = ClosedBarClose() < _emaSlowValue", entry_analysis)
        self.assertIn("double slopeDenominator = Math.Max(_atrValue, TickSize);", entry_analysis)
        self.assertIn("return (delta / SlopeLookbackBars) / slopeDenominator;", entry_analysis)
        self.assertIn("ClosedBarCount() < V1ImpulseBars", entry_analysis)
        self.assertIn("const int lookback = V1ImpulseBars;", entry_analysis)
        self.assertIn("bodyPct >= V1StrongBodyPct", entry_analysis)
        self.assertIn("strongBars < V1MinStrongBars", entry_analysis)
        self.assertIn("_emaFast = EMA(V1FastEmaPeriod);", state_lifecycle)
        self.assertIn("bool hasStrategyLookback = ClosedBarIndex() >=", state_lifecycle)
        self.assertIn("if (impulseMove < (MinImpulseAtrMultiple * _atrValue))", entry_analysis)
        self.assertIn("ClosedBarFastEma()", entry_analysis)
        self.assertIn("ClosedBarAtrValue()", entry_analysis)
        self.assertIn("ClosedBarCount()", entry_analysis)
        self.assertIn("ClosedBarHigh(barsAgo)", entry_analysis)
        self.assertIn("ClosedBarClose() > ClosedBarOpen()", entry_analysis)
        self.assertIn("&& ClosedBarClose() > ClosedBarClose(1)", entry_analysis)
        self.assertIn("&& ClosedBarHigh() > ClosedBarHigh(1)", entry_analysis)
        self.assertIn("&& ClosedBarClose() < ClosedBarClose(1)", entry_analysis)
        self.assertIn("&& ClosedBarLow() < ClosedBarLow(1)", entry_analysis)
        self.assertIn("if (!HasPullbackLeg2Candidate())", entry_analysis)
        self.assertIn("return startingRetracement >= MinPullbackRetracement;", entry_analysis)
        self.assertIn("return retracement >= MinPullbackRetracement;", entry_analysis)
        self.assertIn('_setupState = SecondLegSetupState.TrackingPullbackLeg2;', entry_analysis)
        self.assertIn("bool leg2Refreshed = false;", entry_analysis)
        self.assertIn("if (leg2Refreshed)", entry_analysis)
        self.assertIn("PullbackLeg2CandidateRefresh", entry_analysis)
        self.assertNotIn('LogSetupStateTransition(previousState, _setupState, "PullbackLeg2Candidate");\n            TryWaitForSignalBar();', entry_analysis)
        self.assertIn("return \"EntryExpired\";", entry_analysis)
        self.assertIn("CancelPendingEntry(armedEntryInvalidationReason);", entry_analysis)
        self.assertIn("ClosedBarIndex()", entry_analysis)
        self.assertIn('RecordEntryBlock(', entry_analysis)
        self.assertIn('"SignalInvalid"', entry_analysis)
        self.assertIn('RecordEntryBlock("RiskTooSmall",', entry_analysis)
        self.assertIn('RecordEntryBlock("SecondLegTooStrong",', entry_analysis)
        self.assertIn("private bool IsLongOpposingStructure(StructureLevelKind kind)", entry_analysis)
        self.assertIn("private bool IsShortOpposingStructure(StructureLevelKind kind)", entry_analysis)
        self.assertIn("if (!IsShortOpposingStructure(level.Kind))", entry_analysis)
        self.assertIn("if (!IsLongOpposingStructure(level.Kind))", entry_analysis)
        self.assertNotIn('return "SecondLegTooStrong";', entry_analysis)
        self.assertNotIn("private bool ArmedEntryLeg2StillCorrective()", entry_analysis)
        self.assertNotIn('ResetSetupState("SecondLegTooStrong");', entry_analysis)
        self.assertNotIn('InvalidRisk', entry_analysis)
        self.assertNotIn("private void HandleActiveTradeLifecycle()", entry_analysis)
        self.assertNotIn("private bool HasPendingOrWorkingEntryLifecycle()", entry_analysis)
        self.assertNotIn("private bool HasActiveManagedTradeContext()", entry_analysis)
        self.assertNotIn("private void PromoteToManagingTrade()", entry_analysis)
        self.assertNotIn("private void DemoteToWaitingForTrigger()", entry_analysis)
        self.assertNotIn("private void SyncSetupStateWithEntryLifecycle()", entry_analysis)
        self.assertNotIn("private bool EnforceSessionClose()", entry_analysis)
        self.assertIn("if (HasWorkingPrimaryEntryForActiveSignal())", orders)
        self.assertIn("private bool CancelWorkingPrimaryEntries(string reason)", orders)
        self.assertIn("private void HandleActiveTradeLifecycle()", orders)
        self.assertIn("private bool HasPendingOrWorkingEntryLifecycle()", orders)
        self.assertIn("private bool HasActiveManagedTradeContext()", orders)
        self.assertIn("private void PromoteToManagingTrade()", orders)
        self.assertIn("private void DemoteToWaitingForTrigger()", orders)
        self.assertIn("private void SyncSetupStateWithEntryLifecycle()", orders)
        self.assertNotIn("SignalExpired", entry_analysis)
        self.assertNotIn("MinRoomToStructureAtr", entry_analysis)
        self.assertNotIn("MinPullbackBars", entry_analysis)
        self.assertNotIn("CancelIfOppositeSignal", entry_analysis)
        self.assertIn("private bool EnforceSessionClose()", state_lifecycle)
        self.assertIn("bool rthBoundary = ClosedBarStartsRthSession();", state_lifecycle)
        self.assertIn('CancelPendingEntry("RthOpen");', state_lifecycle)
        self.assertIn('ResetSetupState("RthOpen");', state_lifecycle)

        self.assertIn("private void RefreshStructureLevels()", advanced_context)
        self.assertIn("DateTime tradingDate = ClosedBarTime().Date;", advanced_context)
        self.assertIn("if (IsRthBar(hhmm))", advanced_context)
        self.assertIn("if (_rthTradingDate == DateTime.MinValue || tradingDate != _rthTradingDate)", advanced_context)
        self.assertIn("int hhmm = ClosedBarTimeHhmm();", advanced_context)
        self.assertIn("int openingRangeEnd = AddMinutesToHhmm(RthOpenHhmm, OpeningRangeMinutes);", advanced_context)
        self.assertIn("AddStructureLevel(StructureLevelKind.PriorDayHigh, _priorRthHigh, \"PDH\");", advanced_context)
        self.assertNotIn("PriorDayClose", advanced_context)
        self.assertNotIn("UpdateRelativeVolumeState", advanced_context)

        self.assertIn("if (_lossCooldownUntilBar >= 0 && ClosedBarIndex() < _lossCooldownUntilBar)", bar_flow)
        self.assertIn("case SecondLegSetupState.Blocked:", bar_flow)
        self.assertIn("ReleaseBlockedState();", bar_flow)
        self.assertIn("case SecondLegSetupState.Reset:", bar_flow)
        self.assertIn("AdvanceResetState();", bar_flow)
        self.assertIn("private string GetHardBlockReason()", bar_flow)
        self.assertIn('return "TradeLimitHit";', bar_flow)
        self.assertIn('return "LossStreakHit";', bar_flow)
        self.assertIn('return "DailyLossLimit";', bar_flow)
        self.assertIn('return "LossCooldown";', bar_flow)
        self.assertIn("EnterBlockedState(blockReason);", bar_flow)
        self.assertIn("if (CooldownBarsAfterLoss > 0)", orders)


if __name__ == "__main__":
    unittest.main()
