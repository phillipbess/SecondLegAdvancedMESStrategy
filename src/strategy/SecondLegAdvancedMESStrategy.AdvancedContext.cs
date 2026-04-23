using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private void UpdateAdvancedSessionContext()
        {
            DateTime tradingDate = ClosedBarTime().Date;
            int hhmm = ClosedBarTimeHhmm();
            if (IsRthBar(hhmm))
            {
                if (_rthTradingDate == DateTime.MinValue || tradingDate != _rthTradingDate)
                    RollRthTradingDate(tradingDate);

                _currentRthHigh = double.IsNaN(_currentRthHigh) ? ClosedBarHigh() : Math.Max(_currentRthHigh, ClosedBarHigh());
                _currentRthLow = double.IsNaN(_currentRthLow) ? ClosedBarLow() : Math.Min(_currentRthLow, ClosedBarLow());

                int openingRangeEnd = AddMinutesToHhmm(RthOpenHhmm, OpeningRangeMinutes);
                if (UseOpeningRange && hhmm >= RthOpenHhmm && hhmm < openingRangeEnd)
                {
                    _openingRangeHigh = double.IsNaN(_openingRangeHigh) ? ClosedBarHigh() : Math.Max(_openingRangeHigh, ClosedBarHigh());
                    _openingRangeLow = double.IsNaN(_openingRangeLow) ? ClosedBarLow() : Math.Min(_openingRangeLow, ClosedBarLow());
                }
                else if (UseOpeningRange && hhmm >= openingRangeEnd && !double.IsNaN(_openingRangeHigh) && !double.IsNaN(_openingRangeLow))
                {
                    _openingRangeComplete = true;
                }
            }

            RefreshStructureLevels();
        }

        private void RollRthTradingDate(DateTime tradingDate)
        {
            if (_rthTradingDate != DateTime.MinValue && !double.IsNaN(_currentRthHigh) && !double.IsNaN(_currentRthLow))
            {
                _priorRthHigh = _currentRthHigh;
                _priorRthLow = _currentRthLow;
            }

            _rthTradingDate = tradingDate;
            _currentRthHigh = double.NaN;
            _currentRthLow = double.NaN;
            _openingRangeHigh = double.NaN;
            _openingRangeLow = double.NaN;
            _openingRangeComplete = false;
        }

        private void RefreshStructureLevels()
        {
            _structureLevels.Clear();

            if (UsePriorDayHighLow)
            {
                AddStructureLevel(StructureLevelKind.PriorDayHigh, _priorRthHigh, "PDH");
                AddStructureLevel(StructureLevelKind.PriorDayLow, _priorRthLow, "PDL");
            }

            if (UseOpeningRange && _openingRangeComplete)
            {
                AddStructureLevel(StructureLevelKind.OpeningRangeHigh, _openingRangeHigh, "ORH");
                AddStructureLevel(StructureLevelKind.OpeningRangeLow, _openingRangeLow, "ORL");
            }

            AddStructureLevel(StructureLevelKind.SwingHigh, HighestHigh(SwingLookbackBars, 1), "SWING_H");
            AddStructureLevel(StructureLevelKind.SwingLow, LowestLow(SwingLookbackBars, 1), "SWING_L");
        }

        private void AddStructureLevel(StructureLevelKind kind, double price, string label)
        {
            if (double.IsNaN(price) || price <= 0.0)
                return;

            _structureLevels.Add(new StructureLevel
            {
                Kind = kind,
                Price = Instrument.MasterInstrument.RoundToTickSize(price),
                Label = label,
            });
        }

        private int AddMinutesToHhmm(int hhmm, int minutesToAdd)
        {
            int hours = hhmm / 100;
            int minutes = hhmm % 100;
            DateTime anchor = new DateTime(2000, 1, 1, hours, minutes, 0);
            DateTime result = anchor.AddMinutes(minutesToAdd);
            return (result.Hour * 100) + result.Minute;
        }

        private bool IsRthBar(int hhmm)
        {
            return hhmm >= RthOpenHhmm && hhmm < RthCloseHhmm;
        }

        private bool ClosedBarStartsRthSession()
        {
            int currentHhmm = ClosedBarTimeHhmm();
            if (!IsRthBar(currentHhmm))
                return false;

            if (ClosedBarIndex() == 0)
                return true;

            DateTime currentTime = ClosedBarTime();
            DateTime priorTime = ClosedBarTime(1);
            if (currentTime.Date != priorTime.Date)
                return true;

            return !IsRthBar(ClosedBarTimeHhmm(1));
        }
    }
}
