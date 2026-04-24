using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;

namespace QuantConnect.Algorithm.CSharp
{
    public class SecondLegQCSpike : QCAlgorithm
    {
        private const double TickSize = 0.25;
        private const double PointValue = 5.0;
        private const int ImpulseBars = 3;
        private const int MinStrongBars = 2;
        private const double StrongBodyPct = 0.50;
        private const int TrendEmaFast = 50;
        private const int TrendEmaSlow = 200;
        private const int SlopeLookbackBars = 5;
        private const double SlopeMinAtrPctPerBar = 0.03;
        private const int AtrPeriod = 14;
        private const int AtrRegimeLookback = 50;
        private const double MinAtrRegimeRatio = 0.75;
        private const double MaxAtrRegimeRatio = 2.25;
        private double MinImpulseAtrMultiple = 1.25;
        private int MaxPullbackBars = 12;
        private double MinPullbackRetracement = 0.236;
        private double MaxPullbackRetracement = 0.618;
        private double SecondLegMaxMomentumRatio = 0.80;
        private const int EntryOffsetTicks = 1;
        private const int MaxTriggerBars = 3;
        private const int StopBufferTicks = 2;
        private double RiskPerTrade = 150.0;
        private const double MaxStopAtrMultiple = 1.50;
        private const int SwingLookbackBars = 20;
        private double MinRoomToStructureR = 1.00;
        private const int OpeningRangeMinutes = 30;
        private const int MaxOutcomeBars = 24;

        private Future _future;
        private Symbol _continuousSymbol;
        private readonly List<BarSnapshot> _bars = new List<BarSnapshot>();
        private readonly List<double> _trueRanges = new List<double>();
        private readonly List<double> _atrValues = new List<double>();
        private readonly Dictionary<string, MonthStats> _monthly = new Dictionary<string, MonthStats>();
        private readonly Dictionary<string, int> _blocks = new Dictionary<string, int>();
        private readonly StringBuilder _tradeExport = new StringBuilder();
        private readonly List<string> _tradeRuntimeRows = new List<string>();

        private DateTime _sessionDate = DateTime.MinValue;
        private double _sessionHigh = double.NaN;
        private double _sessionLow = double.NaN;
        private double _priorRthHigh = double.NaN;
        private double _priorRthLow = double.NaN;
        private double _openingRangeHigh = double.NaN;
        private double _openingRangeLow = double.NaN;
        private bool _openingRangeComplete;

        private SetupState _state = SetupState.SeekingImpulse;
        private Bias _activeBias = Bias.Neutral;
        private ImpulseSnapshot _impulse = new ImpulseSnapshot();
        private PullbackSnapshot _leg1 = new PullbackSnapshot();
        private PullbackSnapshot _leg2 = new PullbackSnapshot();
        private PlannedSignal _planned = new PlannedSignal();
        private VirtualTrade _virtualTrade = new VirtualTrade();
        private double _separationHigh = double.NaN;
        private double _separationLow = double.NaN;
        private int _separationBar = -1;

        private int _fiveMinuteBars;
        private int _rthFiveMinuteBars;
        private int _trendBars;
        private int _impulseCount;
        private int _leg1Count;
        private int _separationCount;
        private int _leg2CandidateCount;
        private int _armedSignals;
        private int _longArmed;
        private int _shortArmed;
        private int _triggeredSignals;
        private int _expiredSignals;
        private int _virtualTrades;
        private int _touchOneR;
        private int _winsTwoR;
        private int _stops;
        private int _timeouts;
        private double _netR;
        private string _tradeExportKey = string.Empty;

        public override void Initialize()
        {
            DateTime startDate = DateParameter("startDate", new DateTime(2025, 4, 24));
            DateTime endDate = DateParameter("endDate", new DateTime(2026, 4, 23));
            MinImpulseAtrMultiple = DoubleParameter("minImpulseAtr", MinImpulseAtrMultiple);
            MaxPullbackBars = IntParameter("maxPullbackBars", MaxPullbackBars);
            MinPullbackRetracement = DoubleParameter("minPullbackRetracement", MinPullbackRetracement);
            MaxPullbackRetracement = DoubleParameter("maxPullbackRetracement", MaxPullbackRetracement);
            SecondLegMaxMomentumRatio = DoubleParameter("secondLegMaxMomentumRatio", SecondLegMaxMomentumRatio);
            MinRoomToStructureR = DoubleParameter("minRoomToStructureR", MinRoomToStructureR);
            RiskPerTrade = DoubleParameter("riskPerTrade", RiskPerTrade);

            SetStartDate(startDate.Year, startDate.Month, startDate.Day);
            SetEndDate(endDate.Year, endDate.Month, endDate.Day);
            SetCash(100000);
            SetTimeZone(TimeZones.NewYork);

            _future = AddFuture(
                Futures.Indices.MicroSP500EMini,
                Resolution.Minute,
                dataMappingMode: DataMappingMode.OpenInterest,
                dataNormalizationMode: DataNormalizationMode.BackwardsRatio,
                contractDepthOffset: 0);

            _continuousSymbol = _future.Symbol;
            Consolidate<TradeBar>(_continuousSymbol, TimeSpan.FromMinutes(5), OnFiveMinuteBar);
            _tradeExportKey = $"{ProjectId}/secondleg_trade_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_leg2_{ParamToken(SecondLegMaxMomentumRatio)}_imp_{ParamToken(MinImpulseAtrMultiple)}_room_{ParamToken(MinRoomToStructureR)}.csv";
            _tradeExport.AppendLine("tradeId,signalTime,triggerTime,closeTime,side,entry,stop,oneR,twoR,riskPts,riskDollars,quantity,atrAtPlan,stopAtrMultiple,impulseAtrMultiple,leg1Retracement,leg2Retracement,leg2MomentumRatio,totalPullbackBars,leg1Bars,leg2Bars,structure,roomToStructureR,outcome,rMultiple,touched1R,barsHeld");

            Debug($"SecondLegQCSpike initialized: MES continuous futures entry-detector plus virtual outcome pass, no orders. startDate={startDate:yyyy-MM-dd} endDate={endDate:yyyy-MM-dd} minImpulseAtr={MinImpulseAtrMultiple:0.###} minPullbackRetracement={MinPullbackRetracement:0.###} maxPullbackRetracement={MaxPullbackRetracement:0.###} secondLegMaxMomentumRatio={SecondLegMaxMomentumRatio:0.###} minRoomToStructureR={MinRoomToStructureR:0.###} riskPerTrade={RiskPerTrade:0.##}");
        }

