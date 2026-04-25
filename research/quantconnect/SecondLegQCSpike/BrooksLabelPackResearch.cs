using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private int LabelPackMaxSetups = 100;
        private int LabelPackBeforeBars = 36;
        private int LabelPackAfterBars = 36;
        private int LabelPackMeasureMinutes = 60;
        private double LabelPackMinMoveAtr = 0.75;
        private int LabelPackMinStrongBars = 3;
        private bool LabelPackExportContext = true;
        private double LabelPackContextMinMoveAtr = 0.25;
        private int LabelPackContextMinStrongBars = 1;
        private readonly List<LabelWindow> _labelWindows = new List<LabelWindow>();
        private readonly HashSet<string> _labelSetups = new HashSet<string>();
        private bool _labelMeasured;
        private bool _labelContextExported;
        private Bias _labelBias = Bias.Neutral;
        private int _labelStrongBullBars;
        private int _labelStrongBearBars;
        private double _labelFirstHourMoveAtr;
        private double _labelSessionHigh = double.NaN;
        private double _labelSessionLow = double.NaN;
        private bool _labelPullbackSeen;
        private double _labelPullbackHigh = double.NaN;
        private double _labelPullbackLow = double.NaN;

        private void ConfigureBrooksLabelPackResearch(DateTime startDate, DateTime endDate)
        {
            if (MomentumResearchMode != "labelpack")
                return;

            LabelPackMaxSetups = Math.Max(1, IntParameter("labelPackMaxSetups", LabelPackMaxSetups));
            LabelPackBeforeBars = Math.Max(5, IntParameter("labelPackBeforeBars", LabelPackBeforeBars));
            LabelPackAfterBars = Math.Max(5, IntParameter("labelPackAfterBars", LabelPackAfterBars));
            LabelPackMeasureMinutes = Math.Max(OpeningRangeMinutes, IntParameter("labelPackMeasureMinutes", LabelPackMeasureMinutes));
            LabelPackMinMoveAtr = Math.Max(0.0, DoubleParameter("labelPackMinMoveAtr", LabelPackMinMoveAtr));
            LabelPackMinStrongBars = Math.Max(1, IntParameter("labelPackMinStrongBars", LabelPackMinStrongBars));
            LabelPackExportContext = BoolParameter("labelPackExportContext", LabelPackExportContext);
            LabelPackContextMinMoveAtr = Math.Max(0.0, DoubleParameter("labelPackContextMinMoveAtr", LabelPackContextMinMoveAtr));
            LabelPackContextMinStrongBars = Math.Max(0, IntParameter("labelPackContextMinStrongBars", LabelPackContextMinStrongBars));
            _tradeExportKey = $"{ProjectId}/brooks_label_pack_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_setups_{LabelPackMaxSetups}_measure_{LabelPackMeasureMinutes}_move_{ParamToken(LabelPackMinMoveAtr)}_strong_{LabelPackMinStrongBars}_ctx_{(LabelPackExportContext ? 1 : 0)}_{ParamToken(LabelPackContextMinMoveAtr)}.csv";
            _tradeExport.Clear();
            _tradeExport.AppendLine("setupId,setupType,side,setupTime,barTime,barOffset,open,high,low,close,atr,emaFast,emaSlow,minutesFromOpen,sessionOpen,sessionHighAtSetup,sessionLowAtSetup,openingRangeHighAtSetup,openingRangeLowAtSetup,firstHourMoveAtr,strongBullBars,strongBearBars");
        }

        private void ResetBrooksLabelPackSession()
        {
            _labelMeasured = false;
            _labelContextExported = false;
            _labelBias = Bias.Neutral;
            _labelStrongBullBars = 0;
            _labelStrongBearBars = 0;
            _labelFirstHourMoveAtr = 0.0;
            _labelSessionHigh = double.NaN;
            _labelSessionLow = double.NaN;
            _labelPullbackSeen = false;
            _labelPullbackHigh = double.NaN;
            _labelPullbackLow = double.NaN;
        }

        private void TryBrooksLabelPackResearch(BarSnapshot bar)
        {
            FlushCompletedLabelWindows(bar);
            if (_labelSetups.Count >= LabelPackMaxSetups)
                return;

            int minutes = MinutesFromOpen(bar);
            _labelSessionHigh = double.IsNaN(_labelSessionHigh) ? bar.High : Math.Max(_labelSessionHigh, bar.High);
            _labelSessionLow = double.IsNaN(_labelSessionLow) ? bar.Low : Math.Min(_labelSessionLow, bar.Low);
            if (IsLabelStrongBar(bar, Bias.Long))
                _labelStrongBullBars++;
            if (IsLabelStrongBar(bar, Bias.Short))
                _labelStrongBearBars++;

            if (!_labelMeasured)
            {
                if (minutes < LabelPackMeasureMinutes || double.IsNaN(_odSessionOpen) || bar.Atr <= 0.0)
                    return;
                _labelFirstHourMoveAtr = (bar.Close - _odSessionOpen) / Math.Max(bar.Atr, TickSize);
                if (_labelFirstHourMoveAtr >= LabelPackMinMoveAtr && _labelStrongBullBars >= LabelPackMinStrongBars)
                    _labelBias = Bias.Long;
                else if (_labelFirstHourMoveAtr <= -LabelPackMinMoveAtr && _labelStrongBearBars >= LabelPackMinStrongBars)
                    _labelBias = Bias.Short;
                else
                    Block("LabelNoTfo");
                TryCreateContextLabelWindow(bar);
                _labelMeasured = true;
                return;
            }

            if (_labelBias == Bias.Neutral || minutes <= LabelPackMeasureMinutes || _bars.Count < 2)
                return;

            BarSnapshot previous = PreviousBar();
            if (_labelBias == Bias.Long)
                TryLabelLongTfo(bar, previous);
            else
                TryLabelShortTfo(bar, previous);
        }

        private void TryLabelLongTfo(BarSnapshot bar, BarSnapshot previous)
        {
            if (!_labelPullbackSeen && _labelSessionHigh - bar.Low >= 0.25 * Math.Max(bar.Atr, TickSize))
            {
                _labelPullbackSeen = true;
                _labelPullbackHigh = bar.High;
                _labelPullbackLow = bar.Low;
            }
            if (!_labelPullbackSeen)
                return;

            _labelPullbackHigh = Math.Max(_labelPullbackHigh, bar.High);
            _labelPullbackLow = Math.Min(_labelPullbackLow, bar.Low);
            double retrace = (_labelSessionHigh - _labelPullbackLow) / Math.Max(TickSize, _labelSessionHigh - _odSessionOpen);
            if (retrace > 0.85)
                return;
            if (bar.Close > previous.High)
                CreateLabelWindow("TFO_PULLBACK", Bias.Long, bar);
        }

        private void TryLabelShortTfo(BarSnapshot bar, BarSnapshot previous)
        {
            if (!_labelPullbackSeen && bar.High - _labelSessionLow >= 0.25 * Math.Max(bar.Atr, TickSize))
            {
                _labelPullbackSeen = true;
                _labelPullbackHigh = bar.High;
                _labelPullbackLow = bar.Low;
            }
            if (!_labelPullbackSeen)
                return;

            _labelPullbackHigh = Math.Max(_labelPullbackHigh, bar.High);
            _labelPullbackLow = Math.Min(_labelPullbackLow, bar.Low);
            double retrace = (_labelPullbackHigh - _labelSessionLow) / Math.Max(TickSize, _odSessionOpen - _labelSessionLow);
            if (retrace > 0.85)
                return;
            if (bar.Close < previous.Low)
                CreateLabelWindow("TFO_PULLBACK", Bias.Short, bar);
        }

        private void TryCreateContextLabelWindow(BarSnapshot bar)
        {
            if (!LabelPackExportContext || _labelContextExported)
                return;

            Bias contextBias = _labelBias;
            if (contextBias == Bias.Neutral)
            {
                if (_labelFirstHourMoveAtr >= LabelPackContextMinMoveAtr && _labelStrongBullBars >= LabelPackContextMinStrongBars)
                    contextBias = Bias.Long;
                else if (_labelFirstHourMoveAtr <= -LabelPackContextMinMoveAtr && _labelStrongBearBars >= LabelPackContextMinStrongBars)
                    contextBias = Bias.Short;
            }

            if (contextBias == Bias.Neutral)
                return;

            _labelContextExported = true;
            CreateLabelWindow("FIRST_HOUR_CONTEXT", contextBias, bar);
            Block("LabelContext");
        }

        private void CreateLabelWindow(string setupType, Bias bias, BarSnapshot setupBar)
        {
            string setupId = $"{setupBar.EndTime:yyyyMMdd_HHmm}_{setupType}_{bias}_{_labelSetups.Count + 1:000}";
            if (!_labelSetups.Add(setupId))
                return;

            var window = new LabelWindow
            {
                SetupId = setupId,
                SetupType = setupType,
                Bias = bias,
                SetupBar = setupBar.Index,
                EndBar = setupBar.Index + LabelPackAfterBars,
                SetupTime = setupBar.EndTime,
                SessionOpen = _odSessionOpen,
                SessionHighAtSetup = _labelSessionHigh,
                SessionLowAtSetup = _labelSessionLow,
                OpeningRangeHighAtSetup = _openingRangeHigh,
                OpeningRangeLowAtSetup = _openingRangeLow,
                FirstHourMoveAtr = _labelFirstHourMoveAtr,
                StrongBullBars = _labelStrongBullBars,
                StrongBearBars = _labelStrongBearBars
            };
            int first = Math.Max(0, setupBar.Index - LabelPackBeforeBars);
            foreach (var prior in _bars.Where(x => x.Index >= first && x.Index <= setupBar.Index))
                AppendLabelBar(window, prior);
            _labelWindows.Add(window);
            _labelPullbackSeen = false;
        }

        private void FlushCompletedLabelWindows(BarSnapshot bar)
        {
            for (int i = _labelWindows.Count - 1; i >= 0; i--)
            {
                var window = _labelWindows[i];
                if (bar.Index > window.SetupBar && bar.Index <= window.EndBar)
                    AppendLabelBar(window, bar);
                if (bar.Index >= window.EndBar)
                    _labelWindows.RemoveAt(i);
            }
        }

        private void AppendLabelBar(LabelWindow window, BarSnapshot bar)
        {
            _tradeExport.AppendLine(string.Join(",", new[]
            {
                window.SetupId,
                window.SetupType,
                window.Bias.ToString(),
                CsvTime(window.SetupTime),
                CsvTime(bar.EndTime),
                (bar.Index - window.SetupBar).ToString(CultureInfo.InvariantCulture),
                N(bar.Open),
                N(bar.High),
                N(bar.Low),
                N(bar.Close),
                N(bar.Atr),
                N(bar.EmaFast),
                N(bar.EmaSlow),
                MinutesFromOpen(bar).ToString(CultureInfo.InvariantCulture),
                N(window.SessionOpen),
                N(window.SessionHighAtSetup),
                N(window.SessionLowAtSetup),
                N(window.OpeningRangeHighAtSetup),
                N(window.OpeningRangeLowAtSetup),
                N(window.FirstHourMoveAtr),
                window.StrongBullBars.ToString(CultureInfo.InvariantCulture),
                window.StrongBearBars.ToString(CultureInfo.InvariantCulture)
            }));
        }

        private bool IsLabelStrongBar(BarSnapshot bar, Bias bias)
        {
            double closePct = CloseLocationPct(bar);
            double bodyPct = BodyPct(bar);
            if (bias == Bias.Long)
                return bar.Close > bar.Open && bodyPct >= 0.45 && closePct >= 0.65;
            return bar.Close < bar.Open && bodyPct >= 0.45 && closePct <= 0.35;
        }

        private sealed class LabelWindow
        {
            public string SetupId = string.Empty;
            public string SetupType = string.Empty;
            public Bias Bias = Bias.Neutral;
            public int SetupBar;
            public int EndBar;
            public DateTime SetupTime;
            public double SessionOpen;
            public double SessionHighAtSetup;
            public double SessionLowAtSetup;
            public double OpeningRangeHighAtSetup;
            public double OpeningRangeLowAtSetup;
            public double FirstHourMoveAtr;
            public int StrongBullBars;
            public int StrongBearBars;
        }
    }
}
