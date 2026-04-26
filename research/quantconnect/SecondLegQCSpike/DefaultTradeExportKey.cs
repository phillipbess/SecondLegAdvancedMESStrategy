using System;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string BuildDefaultTradeExportKey(DateTime startDate, DateTime endDate)
        {
            return $"{ProjectId}/secondleg_trade_export_{EntryModeToken()}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_leg2_{ParamToken(SecondLegMaxMomentumRatio)}_leg2retr_{ParamToken(LiteMaxLeg2Retracement)}_imp_{ParamToken(EffectiveMinImpulseAtrMultiple())}_pbatr_{ParamToken(LiteMinPullbackAtr)}_l2atr_{ParamToken(LiteMinLeg2Atr)}_sig_{ParamToken(LiteMinSignalClosePct)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}_trig_{MaxTriggerBars}_liq_{(LiteUseStructureBreakEntry ? "1" : "0")}_room_{ParamToken(MinRoomToStructureR)}_rmin_{ParamToken(LiteEntryRoomMinR)}_rmax_{ParamToken(LiteEntryRoomMaxR)}_struct_{TextKeyToken(LiteAllowedStructures)}_hrs_{HourKeyToken(LiteBlockedHours)}_hL_{HourKeyToken(LiteBlockedLongHours)}_hS_{HourKeyToken(LiteBlockedShortHours)}.csv";
        }

        private string BuildDefaultTradeExportHeader()
        {
            return "tradeId,entryMode,signalTime,triggerTime,closeTime,side,entry,stop,touchProbePrice,targetPrice,targetR,riskPts,riskDollars,quantity,atrAtPlan,stopAtrMultiple,impulseAtrMultiple,leg1Retracement,leg2Retracement,leg2MomentumRatio,totalPullbackBars,leg1Bars,leg2Bars,structure,roomToStructureR,signalHour,minutesFromOpen,signalClosePct,signalBodyPct,emaFastDistanceAtr,emaSlowDistanceAtr,atrRatio,slopeAtrPct,maxFavorableR,maxAdverseR,outcome,rMultiple,touchedProbe,barsHeld";
        }
    }
}
