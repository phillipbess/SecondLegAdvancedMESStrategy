using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy : Strategy
    {
        public const string StrategyVersion = "0.2.2-playback-v1";
        public const int V1FastEmaPeriod = 50;
        public const int V1ImpulseBars = 3;
        public const double V1StrongBodyPct = 0.50;
        public const int V1MinStrongBars = 2;
        public const int RthOpenHhmm = 930;
        public const int RthCloseHhmm = 1600;

        private readonly List<StructureLevel> _structureLevels = new List<StructureLevel>();
        private readonly Dictionary<string, DateTime> _decisionDebounce = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private SecondLegSetupState _setupState = SecondLegSetupState.Reset;
        private SecondLegBias _activeBias = SecondLegBias.Neutral;

        private DateTime _sessionAnchor = DateTime.MinValue;
        private DateTime _rthTradingDate = DateTime.MinValue;
        private DateTime _lastStateTransitionUtc = DateTime.MinValue;

        private double _atrValue;
        private double _emaFastValue;
        private double _emaSlowValue;
        private double _emaFastSlopeAtrPct;
        private double _atrRegimeRatio;
        private double _priorRthHigh = double.NaN;
        private double _priorRthLow = double.NaN;
        private double _currentRthHigh = double.NaN;
        private double _currentRthLow = double.NaN;
        private double _openingRangeHigh = double.NaN;
        private double _openingRangeLow = double.NaN;
        private double _separationHigh = double.NaN;
        private double _separationLow = double.NaN;

        private bool _trendContextValid;
        private bool _sessionFilterValid;
        private bool _volatilityRegimeValid;
        private bool _structureRoomValid;
        private bool _openingRangeComplete;

        private ImpulseSnapshot _impulse = new ImpulseSnapshot();
        private PullbackSnapshot _pullbackLeg1 = new PullbackSnapshot();
        private PullbackSnapshot _pullbackLeg2 = new PullbackSnapshot();
        private PlannedEntry _plannedEntry = PlannedEntry.CreateEmpty();

        private double _signalBarHigh;
        private double _signalBarLow;
        private int _signalBarIndex = -1;

        private int _consecutiveLosses;
        private int _tradesThisSession;
        private double _sessionRealizedR;

        private string _lastBlockReason = string.Empty;

        private bool _hasWorkingEntry;
        private bool _hostShellReady;

        private ATR _atr;
        private EMA _emaFast;
        private EMA _emaSlow;

        private Order _entryOrder;
        private bool _entryFilledForActiveSignal;
        private bool _tradeJustClosed;
        private int _pullbackBounceBar = -1;
        private int _lastProcessedTradeCount;
        private int _lossCooldownUntilBar = -1;
        private bool _sessionResetPending;
        private bool _rthOpenResetPending;
        private double _bestFavorablePrice;
        private bool _simpleTrailArmed;
    }
}