        private void OnFiveMinuteBar(TradeBar input)
        {
            _fiveMinuteBars++;
            if (!IsRegularSession(input.EndTime))
                return;

            var bar = BuildBarSnapshot(input);
            _bars.Add(bar);
            _rthFiveMinuteBars++;
            GetMonth(bar.EndTime).Bars++;

            UpdateSessionState(bar);

            if (!IndicatorsReady(bar))
                return;

            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_state == SetupState.WaitingForTrigger)
            {
                ObserveTriggerOrExpiry(bar);
                if (_state == SetupState.WaitingForTrigger)
                    return;
            }

            RefreshTrendContext(bar);
            if (_activeBias == Bias.Neutral)
            {
                ResetSetup("TrendOrAtrInvalid", false);
                return;
            }

            _trendBars++;
            switch (_state)
            {
                case SetupState.SeekingImpulse:
                    TryCaptureImpulse(bar);
                    break;
                case SetupState.TrackingPullbackLeg1:
                    TryTrackLeg1(bar);
                    break;
                case SetupState.TrackingSeparation:
                    TryTrackSeparation(bar);
                    break;
                case SetupState.TrackingPullbackLeg2:
                    TryTrackLeg2(bar);
                    break;
                case SetupState.WaitingForSignalBar:
                    TryBuildSignal(bar);
                    break;
                default:
                    _state = SetupState.SeekingImpulse;
                    break;
            }
        }

        public override void OnEndOfAlgorithm()
        {
            if (_virtualTrade.IsActive)
                CloseVirtualTrade(_bars[_bars.Count - 1], "AlgorithmEnd");
            SaveTradeExport();

            SetRuntimeStatistic("5m Bars", _fiveMinuteBars.ToString());
            SetRuntimeStatistic("RTH 5m Bars", _rthFiveMinuteBars.ToString());
            SetRuntimeStatistic("Trend Bars", _trendBars.ToString());
            SetRuntimeStatistic("Impulses", _impulseCount.ToString());
            SetRuntimeStatistic("Leg1", _leg1Count.ToString());
            SetRuntimeStatistic("Separations", _separationCount.ToString());
            SetRuntimeStatistic("Leg2 Candidates", _leg2CandidateCount.ToString());
            SetRuntimeStatistic("Armed", _armedSignals.ToString());
            SetRuntimeStatistic("Long Armed", _longArmed.ToString());
            SetRuntimeStatistic("Short Armed", _shortArmed.ToString());
            SetRuntimeStatistic("Triggered", _triggeredSignals.ToString());
            SetRuntimeStatistic("Expired", _expiredSignals.ToString());
            SetRuntimeStatistic("Virtual Trades", _virtualTrades.ToString());
            SetRuntimeStatistic("Touch 1R", _touchOneR.ToString());
            SetRuntimeStatistic("Win 2R", _winsTwoR.ToString());
            SetRuntimeStatistic("Stops", _stops.ToString());
            SetRuntimeStatistic("Timeouts", _timeouts.ToString());
            SetRuntimeStatistic("Net R", _netR.ToString("0.00"));
            SetRuntimeStatistic("Avg R", _virtualTrades > 0 ? (_netR / _virtualTrades).ToString("0.00") : "0.00");
            SetRuntimeStatistic("Params", $"imp={MinImpulseAtrMultiple:0.###} retr={MinPullbackRetracement:0.###}-{MaxPullbackRetracement:0.###} leg2={SecondLegMaxMomentumRatio:0.###} room={MinRoomToStructureR:0.###}");
            SetRuntimeStatistic("Export Key", _tradeExportKey);
            foreach (string row in _tradeRuntimeRows)
            {
                int colon = row.IndexOf(':');
                if (colon > 0)
                    SetRuntimeStatistic(row.Substring(0, colon), row.Substring(colon + 1));
            }
            SetRuntimeStatistic("Block Types", _blocks.Count.ToString());
            SetRuntimeStatistic("SignalInvalid", BlockCount("SignalInvalid").ToString());
            SetRuntimeStatistic("StopTooWide", BlockCount("StopTooWide").ToString());
            SetRuntimeStatistic("StructureRoom", BlockCount("StructureRoom").ToString());
            SetRuntimeStatistic("RiskTooSmall", BlockCount("RiskTooSmall").ToString());
            int blockRank = 1;
            foreach (var item in _blocks.OrderByDescending(x => x.Value).Take(5))
                SetRuntimeStatistic($"Block {blockRank++}", $"{item.Key}:{item.Value}");

            Debug($"ENTRY_DETECTOR_SUMMARY bars={_rthFiveMinuteBars} trendBars={_trendBars} impulses={_impulseCount} leg1={_leg1Count} separation={_separationCount} leg2={_leg2CandidateCount} armed={_armedSignals} long={_longArmed} short={_shortArmed} triggered={_triggeredSignals} expired={_expiredSignals}");
            Debug($"VIRTUAL_OUTCOME_SUMMARY trades={_virtualTrades} touch1R={_touchOneR} win2R={_winsTwoR} stops={_stops} timeouts={_timeouts} netR={_netR:0.00} avgR={(_virtualTrades > 0 ? _netR / _virtualTrades : 0.0):0.00}");
            foreach (var item in _monthly.OrderBy(x => x.Key))
                Debug($"MONTH_SUMMARY month={item.Key} bars={item.Value.Bars} armed={item.Value.Armed} long={item.Value.LongArmed} short={item.Value.ShortArmed} triggered={item.Value.Triggered} expired={item.Value.Expired}");
            foreach (var item in _blocks.OrderByDescending(x => x.Value).Take(12))
                Debug($"BLOCK_SUMMARY reason={item.Key} count={item.Value}");
        }

