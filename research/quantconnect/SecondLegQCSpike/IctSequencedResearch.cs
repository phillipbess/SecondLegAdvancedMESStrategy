using System;
using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private DateTime _ictSequencedEntryExpiry = DateTime.MinValue;
        private DateTime _ictSequencedTradeExpiry = DateTime.MinValue;

        public override void OnData(Slice slice)
        {
            if (!IsIctSequencedMode() || _continuousSymbol == null)
                return;
            if (!slice.Bars.TryGetValue(_continuousSymbol, out TradeBar bar))
                return;
            if (!IsRegularSession(bar.EndTime))
                return;

            ProcessIctSequencedExecution(bar);
        }

        private bool IsIctSequencedMode()
        {
            return string.Equals(EntryMode, "ictsequenced", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "ictseq", StringComparison.OrdinalIgnoreCase);
        }

        private void TryIctSequencedSignal(BarSnapshot bar)
        {
            ResetIctSessionIfNeeded(bar);

            if (_virtualTrade.IsActive || _ictPending.IsActive)
                return;

            if (IctOneTradePerDay && _ictTradedToday)
                return;

            int minutes = MinutesFromOpen(bar);
            if (minutes < IctWindowStartMinutes || minutes > IctWindowEndMinutes)
                return;

            if (_ictSweep.IsActive && bar.Index - _ictSweep.Bar > IctMaxBarsAfterSweep)
                _ictSweep = new IctSweep();

            if (!_ictSweep.IsActive)
            {
                TryCaptureIctSweep(bar);
                return;
            }

            TryCaptureIctDisplacementAndFvg(bar);
            if (_ictPending.IsActive)
                _ictSequencedEntryExpiry = bar.EndTime.AddMinutes(IctMaxBarsAfterFvg * BarMinutes);
        }

        private void ProcessIctSequencedExecution(TradeBar input)
        {
            if (_virtualTrade.IsActive)
            {
                ObserveIctSequencedTrade(input);
                return;
            }

            if (!_ictPending.IsActive)
                return;

            if (input.EndTime > _ictSequencedEntryExpiry)
            {
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                _expiredSignals++;
                GetMonth(input.EndTime).Expired++;
                return;
            }

            bool touched = _ictPending.Bias == Bias.Long
                ? (double)input.Low <= _ictPending.Entry
                : (double)input.High >= _ictPending.Entry;
            if (!touched)
                return;

            StartIctSequencedTrade(input);
        }

        private void StartIctSequencedTrade(TradeBar input)
        {
            if (!SideAllowed(_ictPending.Bias))
            {
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                return;
            }

            double risk = Math.Abs(_ictPending.Entry - _ictPending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("IctSeqRiskTooSmall");
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                return;
            }
            if (_ictPending.AtrAtSignal > 0.0 && risk / _ictPending.AtrAtSignal > MaxStopAtrMultiple)
            {
                Block("IctSeqStopTooWide");
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                return;
            }

            _triggeredSignals++;
            GetMonth(input.EndTime).Triggered++;
            _ictTradedToday = true;
            _virtualTrades++;
            _ictSequencedTradeExpiry = input.EndTime.AddMinutes(MaxOutcomeBars * BarMinutes);
            _virtualTrade = new VirtualTrade
            {
                TradeId = _virtualTrades,
                Bias = _ictPending.Bias,
                Entry = _ictPending.Entry,
                Stop = _ictPending.Stop,
                TouchProbePrice = _ictPending.Bias == Bias.Long ? _ictPending.Entry + TouchProbeR * risk : _ictPending.Entry - TouchProbeR * risk,
                TargetPrice = _ictPending.Bias == Bias.Long ? _ictPending.Entry + ProfitTargetR * risk : _ictPending.Entry - ProfitTargetR * risk,
                TargetR = ProfitTargetR,
                TriggerBar = _bars.Count,
                ExpiryBar = _bars.Count + MaxOutcomeBars,
                SignalTime = _ictPending.SignalTime,
                TriggerTime = input.EndTime,
                Structure = _ictPending.Structure + "_SEQ",
                RiskDollars = riskPerContract,
                Quantity = quantity,
                AtrAtPlan = _ictPending.AtrAtSignal,
                StopAtrMultiple = _ictPending.AtrAtSignal > 0.0 ? risk / _ictPending.AtrAtSignal : 0.0,
                SignalHour = _ictPending.SignalHour,
                MinutesFromOpen = _ictPending.MinutesFromOpen,
                SignalClosePct = _ictPending.SignalClosePct,
                SignalBodyPct = _ictPending.SignalBodyPct,
                AtrRatio = _ictPending.AtrRatio,
                SlopeAtrPct = _ictPending.SlopeAtrPct,
                MaxAdverseR = 0.0,
                MaxFavorableR = 0.0
            };

            Debug($"ICT_SEQ_OPEN time={input.EndTime:yyyy-MM-dd HH:mm:ss} side={_virtualTrade.Bias} entry={_virtualTrade.Entry:0.00} stop={_virtualTrade.Stop:0.00} target={_virtualTrade.TargetPrice:0.00} setup={_virtualTrade.Structure}");
            _ictPending = new IctPending();
            _ictSweep = new IctSweep();
        }

        private void ObserveIctSequencedTrade(TradeBar input)
        {
            if (input.EndTime <= _virtualTrade.TriggerTime)
                return;

            BarSnapshot bar = SequencedSnapshot(input);
            bool stopHit = _virtualTrade.Bias == Bias.Long ? bar.Low <= _virtualTrade.Stop : bar.High >= _virtualTrade.Stop;
            bool touchProbeHit = _virtualTrade.Bias == Bias.Long ? bar.High >= _virtualTrade.TouchProbePrice : bar.Low <= _virtualTrade.TouchProbePrice;
            bool targetHit = _virtualTrade.Bias == Bias.Long ? bar.High >= _virtualTrade.TargetPrice : bar.Low <= _virtualTrade.TargetPrice;
            UpdateExcursion(bar);

            if (touchProbeHit && !_virtualTrade.TouchedProbe)
            {
                _virtualTrade.TouchedProbe = true;
                _touchOneR++;
            }

            if (TryCloseAmbiguousOutcome(bar, stopHit, targetHit))
                return;

            if (stopHit)
            {
                CloseVirtualTrade(bar, "Stop", -1.0);
                return;
            }

            if (targetHit)
            {
                CloseVirtualTrade(bar, "Target", _virtualTrade.TargetR);
                return;
            }

            if (input.EndTime > _ictSequencedTradeExpiry || input.EndTime.TimeOfDay >= new TimeSpan(15, 55, 0))
            {
                double risk = Math.Max(TickSize, Math.Abs(_virtualTrade.Entry - _virtualTrade.Stop));
                double openR = _virtualTrade.Bias == Bias.Long
                    ? ((double)input.Close - _virtualTrade.Entry) / risk
                    : (_virtualTrade.Entry - (double)input.Close) / risk;
                CloseVirtualTrade(bar, "Timeout", openR);
            }
        }

        private BarSnapshot SequencedSnapshot(TradeBar input)
        {
            return new BarSnapshot
            {
                Index = _bars.Count,
                EndTime = input.EndTime,
                Open = (double)input.Open,
                High = (double)input.High,
                Low = (double)input.Low,
                Close = (double)input.Close,
                Volume = (double)input.Volume
            };
        }
    }
}
