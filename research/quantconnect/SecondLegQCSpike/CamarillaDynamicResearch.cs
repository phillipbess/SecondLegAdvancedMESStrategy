using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string CamarillaModel = "dynamic";
        private int CamarillaMinSignalMinutes = 15;
        private int CamarillaMaxSignalMinutes = 300;
        private int CamarillaSlopeLookbackBars = 6;
        private double CamarillaFlatSlopeThreshold = 0.015;
        private double CamarillaBreakoutSlopeThreshold = 0.025;
        private int CamarillaBreakoutConfirmBars = 2;
        private int CamarillaStopBufferTicks = 2;
        private string CamarillaStopMode = "level";
        private double CamarillaStopAtr = 1.25;
        private double CamarillaMinPriorRangeAtr = 0.50;
        private double CamarillaMaxPriorRangeAtr = 4.00;
        private bool CamarillaOneTradePerDay = true;

        private DateTime _camSessionDate = DateTime.MinValue;
        private double _camSessionHigh = double.NaN;
        private double _camSessionLow = double.NaN;
        private double _camSessionClose = double.NaN;
        private double _camPriorHigh = double.NaN;
        private double _camPriorLow = double.NaN;
        private double _camPriorClose = double.NaN;
        private double _camCumPv;
        private double _camCumVolume;
        private readonly List<double> _camVwapValues = new List<double>();
        private bool _camTradedToday;
        private int _camLongBreakCount;
        private int _camShortBreakCount;
        private CamarillaPending _camPending = new CamarillaPending();

        private void ConfigureCamarillaDynamicResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsCamarillaMode())
                return;

            CamarillaModel = TextParameter("camarillaModel", CamarillaModel).ToLowerInvariant();
            CamarillaMinSignalMinutes = Math.Max(0, IntParameter("camarillaMinSignalMinutes", CamarillaMinSignalMinutes));
            CamarillaMaxSignalMinutes = Math.Max(CamarillaMinSignalMinutes, IntParameter("camarillaMaxSignalMinutes", CamarillaMaxSignalMinutes));
            CamarillaSlopeLookbackBars = Math.Max(1, IntParameter("camarillaSlopeLookbackBars", CamarillaSlopeLookbackBars));
            CamarillaFlatSlopeThreshold = Math.Max(0.0, DoubleParameter("camarillaFlatSlopeThreshold", CamarillaFlatSlopeThreshold));
            CamarillaBreakoutSlopeThreshold = Math.Max(CamarillaFlatSlopeThreshold, DoubleParameter("camarillaBreakoutSlopeThreshold", CamarillaBreakoutSlopeThreshold));
            CamarillaBreakoutConfirmBars = Math.Max(1, IntParameter("camarillaBreakoutConfirmBars", CamarillaBreakoutConfirmBars));
            CamarillaStopBufferTicks = Math.Max(0, IntParameter("camarillaStopBufferTicks", CamarillaStopBufferTicks));
            CamarillaStopMode = TextParameter("camarillaStopMode", CamarillaStopMode).ToLowerInvariant();
            CamarillaStopAtr = Math.Max(0.25, DoubleParameter("camarillaStopAtr", CamarillaStopAtr));
            CamarillaMinPriorRangeAtr = Math.Max(0.0, DoubleParameter("camarillaMinPriorRangeAtr", CamarillaMinPriorRangeAtr));
            CamarillaMaxPriorRangeAtr = Math.Max(CamarillaMinPriorRangeAtr, DoubleParameter("camarillaMaxPriorRangeAtr", CamarillaMaxPriorRangeAtr));
            CamarillaOneTradePerDay = BoolParameter("camarillaOneTradePerDay", CamarillaOneTradePerDay);
            _tradeExportKey = $"{ProjectId}/camarilla_dynamic_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_model_{CamarillaModel}_stop_{CamarillaStopMode}_{ParamToken(CamarillaStopAtr)}_flat_{ParamToken(CamarillaFlatSlopeThreshold)}_break_{ParamToken(CamarillaBreakoutSlopeThreshold)}_confirm_{CamarillaBreakoutConfirmBars}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private bool IsCamarillaMode()
        {
            return string.Equals(EntryMode, "camarilla", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "camarilladynamic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "camdynamic", StringComparison.OrdinalIgnoreCase);
        }

        private void TryCamarillaDynamicResearch(BarSnapshot bar)
        {
            ResetCamarillaSessionIfNeeded(bar);
            UpdateCamarillaVwap(bar);

            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                {
                    UpdateCamarillaSessionBar(bar);
                    return;
                }
            }

            if (_camPending.IsActive)
            {
                StartCamarillaTrade(bar, _camPending);
                _camPending = new CamarillaPending();
                UpdateCamarillaSessionBar(bar);
                return;
            }

            if (CamarillaOneTradePerDay && _camTradedToday)
            {
                UpdateCamarillaSessionBar(bar);
                return;
            }

            if (!TryGetCamarillaLevels(out CamarillaLevels levels))
            {
                UpdateCamarillaSessionBar(bar);
                return;
            }

            int minutes = MinutesFromOpen(bar);
            if (minutes < CamarillaMinSignalMinutes || minutes > CamarillaMaxSignalMinutes)
            {
                UpdateCamarillaSessionBar(bar);
                return;
            }

            double priorRangeAtr = bar.Atr > 0.0 ? levels.Range / bar.Atr : 0.0;
            if (priorRangeAtr < CamarillaMinPriorRangeAtr || priorRangeAtr > CamarillaMaxPriorRangeAtr)
            {
                Block("CamPriorRangeFilter");
                UpdateCamarillaSessionBar(bar);
                return;
            }

            double slope = CamarillaSlopeAtr(bar);
            bool wantsFade = CamarillaModel == "fade" || (CamarillaModel == "dynamic" && Math.Abs(slope) <= CamarillaFlatSlopeThreshold);
            bool wantsBreakout = CamarillaModel == "breakout" || (CamarillaModel == "dynamic" && Math.Abs(slope) >= CamarillaBreakoutSlopeThreshold);

            if (wantsFade && TryArmCamarillaFade(bar, levels, slope))
            {
                UpdateCamarillaSessionBar(bar);
                return;
            }

            if (wantsBreakout)
                TryArmCamarillaBreakout(bar, levels, slope);

            UpdateCamarillaSessionBar(bar);
        }

        private void ResetCamarillaSessionIfNeeded(BarSnapshot bar)
        {
            if (_camSessionDate == bar.EndTime.Date)
                return;

            if (!double.IsNaN(_camSessionHigh) && !double.IsNaN(_camSessionLow) && !double.IsNaN(_camSessionClose))
            {
                _camPriorHigh = _camSessionHigh;
                _camPriorLow = _camSessionLow;
                _camPriorClose = _camSessionClose;
            }

            _camSessionDate = bar.EndTime.Date;
            _camSessionHigh = double.NaN;
            _camSessionLow = double.NaN;
            _camSessionClose = double.NaN;
            _camCumPv = 0.0;
            _camCumVolume = 0.0;
            _camVwapValues.Clear();
            _camTradedToday = false;
            _camLongBreakCount = 0;
            _camShortBreakCount = 0;
            _camPending = new CamarillaPending();
        }

        private void UpdateCamarillaSessionBar(BarSnapshot bar)
        {
            _camSessionHigh = double.IsNaN(_camSessionHigh) ? bar.High : Math.Max(_camSessionHigh, bar.High);
            _camSessionLow = double.IsNaN(_camSessionLow) ? bar.Low : Math.Min(_camSessionLow, bar.Low);
            _camSessionClose = bar.Close;
        }

        private void UpdateCamarillaVwap(BarSnapshot bar)
        {
            double volume = Math.Max(1.0, bar.Volume);
            double typical = (bar.High + bar.Low + bar.Close) / 3.0;
            _camCumPv += typical * volume;
            _camCumVolume += volume;
            _camVwapValues.Add(_camCumVolume > 0.0 ? _camCumPv / _camCumVolume : typical);
        }

        private bool TryGetCamarillaLevels(out CamarillaLevels levels)
        {
            levels = new CamarillaLevels();
            if (double.IsNaN(_camPriorHigh) || double.IsNaN(_camPriorLow) || double.IsNaN(_camPriorClose))
                return false;

            double range = _camPriorHigh - _camPriorLow;
            if (range <= TickSize)
                return false;

            levels.Range = range;
            levels.H3 = _camPriorClose + range * 1.1 / 4.0;
            levels.H4 = _camPriorClose + range * 1.1 / 2.0;
            levels.L3 = _camPriorClose - range * 1.1 / 4.0;
            levels.L4 = _camPriorClose - range * 1.1 / 2.0;
            return true;
        }

        private double CamarillaSlopeAtr(BarSnapshot bar)
        {
            if (_camVwapValues.Count <= CamarillaSlopeLookbackBars)
                return 0.0;

            double current = _camVwapValues[_camVwapValues.Count - 1];
            double prior = _camVwapValues[_camVwapValues.Count - 1 - CamarillaSlopeLookbackBars];
            double slopePerBar = (current - prior) / CamarillaSlopeLookbackBars;
            return slopePerBar / Math.Max(bar.Atr, TickSize);
        }

        private bool TryArmCamarillaFade(BarSnapshot bar, CamarillaLevels levels, double slope)
        {
            if (bar.High >= levels.H3 && bar.Close < levels.H3 && CloseLocationPct(bar) <= 0.55)
                return ArmCamarillaSignal(bar, Bias.Short, CamarillaStop(bar, Bias.Short, levels.H4 + CamarillaStopBufferTicks * TickSize), "CAM_H3_FADE", slope);
            if (bar.Low <= levels.L3 && bar.Close > levels.L3 && CloseLocationPct(bar) >= 0.45)
                return ArmCamarillaSignal(bar, Bias.Long, CamarillaStop(bar, Bias.Long, levels.L4 - CamarillaStopBufferTicks * TickSize), "CAM_L3_FADE", slope);
            return false;
        }

        private bool TryArmCamarillaBreakout(BarSnapshot bar, CamarillaLevels levels, double slope)
        {
            if (bar.Close > levels.H4 && slope >= CamarillaBreakoutSlopeThreshold)
            {
                _camLongBreakCount++;
                _camShortBreakCount = 0;
                if (_camLongBreakCount >= CamarillaBreakoutConfirmBars)
                    return ArmCamarillaSignal(bar, Bias.Long, CamarillaStop(bar, Bias.Long, levels.H4 - CamarillaStopBufferTicks * TickSize), "CAM_H4_BREAK", slope);
                return false;
            }
            if (bar.Close < levels.L4 && slope <= -CamarillaBreakoutSlopeThreshold)
            {
                _camShortBreakCount++;
                _camLongBreakCount = 0;
                if (_camShortBreakCount >= CamarillaBreakoutConfirmBars)
                    return ArmCamarillaSignal(bar, Bias.Short, CamarillaStop(bar, Bias.Short, levels.L4 + CamarillaStopBufferTicks * TickSize), "CAM_L4_BREAK", slope);
                return false;
            }

            _camLongBreakCount = 0;
            _camShortBreakCount = 0;
            return false;
        }

        private double CamarillaStop(BarSnapshot bar, Bias bias, double levelStop)
        {
            if (CamarillaStopMode == "atr")
                return bias == Bias.Long
                    ? bar.Close - CamarillaStopAtr * bar.Atr
                    : bar.Close + CamarillaStopAtr * bar.Atr;

            if (CamarillaStopMode == "tighter")
            {
                double atrStop = bias == Bias.Long
                    ? bar.Close - CamarillaStopAtr * bar.Atr
                    : bar.Close + CamarillaStopAtr * bar.Atr;
                return bias == Bias.Long
                    ? Math.Max(levelStop, atrStop)
                    : Math.Min(levelStop, atrStop);
            }

            return levelStop;
        }

        private bool ArmCamarillaSignal(BarSnapshot bar, Bias bias, double stop, string label, double slope)
        {
            if (!SideAllowed(bias))
                return false;

            _camPending = new CamarillaPending
            {
                Bias = bias,
                SignalBar = bar.Index,
                SignalTime = bar.EndTime,
                Stop = RoundToTick(stop),
                Structure = label,
                AtrAtSignal = bar.Atr,
                SignalHour = bar.EndTime.Hour,
                MinutesFromOpen = MinutesFromOpen(bar),
                SignalClosePct = CloseLocationPct(bar),
                SignalBodyPct = BodyPct(bar),
                AtrRatio = bar.AtrRatio,
                SlopeAtrPct = slope
            };

            _armedSignals++;
            if (bias == Bias.Long)
                _longArmed++;
            else
                _shortArmed++;

            MonthStats month = GetMonth(bar.EndTime);
            month.Armed++;
            if (bias == Bias.Long)
                month.LongArmed++;
            else
                month.ShortArmed++;
            return true;
        }

        private void StartCamarillaTrade(BarSnapshot bar, CamarillaPending pending)
        {
            double entry = RoundToTick(bar.Open);
            double risk = Math.Abs(entry - pending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("CamRiskTooSmall");
                return;
            }
            if (bar.Atr > 0.0 && risk / bar.Atr > MaxStopAtrMultiple)
            {
                Block("CamStopTooWide");
                return;
            }

            _triggeredSignals++;
            GetMonth(bar.EndTime).Triggered++;
            _camTradedToday = true;
            _virtualTrades++;
            _virtualTrade = new VirtualTrade
            {
                TradeId = _virtualTrades,
                Bias = pending.Bias,
                Entry = entry,
                Stop = pending.Stop,
                TouchProbePrice = pending.Bias == Bias.Long ? entry + TouchProbeR * risk : entry - TouchProbeR * risk,
                TargetPrice = pending.Bias == Bias.Long ? entry + ProfitTargetR * risk : entry - ProfitTargetR * risk,
                TargetR = ProfitTargetR,
                TriggerBar = bar.Index,
                ExpiryBar = bar.Index + MaxOutcomeBars,
                SignalTime = pending.SignalTime,
                TriggerTime = bar.EndTime,
                Structure = pending.Structure,
                RiskDollars = riskPerContract,
                Quantity = quantity,
                AtrAtPlan = pending.AtrAtSignal,
                StopAtrMultiple = bar.Atr > 0.0 ? risk / bar.Atr : 0.0,
                SignalHour = pending.SignalHour,
                MinutesFromOpen = pending.MinutesFromOpen,
                SignalClosePct = pending.SignalClosePct,
                SignalBodyPct = pending.SignalBodyPct,
                AtrRatio = pending.AtrRatio,
                SlopeAtrPct = pending.SlopeAtrPct,
                MaxAdverseR = 0.0,
                MaxFavorableR = 0.0
            };

            Debug($"CAM_TRADE_OPEN time={bar.EndTime:yyyy-MM-dd HH:mm} side={_virtualTrade.Bias} entry={entry:0.00} stop={pending.Stop:0.00} label={pending.Structure} slope={pending.SlopeAtrPct.ToString("0.####", CultureInfo.InvariantCulture)}");
            ObserveVirtualTrade(bar);
        }

        private struct CamarillaLevels
        {
            public double H3;
            public double H4;
            public double L3;
            public double L4;
            public double Range;
        }

        private sealed class CamarillaPending
        {
            public Bias Bias = Bias.Neutral;
            public int SignalBar = -1;
            public DateTime SignalTime;
            public double Stop;
            public string Structure = string.Empty;
            public double AtrAtSignal;
            public int SignalHour;
            public int MinutesFromOpen;
            public double SignalClosePct;
            public double SignalBodyPct;
            public double AtrRatio;
            public double SlopeAtrPct;
            public bool IsActive => SignalBar >= 0 && Bias != Bias.Neutral;
        }
    }
}