        private void TryCaptureImpulse(BarSnapshot bar)
        {
            if (_activeBias == Bias.Neutral || _bars.Count < ImpulseBars)
                return;

            var window = _bars.Skip(_bars.Count - ImpulseBars).Take(ImpulseBars).ToList();
            double high = window.Max(x => x.High);
            double low = window.Min(x => x.Low);
            double move = high - low;
            if (move < MinImpulseAtrMultiple * bar.Atr)
                return;

            int strongBars = window.Count(x =>
            {
                double range = Math.Max(x.High - x.Low, TickSize);
                double bodyPct = Math.Abs(x.Close - x.Open) / range;
                bool directional = _activeBias == Bias.Long ? x.Close > x.Open : x.Close < x.Open;
                return directional && bodyPct >= StrongBodyPct;
            });
            if (strongBars < MinStrongBars)
                return;

            if (_activeBias == Bias.Long && !(bar.Close > bar.Open && bar.Close > bar.EmaFast))
                return;
            if (_activeBias == Bias.Short && !(bar.Close < bar.Open && bar.Close < bar.EmaFast))
                return;

            _impulse = new ImpulseSnapshot
            {
                StartBar = window[0].Index,
                EndBar = bar.Index,
                High = high,
                Low = low,
                Range = move,
                Bias = _activeBias
            };
            _leg1 = new PullbackSnapshot();
            _leg2 = new PullbackSnapshot();
            _separationHigh = double.NaN;
            _separationLow = double.NaN;
            _separationBar = -1;
            _impulseCount++;
            _state = SetupState.TrackingPullbackLeg1;
        }

        private void TryTrackLeg1(BarSnapshot bar)
        {
            if (bar.Index <= _impulse.EndBar || _bars.Count < 2)
                return;

            BarSnapshot previous = PreviousBar();
            if (!_leg1.IsActive)
            {
                bool starts = _impulse.Bias == Bias.Long
                    ? bar.Low < previous.Low || bar.Close < previous.Close
                    : bar.High > previous.High || bar.Close > previous.Close;
                if (!starts)
                    return;

                _leg1 = PullbackSnapshot.FromBar(bar);
                double startingRetracement = ComputeRetracement(_impulse.Bias == Bias.Long ? _leg1.Low : _leg1.High);
                if (startingRetracement > MaxPullbackRetracement)
                {
                    ResetSetup("PullbackTooDeep");
                    return;
                }

                if (startingRetracement >= MinPullbackRetracement)
                {
                    _leg1Count++;
                    _state = SetupState.TrackingSeparation;
                }
                return;
            }

            _leg1.Absorb(bar);
            int bars = bar.Index - _leg1.StartBar + 1;
            double retracement = ComputeRetracement(_impulse.Bias == Bias.Long ? _leg1.Low : _leg1.High);
            if (bars > MaxPullbackBars)
            {
                ResetSetup("PullbackTooLong");
                return;
            }
            if (retracement > MaxPullbackRetracement)
            {
                ResetSetup("PullbackTooDeep");
                return;
            }
            if (retracement >= MinPullbackRetracement)
            {
                _leg1Count++;
                _state = SetupState.TrackingSeparation;
            }
        }

        private void TryTrackSeparation(BarSnapshot bar)
        {
            if (!_leg1.IsActive || _bars.Count < 2)
                return;

            int totalBars = bar.Index - _leg1.StartBar + 1;
            if (totalBars > MaxPullbackBars)
            {
                ResetSetup("PullbackTooLong");
                return;
            }

            BarSnapshot previous = PreviousBar();
            if (_impulse.Bias == Bias.Long)
            {
                bool separation = bar.Low >= _leg1.Low && bar.Close > previous.Close && bar.High > previous.High;
                if (separation)
                {
                    CaptureSeparation(bar);
                    return;
                }

                bool extendsPullback = bar.Low < _leg1.Low || bar.Close < previous.Close;
                if (extendsPullback)
                {
                    _leg1.Absorb(bar);
                    if (ComputeRetracement(_leg1.Low) > MaxPullbackRetracement)
                        ResetSetup("PullbackTooDeep");
                }
            }
            else
            {
                bool separation = bar.High <= _leg1.High && bar.Close < previous.Close && bar.Low < previous.Low;
                if (separation)
                {
                    CaptureSeparation(bar);
                    return;
                }

                bool extendsPullback = bar.High > _leg1.High || bar.Close > previous.Close;
                if (extendsPullback)
                {
                    _leg1.Absorb(bar);
                    if (ComputeRetracement(_leg1.High) > MaxPullbackRetracement)
                        ResetSetup("PullbackTooDeep");
                }
            }
        }

