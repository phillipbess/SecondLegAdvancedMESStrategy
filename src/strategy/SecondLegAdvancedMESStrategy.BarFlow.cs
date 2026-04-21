namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private void HandleTradeLogic()
        {
            RefreshContext();
            SyncSetupStateWithEntryLifecycle();

            if (!MayEvaluateNewSetup())
                return;

            switch (_setupState)
            {
                case SecondLegSetupState.Blocked:
                    ReleaseBlockedState();
                    break;

                case SecondLegSetupState.Reset:
                    AdvanceResetState();
                    break;

                case SecondLegSetupState.SeekingBias:
                    TryAdvanceFromTrendContext();
                    break;

                case SecondLegSetupState.SeekingImpulse:
                    TryCaptureImpulseLeg();
                    break;

                case SecondLegSetupState.TrackingPullbackLeg1:
                    TryTrackPullbackLeg1();
                    break;

                case SecondLegSetupState.TrackingSeparation:
                    TryTrackSeparation();
                    break;

                case SecondLegSetupState.TrackingPullbackLeg2:
                    TryTrackPullbackLeg2();
                    break;

                case SecondLegSetupState.WaitingForSignalBar:
                    TryWaitForSignalBar();
                    break;

                case SecondLegSetupState.WaitingForTrigger:
                    TryWaitForTrigger();
                    break;

                case SecondLegSetupState.ManagingTrade:
                    HandleActiveTradeLifecycle();
                    break;
            }
        }

        private bool MayEvaluateNewSetup()
        {
            if (Position.Quantity != 0 || _hasWorkingEntry || _entryFilledForActiveSignal)
                return true;

            string blockReason = GetHardBlockReason();
            if (!string.IsNullOrEmpty(blockReason))
            {
                EnterBlockedState(blockReason);
                return false;
            }

            return true;
        }

        private string GetHardBlockReason()
        {
            if (_tradesThisSession >= MaxTradesPerSession)
                return "TradeLimitHit";

            if (_consecutiveLosses >= MaxConsecutiveLosses)
                return "LossStreakHit";

            if (_sessionRealizedR <= DailyLossLimitR)
                return "DailyLossLimit";

            if (_lossCooldownUntilBar >= 0 && ClosedBarIndex() < _lossCooldownUntilBar)
                return "LossCooldown";

            return string.Empty;
        }
    }
}
