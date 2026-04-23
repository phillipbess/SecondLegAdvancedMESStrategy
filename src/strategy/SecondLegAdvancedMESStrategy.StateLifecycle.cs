using System;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "SecondLegAdvancedMESStrategy";
                Calculate = Calculate.OnEachTick;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsUnmanaged = true;
                IsInstantiatedOnEachOptimizationIteration = false;
            }
            else if (State == State.Configure)
            {
                _hostShellReady = true;
                InitializeRuntimeCoreIfNeeded();
                InitializeStatePersistencePath();
                InitializeLogging();
            }
            else if (State == State.DataLoaded)
            {
                _emaFast = EMA(V1FastEmaPeriod);
                _emaSlow = EMA(TrendEmaPeriod);
                _atr = ATR(AtrPeriod);

                ResetClosedBarSessionFlags();
                InitializeRuntimeCoreIfNeeded();
                ResetSetupState("DataLoaded");
            }
            else if (State == State.Realtime)
            {
                if (!stateRestored)
                {
                    RestoreStrategyState();
                    stateRestored = true;
                }

                BeginReconnectGrace("State.Realtime");

                if (Position.Quantity == 0)
                    HandleFlatRealtimeRestartScaffold();
            }
            else if (State == State.Terminated)
            {
                CleanupLogging();
            }
        }

        protected override void OnBarUpdate()
        {
            if (!_hostShellReady)
                return;

            if (BarsInProgress != 0)
                return;

            AdvanceClosedBarSessionFlags();

            bool hasStrategyLookback = ClosedBarIndex() >= Math.Max(TrendEmaPeriod, AtrRegimeLookback) + 5;

            if (hasStrategyLookback && IsClosedPrimaryBarPass())
                RunClosedBarStrategyPass();

            RunRuntimeMaintenancePass("OnBarUpdate");
        }

        private bool IsClosedPrimaryBarPass()
        {
            if (Calculate == Calculate.OnBarClose)
                return true;

            return IsFirstTickOfBar && CurrentBar > 0;
        }

        private void RunClosedBarStrategyPass()
        {
            UpdateAdvancedSessionContext();
            UpdateSessionAnchor();
            if (EnforceSessionClose())
            {
                PumpExitOps("OnBarUpdate.SessionClose");
                ProcessCancellationQueue();
                return;
            }

            HandleTradeLogic();
        }

        private bool EnforceSessionClose()
        {
            if (!FlattenBeforeClose)
                return false;

            int hhmm = ClosedBarTimeHhmm();
            if (hhmm < FlattenTimeHhmm)
                return false;

            if (_hasWorkingEntry && !_entryFilledForActiveSignal)
                CancelPendingEntry("FlattenTime");

            if (Position.Quantity != 0)
            {
                TriggerFlatten("FlattenTime");
                return true;
            }

            return _hasWorkingEntry;
        }

        private void RunRuntimeMaintenancePass(string context)
        {
            AdvanceRestoreObservation(context);
            PumpExitOps(context);
            ContinueFlattenProtocol(context);
            ProcessCancellationQueue();
        }

        private void UpdateSessionAnchor()
        {
            bool sessionBoundary = ClosedBarIsFirstBarOfSession();
            bool rthBoundary = ClosedBarStartsRthSession();
            if (_sessionAnchor == DateTime.MinValue)
                _sessionAnchor = ClosedBarTime().Date;

            if (sessionBoundary)
            {
                _sessionAnchor = ClosedBarTime().Date;
                _sessionResetPending = true;

                if (_hasWorkingEntry && !_entryFilledForActiveSignal)
                    CancelPendingEntry("NewSession");
            }

            if (rthBoundary)
            {
                if (_hasWorkingEntry && !_entryFilledForActiveSignal)
                    CancelPendingEntry("RthOpen");

                if (_hasWorkingEntry || Position.Quantity != 0 || _entryFilledForActiveSignal)
                    _rthOpenResetPending = true;
                else
                    ResetSetupState("RthOpen");
            }

            bool idleSetupContext = !_hasWorkingEntry && Position.Quantity == 0 && !_entryFilledForActiveSignal;

            if (_sessionResetPending && idleSetupContext)
            {
                _tradesThisSession = 0;
                _consecutiveLosses = 0;
                _sessionRealizedR = 0.0;
                _lossCooldownUntilBar = -1;
                _sessionResetPending = false;
                ResetSetupState("NewSession");
            }

            if (_rthOpenResetPending && idleSetupContext)
            {
                _rthOpenResetPending = false;
                ResetSetupState("RthOpen");
            }
        }

        private void ResetSetupState(string reason)
        {
            ClearSetupState(reason, SecondLegSetupState.Reset);
        }

        private void EnterBlockedState(string reason)
        {
            ClearSetupState(reason, SecondLegSetupState.Blocked);
        }

        private void ReleaseBlockedState()
        {
            if (_setupState != SecondLegSetupState.Blocked)
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.Reset;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "ReleaseBlockedState");
        }

        private void AdvanceResetState()
        {
            if (_setupState != SecondLegSetupState.Reset)
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.SeekingBias;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "AdvanceResetState");
        }

        private void ClearSetupState(string reason, SecondLegSetupState nextState)
        {
            SecondLegSetupState previousState = _setupState;
            _setupState = nextState;
            _activeBias = SecondLegBias.Neutral;
            _trendContextValid = false;
            _sessionFilterValid = true;
            _volatilityRegimeValid = false;
            _structureRoomValid = false;
            _signalBarIndex = -1;
            ClearPlannedEntry();
            _lastBlockReason = reason;
            _impulse = new ImpulseSnapshot();
            _pullbackLeg1 = new PullbackSnapshot();
            _pullbackLeg2 = new PullbackSnapshot();
            _separationHigh = double.NaN;
            _separationLow = double.NaN;
            _pullbackBounceBar = -1;
            _entryOrder = null;
            _entryFilledForActiveSignal = false;
            _hasWorkingEntry = false;
            _bestFavorablePrice = 0.0;
            _simpleTrailArmed = false;
            _structureLevels.Clear();
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, reason);
        }
    }
}