        private void CaptureSeparation(BarSnapshot bar)
        {
            _separationHigh = bar.High;
            _separationLow = bar.Low;
            _separationBar = bar.Index;
            _separationCount++;
            _state = SetupState.TrackingPullbackLeg2;
        }

        private void TryTrackLeg2(BarSnapshot bar)
        {
            if (_separationBar < 0 || bar.Index <= _separationBar || _bars.Count < 2)
                return;

            BarSnapshot previous = PreviousBar();
            if (!_leg2.IsActive)
            {
                bool starts = _impulse.Bias == Bias.Long
                    ? bar.Low < previous.Low || bar.Close < previous.Close
                    : bar.High > previous.High || bar.Close > previous.Close;
                if (!starts)
                {
                    _separationHigh = Math.Max(_separationHigh, bar.High);
                    _separationLow = Math.Min(_separationLow, bar.Low);
                    return;
                }

                _leg2 = PullbackSnapshot.FromBar(bar);
            }
            else
                _leg2.Absorb(bar);

            if (HasLeg2Candidate(bar))
            {
                _leg2CandidateCount++;
                _state = SetupState.WaitingForSignalBar;
            }
        }

        private void TryBuildSignal(BarSnapshot bar)
        {
            if (!_leg2.IsActive || _bars.Count < 2)
                return;

            BarSnapshot previous = PreviousBar();
            bool refreshed = false;
            if (_impulse.Bias == Bias.Long && (bar.Low < _leg2.Low || bar.Close < previous.Close))
            {
                _leg2.Absorb(bar);
                refreshed = true;
            }
            else if (_impulse.Bias == Bias.Short && (bar.High > _leg2.High || bar.Close > previous.Close))
            {
                _leg2.Absorb(bar);
                refreshed = true;
            }

            if (!HasLeg2Candidate(bar))
            {
                if (_state != SetupState.SeekingImpulse)
                    _state = SetupState.TrackingPullbackLeg2;
                return;
            }

            if (refreshed || bar.Index <= _leg2.EndBar)
                return;

            double midpoint = bar.Low + (bar.High - bar.Low) * 0.5;
            bool signal = _impulse.Bias == Bias.Long
                ? bar.Low >= _leg2.Low && bar.Close >= midpoint
                : bar.High <= _leg2.High && bar.Close <= midpoint;
            if (!signal)
            {
                Block("SignalInvalid");
                return;
            }

            BuildPlannedSignal(bar);
        }

        private bool HasLeg2Candidate(BarSnapshot bar)
        {
            int totalBars = bar.Index - _leg1.StartBar + 1;
            if (totalBars > MaxPullbackBars)
            {
                ResetSetup("PullbackTooLong");
                return false;
            }

            if (_impulse.Bias == Bias.Long && bar.Close <= bar.EmaSlow)
            {
                ResetSetup("CorrectiveSideInvalid");
                return false;
            }
            if (_impulse.Bias == Bias.Short && bar.Close >= bar.EmaSlow)
            {
                ResetSetup("CorrectiveSideInvalid");
                return false;
            }

            double retracement = ComputeRetracement(_impulse.Bias == Bias.Long ? _leg2.Low : _leg2.High);
            if (retracement < MinPullbackRetracement)
            {
                Block("SecondLegTooShallow");
                return false;
            }
            if (retracement > MaxPullbackRetracement)
            {
                Block("SecondLegTooDeep");
                return false;
            }

            double impulseMomentum = _impulse.Range / ImpulseBars;
            double leg2Move = _impulse.Bias == Bias.Long ? _separationHigh - _leg2.Low : _leg2.High - _separationLow;
            int leg2Bars = Math.Max(1, _leg2.EndBar - _leg2.StartBar + 1);
            double leg2Momentum = leg2Move / leg2Bars;
            if (impulseMomentum <= 0.0 || leg2Momentum > impulseMomentum * SecondLegMaxMomentumRatio)
            {
                Block("SecondLegTooStrong");
                return false;
            }

            return true;
        }

        private double ComputeLeg2MomentumRatio()
        {
            double impulseMomentum = _impulse.Range / ImpulseBars;
            double leg2Move = _impulse.Bias == Bias.Long ? _separationHigh - _leg2.Low : _leg2.High - _separationLow;
            int leg2Bars = Math.Max(1, _leg2.EndBar - _leg2.StartBar + 1);
            double leg2Momentum = leg2Move / leg2Bars;
            return impulseMomentum > 0.0 ? leg2Momentum / impulseMomentum : 0.0;
        }

