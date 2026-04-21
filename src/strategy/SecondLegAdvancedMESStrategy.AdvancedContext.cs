using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private void UpdateAdvancedSessionContext()
        {
            DateTime tradingDate = ClosedBarTime().Date;
            bool sessionBoundary = ClosedBarIsFirstBarOfSession();

            if (_contextTradingDate == DateTime.MinValue || sessionBoundary)
                RollTradingDate(tradingDate);

            _currentSessionHigh = double.IsNaN(_currentSessionHigh) ? ClosedBarHigh() : Math.Max(_currentSessionHigh, ClosedBarHigh());
            _currentSessionLow = double.IsNaN(_currentSessionLow) ? ClosedBarLow() : Math.Min(_currentSessionLow, ClosedBarLow());

            int hhmm = ClosedBarTimeHhmm();
            const int rthOpen = 930;
            int openingRangeEnd = AddMinutesToHhmm(rthOpen, OpeningRangeMinutes);

            if (UseOpeningRange && hhmm >= rthOpen && hhmm < openingRangeEnd)
            {
                _openingRangeHigh = double.IsNaN(_openingRangeHigh) ? ClosedBarHigh() : Math.Max(_openingRangeHigh, ClosedBarHigh());
                _openingRangeLow = double.IsNaN(_openingRangeLow) ? ClosedBarLow() : Math.Min(_openingRangeLow, ClosedBarLow());
            }
            else if (UseOpeningRange && hhmm >= openingRangeEnd && !double.IsNaN(_openingRangeHigh) && !double.IsNaN(_openingRangeLow))
            {
                _openingRangeComplete = true;
            }

            RefreshStructureLevels();
        }

        private void RollTradingDate(DateTime tradingDate)
        {
            if (_contextTradingDate != DateTime.MinValue && !double.IsNaN(_currentSessionHigh) && !double.IsNaN(_currentSessionLow))
            {
                _priorSessionHigh = _currentSessionHigh;
                _priorSessionLow = _currentSessionLow;
            }

            _contextTradingDate = tradingDate;
            _currentSessionHigh = ClosedBarHigh();
            _currentSessionLow = ClosedBarLow();
            _openingRangeHigh = double.NaN;
            _openingRangeLow = double.NaN;
            _openingRangeComplete = false;
        }

        private void RefreshStructureLevels()
        {
            _structureLevels.Clear();

            if (UsePriorDayHighLow)
            {
                AddStructureLevel(StructureLevelKind.PriorDayHigh, _priorSessionHigh, "PDH");
                AddStructureLevel(StructureLevelKind.PriorDayLow, _priorSessionLow, "PDL");
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
    }
}
