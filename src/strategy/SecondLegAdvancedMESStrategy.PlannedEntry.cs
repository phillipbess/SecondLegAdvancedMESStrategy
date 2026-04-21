namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private bool HasPlannedEntry()
        {
            return _plannedEntry != null && _plannedEntry.IsValid;
        }

        private string PlannedEntrySignalName()
        {
            return HasPlannedEntry() ? _plannedEntry.SignalName : string.Empty;
        }

        private void ClearPlannedEntry()
        {
            _plannedEntry = PlannedEntry.CreateEmpty();
        }
    }
}