        private void BuildPlannedSignal(BarSnapshot bar)
        {
            double entry;
            double stop;
            if (_impulse.Bias == Bias.Long)
            {
                entry = RoundToTick(bar.High + EntryOffsetTicks * TickSize);
                stop = RoundToTick(_leg2.Low - StopBufferTicks * TickSize);
            }
            else
            {
                entry = RoundToTick(bar.Low - EntryOffsetTicks * TickSize);
                stop = RoundToTick(_leg2.High + StopBufferTicks * TickSize);
            }

            double stopDistance = Math.Abs(entry - stop);
            double riskPerContract = stopDistance * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (riskPerContract <= 0.0 || quantity <= 0)
            {
                Block("RiskTooSmall");
                return;
            }
            if (bar.Atr > 0.0 && stopDistance / bar.Atr > MaxStopAtrMultiple)
            {
                Block("StopTooWide");
                return;
            }
            if (!HasStructureRoom(entry, stop, _impulse.Bias, bar, out string structure, out double roomToStructureR))
            {
                Block("StructureRoom");
                return;
            }

            double leg1Retracement = ComputeRetracement(_impulse.Bias == Bias.Long ? _leg1.Low : _leg1.High);
            double leg2Retracement = ComputeRetracement(_impulse.Bias == Bias.Long ? _leg2.Low : _leg2.High);
            double leg2MomentumRatio = ComputeLeg2MomentumRatio();

            _planned = new PlannedSignal
            {
                Bias = _impulse.Bias,
                Entry = entry,
                Stop = stop,
                SignalBar = bar.Index,
                ExpiryBar = bar.Index + MaxTriggerBars,
                SignalTime = bar.EndTime,
                Structure = structure,
                RiskDollars = riskPerContract,
                Quantity = quantity,
                AtrAtPlan = bar.Atr,
                StopAtrMultiple = bar.Atr > 0.0 ? stopDistance / bar.Atr : 0.0,
                ImpulseAtrMultiple = bar.Atr > 0.0 ? _impulse.Range / bar.Atr : 0.0,
                Leg1Retracement = leg1Retracement,
                Leg2Retracement = leg2Retracement,
                Leg2MomentumRatio = leg2MomentumRatio,
                TotalPullbackBars = bar.Index - _leg1.StartBar + 1,
                Leg1Bars = _leg1.EndBar - _leg1.StartBar + 1,
                Leg2Bars = _leg2.EndBar - _leg2.StartBar + 1,
                RoomToStructureR = roomToStructureR
            };

            _armedSignals++;
            if (_planned.Bias == Bias.Long)
                _longArmed++;
            else
                _shortArmed++;

            MonthStats month = GetMonth(bar.EndTime);
            month.Armed++;
            if (_planned.Bias == Bias.Long)
                month.LongArmed++;
            else
                month.ShortArmed++;

            _state = SetupState.WaitingForTrigger;
        }

        private void ObserveTriggerOrExpiry(BarSnapshot bar)
        {
            if (!_planned.IsActive)
            {
                ResetSetup("NoPlannedSignal", false);
                return;
            }

            if (bar.Index <= _planned.SignalBar)
                return;

            bool triggered = _planned.Bias == Bias.Long ? bar.High >= _planned.Entry : bar.Low <= _planned.Entry;
            if (triggered)
            {
                _triggeredSignals++;
                GetMonth(bar.EndTime).Triggered++;
                StartVirtualTrade(bar);
                ResetSetup("Triggered", false);
                return;
            }

            if (bar.Index > _planned.ExpiryBar)
            {
                _expiredSignals++;
                GetMonth(bar.EndTime).Expired++;
                ResetSetup("EntryExpired", false);
            }
        }

        private void StartVirtualTrade(BarSnapshot bar)
        {
            double risk = Math.Abs(_planned.Entry - _planned.Stop);
            if (risk <= 0.0)
                return;

            _virtualTrade = new VirtualTrade
            {
                Bias = _planned.Bias,
                Entry = _planned.Entry,
                Stop = _planned.Stop,
                OneR = _planned.Bias == Bias.Long ? _planned.Entry + risk : _planned.Entry - risk,
                TwoR = _planned.Bias == Bias.Long ? _planned.Entry + 2.0 * risk : _planned.Entry - 2.0 * risk,
                TriggerBar = bar.Index,
                ExpiryBar = bar.Index + MaxOutcomeBars,
                TriggerTime = bar.EndTime,
                Structure = _planned.Structure,
                RiskDollars = _planned.RiskDollars,
                Quantity = _planned.Quantity,
                AtrAtPlan = _planned.AtrAtPlan,
                StopAtrMultiple = _planned.StopAtrMultiple,
                ImpulseAtrMultiple = _planned.ImpulseAtrMultiple,
                Leg1Retracement = _planned.Leg1Retracement,
                Leg2Retracement = _planned.Leg2Retracement,
                Leg2MomentumRatio = _planned.Leg2MomentumRatio,
                TotalPullbackBars = _planned.TotalPullbackBars,
                Leg1Bars = _planned.Leg1Bars,
                Leg2Bars = _planned.Leg2Bars,
                RoomToStructureR = _planned.RoomToStructureR,
                SignalTime = _planned.SignalTime,
                TradeId = _virtualTrades + 1
            };

            _virtualTrades++;
            Debug($"VIRTUAL_TRADE_OPEN time={bar.EndTime:yyyy-MM-dd HH:mm} side={_virtualTrade.Bias} entry={_virtualTrade.Entry:0.00} stop={_virtualTrade.Stop:0.00} oneR={_virtualTrade.OneR:0.00} twoR={_virtualTrade.TwoR:0.00} structure={_virtualTrade.Structure}");
            ObserveVirtualTrade(bar);
        }

        private void ObserveVirtualTrade(BarSnapshot bar)
        {
            if (!_virtualTrade.IsActive || bar.Index < _virtualTrade.TriggerBar)
                return;

            bool stopHit = _virtualTrade.Bias == Bias.Long ? bar.Low <= _virtualTrade.Stop : bar.High >= _virtualTrade.Stop;
            bool oneRHit = _virtualTrade.Bias == Bias.Long ? bar.High >= _virtualTrade.OneR : bar.Low <= _virtualTrade.OneR;
            bool twoRHit = _virtualTrade.Bias == Bias.Long ? bar.High >= _virtualTrade.TwoR : bar.Low <= _virtualTrade.TwoR;

            if (oneRHit && !_virtualTrade.TouchedOneR)
            {
                _virtualTrade.TouchedOneR = true;
                _touchOneR++;
            }

            if (stopHit)
            {
                CloseVirtualTrade(bar, "Stop", -1.0);
                return;
            }

            if (twoRHit)
            {
                CloseVirtualTrade(bar, "TwoR", 2.0);
                return;
            }

            if (bar.Index > _virtualTrade.ExpiryBar || bar.EndTime.TimeOfDay >= new TimeSpan(15, 55, 0))
            {
                double risk = Math.Max(TickSize, Math.Abs(_virtualTrade.Entry - _virtualTrade.Stop));
                double openR = _virtualTrade.Bias == Bias.Long
                    ? (bar.Close - _virtualTrade.Entry) / risk
                    : (_virtualTrade.Entry - bar.Close) / risk;
                CloseVirtualTrade(bar, "Timeout", openR);
            }
        }

