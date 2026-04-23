using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        [NinjaScriptProperty]
        [Range(50, 400)]
        [Display(Name = "Trend EMA Slow", GroupName = "Trend Context", Order = 0)]
        public int TrendEmaPeriod { get; set; } = 200;

        [NinjaScriptProperty]
        [Range(2, 20)]
        [Display(Name = "Slope Lookback Bars", GroupName = "Trend Context", Order = 1)]
        public int SlopeLookbackBars { get; set; } = 5;

        [NinjaScriptProperty]
        [Range(0.0, 0.50)]
        [Display(Name = "Slope Min ATR Pct/Bar", GroupName = "Trend Context", Order = 2)]
        public double SlopeMinAtrPctPerBar { get; set; } = 0.03;

        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "ATR Period", GroupName = "Volatility", Order = 0)]
        public int AtrPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Range(10, 200)]
        [Display(Name = "ATR Regime Lookback", GroupName = "Volatility", Order = 1)]
        public int AtrRegimeLookback { get; set; } = 50;

        [NinjaScriptProperty]
        [Range(0.1, 2.0)]
        [Display(Name = "Min ATR Regime Ratio", GroupName = "Volatility", Order = 2)]
        public double MinAtrRegimeRatio { get; set; } = 0.75;

        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name = "Max ATR Regime Ratio", GroupName = "Volatility", Order = 3)]
        public double MaxAtrRegimeRatio { get; set; } = 2.25;

        [NinjaScriptProperty]
        [Range(0.25, 5.0)]
        [Display(Name = "Max Stop ATR Multiple", GroupName = "Volatility", Order = 4)]
        public double MaxStopAtrMultiple { get; set; } = 1.50;

        [NinjaScriptProperty]
        [Range(0.5, 5.0)]
        [Display(Name = "Min Impulse ATR Multiple", GroupName = "Impulse", Order = 0)]
        public double MinImpulseAtrMultiple { get; set; } = 1.25;

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Max Pullback Bars", GroupName = "Pullback", Order = 0)]
        public int MaxPullbackBars { get; set; } = 12;

        [NinjaScriptProperty]
        [Range(0.1, 1.0)]
        [Display(Name = "Min Pullback Retracement", GroupName = "Pullback", Order = 1)]
        public double MinPullbackRetracement { get; set; } = 0.236;

        [NinjaScriptProperty]
        [Range(0.1, 1.0)]
        [Display(Name = "Max Pullback Retracement", GroupName = "Pullback", Order = 2)]
        public double MaxPullbackRetracement { get; set; } = 0.618;

        [NinjaScriptProperty]
        [Range(0.1, 2.0)]
        [Display(Name = "Second Leg Max Momentum Ratio", GroupName = "Pullback", Order = 3)]
        public double SecondLegMaxMomentumRatio { get; set; } = 0.80;

        [NinjaScriptProperty]
        [Range(0, 10)]
        [Display(Name = "Entry Offset Ticks", GroupName = "Entry", Order = 0)]
        public int EntryOffsetTicks { get; set; } = 1;

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Max Trigger Bars", GroupName = "Entry", Order = 1)]
        public int MaxTriggerBars { get; set; } = 3;

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Display(Name = "Stop Buffer Ticks", GroupName = "Risk", Order = 0)]
        public int StopBufferTicks { get; set; } = 2;

        [NinjaScriptProperty]
        [Range(50, 10000)]
        [Display(Name = "Risk Per Trade ($)", GroupName = "Risk", Order = 1)]
        public double RiskPerTrade { get; set; } = 150.0;

        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Min Room To Structure (R)", GroupName = "Risk", Order = 2)]
        public double MinRoomToStructureR { get; set; } = 1.00;

        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Swing Lookback Bars", GroupName = "Risk", Order = 3)]
        public int SwingLookbackBars { get; set; } = 20;

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Max Trades Per Session", GroupName = "Risk", Order = 4)]
        public int MaxTradesPerSession { get; set; } = 3;

        [NinjaScriptProperty]
        [Range(0, 10)]
        [Display(Name = "Max Consecutive Losses", GroupName = "Risk", Order = 5)]
        public int MaxConsecutiveLosses { get; set; } = 2;

        [NinjaScriptProperty]
        [Range(-20.0, 0.0)]
        [Display(Name = "Daily Loss Limit (R)", GroupName = "Risk", Order = 6)]
        public double DailyLossLimitR { get; set; } = -2.5;

        [NinjaScriptProperty]
        [Range(0, 50)]
        [Display(Name = "Cooldown Bars After Loss", GroupName = "Risk", Order = 7)]
        public int CooldownBarsAfterLoss { get; set; } = 5;

        [NinjaScriptProperty]
        [Display(Name = "Trail Enabled", GroupName = "Simple Trail", Order = 0)]
        public bool TrailEnabled { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "Trail Trigger (pts)", GroupName = "Simple Trail", Order = 1)]
        public double TrailTriggerPoints { get; set; } = 15.0;

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "Trail Lock (pts)", GroupName = "Simple Trail", Order = 2)]
        public double TrailLockPoints { get; set; } = 8.0;

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "Trail Distance (pts)", GroupName = "Simple Trail", Order = 3)]
        public double TrailDistancePoints { get; set; } = 10.0;

        [NinjaScriptProperty]
        [Display(Name = "Structure Filter Enabled", GroupName = "Structure", Order = 0)]
        public bool StructureFilterEnabled { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Use Prior Day High Low", GroupName = "Structure", Order = 1)]
        public bool UsePriorDayHighLow { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Use Opening Range", GroupName = "Structure", Order = 2)]
        public bool UseOpeningRange { get; set; } = true;

        [NinjaScriptProperty]
        [Range(5, 120)]
        [Display(Name = "Opening Range Minutes", GroupName = "Structure", Order = 3)]
        public int OpeningRangeMinutes { get; set; } = 30;

        [NinjaScriptProperty]
        [Display(Name = "Flatten Before Close", GroupName = "Session", Order = 0)]
        public bool FlattenBeforeClose { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "Flatten Time HHmm", GroupName = "Session", Order = 1)]
        public int FlattenTimeHhmm { get; set; } = 1555;
    }
}
