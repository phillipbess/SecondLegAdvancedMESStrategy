using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public enum SecondLegBias
    {
        Neutral = 0,
        Long = 1,
        Short = -1,
    }

    public enum SecondLegSetupState
    {
        Reset = 0,
        Blocked = 1,
        SeekingBias = 2,
        SeekingImpulse = 3,
        TrackingPullbackLeg1 = 4,
        TrackingSeparation = 5,
        TrackingPullbackLeg2 = 6,
        WaitingForSignalBar = 7,
        WaitingForTrigger = 8,
        ManagingTrade = 9,
    }

    public enum SecondLegEntryMode
    {
        StrictV1 = 0,
        VideoSecondEntryLite = 1,
    }

    public enum SecondLegTradeDirection
    {
        Both = 0,
        ShortOnly = 1,
        LongOnly = 2,
    }

    public enum StructureLevelKind
    {
        Unknown = 0,
        PriorDayHigh = 1,
        PriorDayLow = 2,
        OpeningRangeHigh = 3,
        OpeningRangeLow = 4,
        SwingHigh = 5,
        SwingLow = 6,
    }

    public sealed class StructureLevel
    {
        public StructureLevelKind Kind { get; set; }
        public double Price { get; set; }
        public string Label { get; set; }

        public StructureLevel()
        {
            Label = string.Empty;
        }
    }

    public sealed class PullbackSnapshot
    {
        public int StartBar { get; set; }
        public int EndBar { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Range { get; set; }
        public double MomentumScore { get; set; }
    }

    public sealed class ImpulseSnapshot
    {
        public int StartBar { get; set; }
        public int EndBar { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Range { get; set; }
        public SecondLegBias Bias { get; set; }
    }

    public sealed class PlannedEntry
    {
        public SecondLegBias Bias { get; set; }
        public string SignalName { get; set; }
        public string Reason { get; set; }
        public double EntryPrice { get; set; }
        public double InitialStopPrice { get; set; }
        public double StopDistance { get; set; }
        public double RiskPerContract { get; set; }
        public double AtrAtPlan { get; set; }
        public string StructureLevelUsed { get; set; }
        public double RoomAtPlan { get; set; }
        public double RequiredRoomAtPlan { get; set; }
        public double StructurePriceAtPlan { get; set; }
        public int Quantity { get; set; }
        public int ExpiryBar { get; set; }

        public PlannedEntry()
        {
            SignalName = string.Empty;
            Reason = string.Empty;
            StructureLevelUsed = string.Empty;
            Bias = SecondLegBias.Neutral;
            ExpiryBar = -1;
            StopDistance = double.NaN;
            RiskPerContract = double.NaN;
            AtrAtPlan = double.NaN;
            RoomAtPlan = double.NaN;
            RequiredRoomAtPlan = double.NaN;
            StructurePriceAtPlan = double.NaN;
        }

        public bool IsValid =>
            Bias != SecondLegBias.Neutral
            && Quantity > 0
            && EntryPrice > 0.0
            && InitialStopPrice > 0.0
            && !string.IsNullOrEmpty(SignalName);

        public static PlannedEntry CreateEmpty()
        {
            return new PlannedEntry();
        }
    }
}