        private void CloseVirtualTrade(BarSnapshot bar, string reason, double? rMultiple = null)
        {
            if (!_virtualTrade.IsActive)
                return;

            double realizedR = rMultiple ?? 0.0;
            if (reason == "TwoR")
                _winsTwoR++;
            else if (reason == "Stop")
                _stops++;
            else
                _timeouts++;

            _netR += realizedR;
            AppendTradeExport(bar, reason, realizedR);
            Debug($"VIRTUAL_TRADE_CLOSE time={bar.EndTime:yyyy-MM-dd HH:mm} reason={reason} side={_virtualTrade.Bias} r={realizedR:0.00} touched1R={_virtualTrade.TouchedOneR}");
            _virtualTrade = new VirtualTrade();
        }

        private void AppendTradeExport(BarSnapshot bar, string outcome, double rMultiple)
        {
            int barsHeld = Math.Max(0, bar.Index - _virtualTrade.TriggerBar + 1);
            _tradeExport.AppendLine(string.Join(",", new[]
            {
                _virtualTrade.TradeId.ToString(CultureInfo.InvariantCulture),
                CsvTime(_virtualTrade.SignalTime),
                CsvTime(_virtualTrade.TriggerTime),
                CsvTime(bar.EndTime),
                _virtualTrade.Bias.ToString(),
                N(_virtualTrade.Entry),
                N(_virtualTrade.Stop),
                N(_virtualTrade.OneR),
                N(_virtualTrade.TwoR),
                N(Math.Abs(_virtualTrade.Entry - _virtualTrade.Stop)),
                N(_virtualTrade.RiskDollars),
                N(_virtualTrade.Quantity),
                N(_virtualTrade.AtrAtPlan),
                N(_virtualTrade.StopAtrMultiple),
                N(_virtualTrade.ImpulseAtrMultiple),
                N(_virtualTrade.Leg1Retracement),
                N(_virtualTrade.Leg2Retracement),
                N(_virtualTrade.Leg2MomentumRatio),
                _virtualTrade.TotalPullbackBars.ToString(CultureInfo.InvariantCulture),
                _virtualTrade.Leg1Bars.ToString(CultureInfo.InvariantCulture),
                _virtualTrade.Leg2Bars.ToString(CultureInfo.InvariantCulture),
                CsvText(_virtualTrade.Structure),
                N(_virtualTrade.RoomToStructureR),
                outcome,
                N(rMultiple),
                _virtualTrade.TouchedOneR ? "true" : "false",
                barsHeld.ToString(CultureInfo.InvariantCulture)
            }));

            string side = _virtualTrade.Bias == Bias.Long ? "L" : "S";
            _tradeRuntimeRows.Add(
                $"T{_virtualTrade.TradeId:00}:{CsvTime(_virtualTrade.TriggerTime).Substring(2)} {side} {outcome} r={N(rMultiple)} l2={N(_virtualTrade.Leg2MomentumRatio)} room={N(_virtualTrade.RoomToStructureR)} retr={N(_virtualTrade.Leg2Retracement)} bars={barsHeld}");
        }

        private void SaveTradeExport()
        {
            if (string.IsNullOrWhiteSpace(_tradeExportKey))
                return;

            bool saved = ObjectStore.Save(_tradeExportKey, _tradeExport.ToString());
            Debug($"TRADE_EXPORT_SAVE key={_tradeExportKey} rows={_virtualTrades} saved={saved}");
        }

        private void RefreshTrendContext(BarSnapshot bar)
        {
            bool atrRegimeValid = bar.Atr > 0.0 && bar.AtrRatio >= MinAtrRegimeRatio && bar.AtrRatio <= MaxAtrRegimeRatio;
            bool longTrend = atrRegimeValid && bar.Close > bar.EmaSlow && bar.SlopeAtrPct >= SlopeMinAtrPctPerBar;
            bool shortTrend = atrRegimeValid && bar.Close < bar.EmaSlow && bar.SlopeAtrPct <= -SlopeMinAtrPctPerBar;
            if (longTrend && !shortTrend)
                _activeBias = Bias.Long;
            else if (shortTrend && !longTrend)
                _activeBias = Bias.Short;
            else
                _activeBias = Bias.Neutral;
        }

        private bool HasStructureRoom(double entry, double stop, Bias bias, BarSnapshot bar, out string label, out double roomToStructureR)
        {
            label = "clear";
            roomToStructureR = 999.0;
            double risk = Math.Max(TickSize, Math.Abs(entry - stop));
            double required = risk * MinRoomToStructureR;
            var levels = BuildStructureLevels(bar);

            if (bias == Bias.Long)
            {
                var resistance = levels.Where(x => x.Price > entry && x.Kind != StructureKind.Support)
                    .OrderBy(x => x.Price)
                    .FirstOrDefault();
                if (resistance == null)
                    return true;
                label = resistance.Label;
                roomToStructureR = (resistance.Price - entry) / risk;
                return resistance.Price - entry >= required;
            }

            var support = levels.Where(x => x.Price < entry && x.Kind != StructureKind.Resistance)
                .OrderByDescending(x => x.Price)
                .FirstOrDefault();
            if (support == null)
                return true;
            label = support.Label;
            roomToStructureR = (entry - support.Price) / risk;
            return entry - support.Price >= required;
        }

