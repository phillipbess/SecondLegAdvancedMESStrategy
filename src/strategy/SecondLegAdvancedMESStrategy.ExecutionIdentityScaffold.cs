using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        // Donor-shaped execution identity scaffold.
        // This file stays host-owned and intentionally avoids adding live NT8
        // behavior until the execution-events lane is ported.

        private string _persistedTradeId = string.Empty;
        private bool _entryPending;
        private bool _entryEarlyLatch;
        private bool _flattenInFlight;

        private void EnsureActiveTradeIdFromCurrentTradeId(string reason)
        {
            if (!string.IsNullOrEmpty(_activeTradeId) || string.IsNullOrEmpty(currentTradeID))
                return;

            _activeTradeId = currentTradeID;
            if (string.IsNullOrEmpty(_countedTradeSessionId))
                _countedTradeSessionId = currentTradeID;

            WriteDebugLog(
                $"TRADE ID ANCHOR | ActiveTradeId restored from TradeID | reason={reason} tradeId={_activeTradeId}");
        }

        private void CapturePersistedTradeIdentity(string reason)
        {
            _persistedTradeId = currentTradeID ?? string.Empty;
            WriteDebugLog(
                $"[PERSISTENCE_SCAFFOLD] captured persisted trade identity | reason={reason} tradeId={_persistedTradeId}");
        }

        private void TrackCumulativeFillQuantity(int cumulativeFilledQuantity, string reason)
        {
            _trackedFillQuantity = Math.Max(_trackedFillQuantity, Math.Max(0, cumulativeFilledQuantity));
            WriteDebugLog(
                $"[EXECUTION_IDENTITY] trackedFillQuantity={_trackedFillQuantity} reason={reason}");
        }

        private void ClearEntrySubmitInFlight(string reason)
        {
            _entryPending = false;
            _entryEarlyLatch = false;
            WriteDebugLog($"[ENTRY_SUBMIT] cleared transient entry submit latches | reason={reason}");
        }

        private void HandleFlatRealtimeRestartScaffold()
        {
            bool preserveDurableSafetyLockout = autoDisabled || _globalKillSwitch;
            if (preserveDurableSafetyLockout)
                WriteDebugLog("[STATE_REALTIME] Preserving durable safety lockout on flat restart");

            ClearEntrySubmitInFlight("State.Realtime.FlatRestart");
            _flattenInFlight = false;
            _flatResetDeferred = false;
        }

        private static bool IsPrimaryEntrySideAction(OrderAction action)
        {
            return action == OrderAction.Buy || action == OrderAction.SellShort;
        }

        private bool MatchesKnownPrimaryEntrySignal(string signal)
        {
            if (string.IsNullOrEmpty(signal))
                return false;

            return string.Equals(signal, _activeEntrySignal, StringComparison.Ordinal)
                || string.Equals(signal, _currentEntryTag, StringComparison.Ordinal)
                || signal.StartsWith("PE_", StringComparison.Ordinal)
                || signal.StartsWith("ENTRY_", StringComparison.Ordinal)
                || signal.StartsWith("LongEntry", StringComparison.Ordinal)
                || signal.StartsWith("ShortEntry", StringComparison.Ordinal);
        }

        private bool MatchesOwnedPrimaryEntrySignal(string signal)
        {
            if (string.IsNullOrEmpty(signal))
                return false;

            string plannedSignalName = PlannedEntrySignalName();
            return string.Equals(signal, _activeEntrySignal, StringComparison.Ordinal)
                || string.Equals(signal, _currentEntryTag, StringComparison.Ordinal)
                || string.Equals(signal, plannedSignalName, StringComparison.Ordinal)
                || string.Equals(signal, currentTradeID, StringComparison.Ordinal);
        }
    }
}