        private List<StructureLevel> BuildStructureLevels(BarSnapshot bar)
        {
            var levels = new List<StructureLevel>();
            if (!double.IsNaN(_priorRthHigh))
                levels.Add(new StructureLevel("PDH", _priorRthHigh, StructureKind.Resistance));
            if (!double.IsNaN(_priorRthLow))
                levels.Add(new StructureLevel("PDL", _priorRthLow, StructureKind.Support));
            if (_openingRangeComplete && !double.IsNaN(_openingRangeHigh))
                levels.Add(new StructureLevel("ORH", _openingRangeHigh, StructureKind.Resistance));
            if (_openingRangeComplete && !double.IsNaN(_openingRangeLow))
                levels.Add(new StructureLevel("ORL", _openingRangeLow, StructureKind.Support));

            if (_bars.Count > SwingLookbackBars)
            {
                var prior = _bars.Take(_bars.Count - 1).Reverse().Take(SwingLookbackBars).ToList();
                levels.Add(new StructureLevel("SWING_H", prior.Max(x => x.High), StructureKind.Resistance));
                levels.Add(new StructureLevel("SWING_L", prior.Min(x => x.Low), StructureKind.Support));
            }

            return levels;
        }

        private BarSnapshot BuildBarSnapshot(TradeBar input)
        {
            double open = (double)input.Open;
            double high = (double)input.High;
            double low = (double)input.Low;
            double close = (double)input.Close;
            double previousClose = _bars.Count > 0 ? _bars[_bars.Count - 1].Close : close;
            double trueRange = Math.Max(high - low, Math.Max(Math.Abs(high - previousClose), Math.Abs(low - previousClose)));
            _trueRanges.Add(trueRange);

            double atr = AverageLast(_trueRanges, AtrPeriod);
            _atrValues.Add(atr);
            double atrBaseline = AverageLast(_atrValues, AtrRegimeLookback);
            double emaFast = _bars.Count == 0 ? close : UpdateEma(_bars[_bars.Count - 1].EmaFast, close, TrendEmaFast);
            double emaSlow = _bars.Count == 0 ? close : UpdateEma(_bars[_bars.Count - 1].EmaSlow, close, TrendEmaSlow);
            double slope = 0.0;
            if (_bars.Count >= SlopeLookbackBars && atr > 0.0)
                slope = ((emaFast - _bars[_bars.Count - SlopeLookbackBars].EmaFast) / SlopeLookbackBars) / Math.Max(atr, TickSize);

            return new BarSnapshot
            {
                Index = _bars.Count,
                EndTime = input.EndTime,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Atr = atr,
                AtrRatio = atrBaseline > 0.0 ? atr / atrBaseline : 0.0,
                EmaFast = emaFast,
                EmaSlow = emaSlow,
                SlopeAtrPct = slope
            };
        }

        private void UpdateSessionState(BarSnapshot bar)
        {
            if (_sessionDate != bar.EndTime.Date)
            {
                if (!double.IsNaN(_sessionHigh) && !double.IsNaN(_sessionLow))
                {
                    _priorRthHigh = _sessionHigh;
                    _priorRthLow = _sessionLow;
                }

                _sessionDate = bar.EndTime.Date;
                _sessionHigh = bar.High;
                _sessionLow = bar.Low;
                _openingRangeHigh = double.NaN;
                _openingRangeLow = double.NaN;
                _openingRangeComplete = false;
                ResetSetup("NewSession", false);
            }
            else
            {
                _sessionHigh = Math.Max(_sessionHigh, bar.High);
                _sessionLow = Math.Min(_sessionLow, bar.Low);
            }

            int minutesFromOpen = (int)(bar.EndTime.TimeOfDay - new TimeSpan(9, 30, 0)).TotalMinutes;
            if (minutesFromOpen <= OpeningRangeMinutes)
            {
                _openingRangeHigh = double.IsNaN(_openingRangeHigh) ? bar.High : Math.Max(_openingRangeHigh, bar.High);
                _openingRangeLow = double.IsNaN(_openingRangeLow) ? bar.Low : Math.Min(_openingRangeLow, bar.Low);
            }
            if (minutesFromOpen >= OpeningRangeMinutes)
                _openingRangeComplete = !double.IsNaN(_openingRangeHigh) && !double.IsNaN(_openingRangeLow);
        }

        private bool IndicatorsReady(BarSnapshot bar)
        {
            return _bars.Count >= Math.Max(TrendEmaSlow, AtrRegimeLookback)
                && bar.Atr > 0.0
                && bar.AtrRatio > 0.0;
        }

        private double ComputeRetracement(double pullbackExtreme)
        {
            if (_impulse.Range <= 0.0)
                return 1.0;
            if (_impulse.Bias == Bias.Short)
                return (pullbackExtreme - _impulse.Low) / Math.Max(_impulse.Range, TickSize);
            return (_impulse.High - pullbackExtreme) / Math.Max(_impulse.Range, TickSize);
        }

        private void ResetSetup(string reason, bool countBlock = true)
        {
            if (countBlock)
                Block(reason);
            _state = SetupState.SeekingImpulse;
            _activeBias = Bias.Neutral;
            _impulse = new ImpulseSnapshot();
            _leg1 = new PullbackSnapshot();
            _leg2 = new PullbackSnapshot();
            _planned = new PlannedSignal();
            _separationHigh = double.NaN;
            _separationLow = double.NaN;
            _separationBar = -1;
        }

        private void Block(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;
            _blocks.TryGetValue(reason, out int count);
            _blocks[reason] = count + 1;
        }

        private int BlockCount(string reason)
        {
            return _blocks.TryGetValue(reason, out int count) ? count : 0;
        }

        private DateTime DateParameter(string key, DateTime fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            return DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value)
                ? value
                : fallback;
        }

        private double DoubleParameter(string key, double fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }

        private int IntParameter(string key, int fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        private MonthStats GetMonth(DateTime time)
        {
            string key = time.ToString("yyyy-MM");
            if (!_monthly.TryGetValue(key, out MonthStats stats))
            {
                stats = new MonthStats();
                _monthly[key] = stats;
            }
            return stats;
        }

        private BarSnapshot PreviousBar()
        {
            return _bars[_bars.Count - 2];
        }

        private static bool IsRegularSession(DateTime time)
        {
            int hhmm = time.Hour * 100 + time.Minute;
            return hhmm >= 930 && hhmm <= 1555;
        }

        private static double RoundToTick(double value)
        {
            return Math.Round(value / TickSize, MidpointRounding.AwayFromZero) * TickSize;
        }

        private static double UpdateEma(double previousEma, double value, int period)
        {
            double alpha = 2.0 / (period + 1.0);
            return previousEma + alpha * (value - previousEma);
        }

        private static double AverageLast(List<double> values, int count)
        {
            if (values.Count == 0)
                return 0.0;
            int take = Math.Min(count, values.Count);
            double sum = 0.0;
            for (int i = values.Count - take; i < values.Count; i++)
                sum += values[i];
            return sum / take;
        }

        private static string N(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string CsvTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static string CsvText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Contains(",") || value.Contains("\"")
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        private static string ParamToken(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture).Replace(".", "p");
        }

        private enum Bias { Neutral, Long, Short }
        private enum SetupState { SeekingImpulse, TrackingPullbackLeg1, TrackingSeparation, TrackingPullbackLeg2, WaitingForSignalBar, WaitingForTrigger }
        private enum StructureKind { Support, Resistance }

        private sealed class BarSnapshot
        {
            public int Index;
            public DateTime EndTime;
            public double Open;
            public double High;
            public double Low;
            public double Close;
            public double Atr;
            public double AtrRatio;
            public double EmaFast;
            public double EmaSlow;
            public double SlopeAtrPct;
        }

        private sealed class ImpulseSnapshot
        {
            public int StartBar = -1;
            public int EndBar = -1;
            public double High;
            public double Low;
            public double Range;
            public Bias Bias = Bias.Neutral;
        }

        private sealed class PullbackSnapshot
        {
            public int StartBar = -1;
            public int EndBar = -1;
            public double High;
            public double Low;
            public bool IsActive => StartBar >= 0;

            public static PullbackSnapshot FromBar(BarSnapshot bar)
            {
                return new PullbackSnapshot
                {
                    StartBar = bar.Index,
                    EndBar = bar.Index,
                    High = bar.High,
                    Low = bar.Low
                };
            }

            public void Absorb(BarSnapshot bar)
            {
                if (!IsActive)
                    return;
                EndBar = bar.Index;
                High = Math.Max(High, bar.High);
                Low = Math.Min(Low, bar.Low);
            }
        }

        private sealed class PlannedSignal
        {
            public Bias Bias = Bias.Neutral;
            public double Entry;
            public double Stop;
            public int SignalBar = -1;
            public int ExpiryBar = -1;
            public DateTime SignalTime;
            public string Structure = string.Empty;
            public double RiskDollars;
            public double Quantity;
            public double AtrAtPlan;
            public double StopAtrMultiple;
            public double ImpulseAtrMultiple;
            public double Leg1Retracement;
            public double Leg2Retracement;
            public double Leg2MomentumRatio;
            public int TotalPullbackBars;
            public int Leg1Bars;
            public int Leg2Bars;
            public double RoomToStructureR;
            public bool IsActive => SignalBar >= 0 && Bias != Bias.Neutral;
        }

        private sealed class VirtualTrade
        {
            public int TradeId;
            public Bias Bias = Bias.Neutral;
            public double Entry;
            public double Stop;
            public double OneR;
            public double TwoR;
            public int TriggerBar = -1;
            public int ExpiryBar = -1;
            public DateTime SignalTime;
            public DateTime TriggerTime;
            public string Structure = string.Empty;
            public double RiskDollars;
            public double Quantity;
            public double AtrAtPlan;
            public double StopAtrMultiple;
            public double ImpulseAtrMultiple;
            public double Leg1Retracement;
            public double Leg2Retracement;
            public double Leg2MomentumRatio;
            public int TotalPullbackBars;
            public int Leg1Bars;
            public int Leg2Bars;
            public double RoomToStructureR;
            public bool TouchedOneR;
            public bool IsActive => TriggerBar >= 0 && Bias != Bias.Neutral;
        }

        private sealed class StructureLevel
        {
            public StructureLevel(string label, double price, StructureKind kind)
            {
                Label = label;
                Price = price;
                Kind = kind;
            }

            public string Label;
            public double Price;
            public StructureKind Kind;
        }

        private sealed class MonthStats
        {
            public int Bars;
            public int Armed;
            public int LongArmed;
            public int ShortArmed;
            public int Triggered;
            public int Expired;
        }
    }
}
