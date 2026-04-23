using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private void RefreshContext()
        {
            _sessionFilterValid = true;

            if (_atr == null || _emaFast == null || _emaSlow == null)
                return;

            _atrValue = ClosedBarAtrValue();
            _emaFastValue = ClosedBarFastEma();
            _emaSlowValue = ClosedBarSlowEma();
            _emaFastSlopeAtrPct = ComputeFastSlopeAtrPct();
            _atrRegimeRatio = ComputeAtrRegimeRatio();

            _volatilityRegimeValid = _atrValue > 0.0
                && _atrRegimeRatio >= MinAtrRegimeRatio
                && _atrRegimeRatio <= MaxAtrRegimeRatio;

            bool longTrendValid = ClosedBarClose() > _emaSlowValue
                && _emaFastSlopeAtrPct >= SlopeMinAtrPctPerBar;
            bool shortTrendValid = ClosedBarClose() < _emaSlowValue
                && _emaFastSlopeAtrPct <= -SlopeMinAtrPctPerBar;

            _trendContextValid = longTrendValid || shortTrendValid;

            if (!_volatilityRegimeValid)
                _activeBias = SecondLegBias.Neutral;
            else if (longTrendValid && !shortTrendValid)
                _activeBias = SecondLegBias.Long;
            else if (shortTrendValid && !longTrendValid)
                _activeBias = SecondLegBias.Short;
            else
                _activeBias = SecondLegBias.Neutral;
        }

        private void TryAdvanceFromTrendContext()
        {
            if (!_trendContextValid || !_volatilityRegimeValid)
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.SeekingImpulse;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "TrendContextReady");
        }

        private void TryCaptureImpulseLeg()
        {
            if (!HasQualifiedImpulse())
                return;

            _pullbackLeg1 = new PullbackSnapshot();
            _pullbackLeg2 = new PullbackSnapshot();
            _separationHigh = double.NaN;
            _separationLow = double.NaN;
            _pullbackBounceBar = -1;
            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.TrackingPullbackLeg1;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "ImpulseQualified");
        }

        private void TryTrackPullbackLeg1()
        {
            if (!HasPullbackLeg1())
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.TrackingSeparation;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "PullbackLeg1Qualified");
        }

        private void TryTrackSeparation()
        {
            if (!_trendContextValid || !_volatilityRegimeValid)
            {
                ResetSetupState("TrendInvalid");
                return;
            }

            int bars = ClosedBarIndex() - _pullbackLeg1.StartBar + 1;
            if (bars > MaxPullbackBars)
            {
                ResetSetupState("PullbackTooLong");
                return;
            }

            if (_activeBias == SecondLegBias.Long)
            {
                bool separation = ClosedBarLow() >= _pullbackLeg1.Low
                    && ClosedBarClose() > ClosedBarClose(1)
                    && ClosedBarHigh() > ClosedBarHigh(1);
                if (separation)
                {
                    _separationHigh = ClosedBarHigh();
                    _separationLow = ClosedBarLow();
                    _pullbackBounceBar = ClosedBarIndex();
                    SecondLegSetupState previousState = _setupState;
                    _setupState = SecondLegSetupState.TrackingPullbackLeg2;
                    _lastStateTransitionUtc = DateTime.UtcNow;
                    LogSetupStateTransition(previousState, _setupState, "SeparationQualified");
                    return;
                }

                bool extendsPullback = ClosedBarLow() < _pullbackLeg1.Low || ClosedBarClose() < ClosedBarClose(1);
                if (extendsPullback)
                {
                    _pullbackLeg1.EndBar = ClosedBarIndex();
                    _pullbackLeg1.High = Math.Max(_pullbackLeg1.High, ClosedBarHigh());
                    _pullbackLeg1.Low = Math.Min(_pullbackLeg1.Low, ClosedBarLow());
                    _pullbackLeg1.Range = _pullbackLeg1.High - _pullbackLeg1.Low;

                    double retracement = ComputeRetracement(_pullbackLeg1.Low);
                    if (retracement > MaxPullbackRetracement)
                    {
                        ResetSetupState("PullbackTooDeep");
                        return;
                    }
                }
            }
            else if (_activeBias == SecondLegBias.Short)
            {
                bool separation = ClosedBarHigh() <= _pullbackLeg1.High
                    && ClosedBarClose() < ClosedBarClose(1)
                    && ClosedBarLow() < ClosedBarLow(1);
                if (separation)
                {
                    _separationHigh = ClosedBarHigh();
                    _separationLow = ClosedBarLow();
                    _pullbackBounceBar = ClosedBarIndex();
                    SecondLegSetupState previousState = _setupState;
                    _setupState = SecondLegSetupState.TrackingPullbackLeg2;
                    _lastStateTransitionUtc = DateTime.UtcNow;
                    LogSetupStateTransition(previousState, _setupState, "SeparationQualified");
                    return;
                }

                bool extendsPullback = ClosedBarHigh() > _pullbackLeg1.High || ClosedBarClose() > ClosedBarClose(1);
                if (extendsPullback)
                {
                    _pullbackLeg1.EndBar = ClosedBarIndex();
                    _pullbackLeg1.High = Math.Max(_pullbackLeg1.High, ClosedBarHigh());
                    _pullbackLeg1.Low = Math.Min(_pullbackLeg1.Low, ClosedBarLow());
                    _pullbackLeg1.Range = _pullbackLeg1.High - _pullbackLeg1.Low;

                    double retracement = ComputeRetracement(_pullbackLeg1.High);
                    if (retracement > MaxPullbackRetracement)
                    {
                        ResetSetupState("PullbackTooDeep");
                        return;
                    }
                }
            }
        }

        private void TryTrackPullbackLeg2()
        {
            if (!HasPullbackLeg2())
                return;

            if (!HasPullbackLeg2Candidate())
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.WaitingForSignalBar;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(
                previousState,
                _setupState,
                "PullbackLeg2Candidate",
                $"leg2Start={_pullbackLeg2.StartBar} leg2End={_pullbackLeg2.EndBar} nextSignalBar={_pullbackLeg2.EndBar + 1} retracement={_lastImpulseRetracement:F3} leg2Momentum={_lastLeg2Momentum:F3}");
        }

        private void TryWaitForSignalBar()
        {
            if (!_volatilityRegimeValid)
            {
                ResetSetupState(GetEntryInvalidationReason());
                return;
            }

            if (!IsCorrectiveLeg2TrendValid())
            {
                ResetSetupState("CorrectiveSideInvalid");
                return;
            }

            bool leg2Refreshed = false;
            if (_impulse.Bias == SecondLegBias.Long)
            {
                bool continuesCountertrend = ClosedBarLow() < _pullbackLeg2.Low || ClosedBarClose() < ClosedBarClose(1);
                if (continuesCountertrend)
                {
                    leg2Refreshed = true;
                    _pullbackLeg2.EndBar = ClosedBarIndex();
                    _pullbackLeg2.High = Math.Max(_pullbackLeg2.High, ClosedBarHigh());
                    _pullbackLeg2.Low = Math.Min(_pullbackLeg2.Low, ClosedBarLow());
                    _pullbackLeg2.Range = _pullbackLeg2.High - _pullbackLeg2.Low;
                }
            }
            else if (_impulse.Bias == SecondLegBias.Short)
            {
                bool continuesCountertrend = ClosedBarHigh() > _pullbackLeg2.High || ClosedBarClose() > ClosedBarClose(1);
                if (continuesCountertrend)
                {
                    leg2Refreshed = true;
                    _pullbackLeg2.EndBar = ClosedBarIndex();
                    _pullbackLeg2.High = Math.Max(_pullbackLeg2.High, ClosedBarHigh());
                    _pullbackLeg2.Low = Math.Min(_pullbackLeg2.Low, ClosedBarLow());
                    _pullbackLeg2.Range = _pullbackLeg2.High - _pullbackLeg2.Low;
                }
            }

            if (!HasPullbackLeg2Candidate())
            {
                if (_setupState == SecondLegSetupState.Reset)
                    return;

                string invalidationReason = !string.IsNullOrEmpty(_lastBlockReason)
                    ? _lastBlockReason
                    : "Leg2Extended";
                _setupState = SecondLegSetupState.TrackingPullbackLeg2;
                _lastStateTransitionUtc = DateTime.UtcNow;
                LogSetupStateTransition(SecondLegSetupState.WaitingForSignalBar, _setupState, invalidationReason);
                return;
            }

            if (leg2Refreshed)
            {
                _lastStateTransitionUtc = DateTime.UtcNow;
                WriteEntryObservation(
                    "ENTRY_STATE",
                    $"from={SecondLegSetupState.WaitingForSignalBar} to={SecondLegSetupState.WaitingForSignalBar} reason=PullbackLeg2CandidateRefresh | leg2Start={_pullbackLeg2.StartBar} leg2End={_pullbackLeg2.EndBar} nextSignalBar={_pullbackLeg2.EndBar + 1} retracement={_lastImpulseRetracement:F3} leg2Momentum={_lastLeg2Momentum:F3}");
                return;
            }

            if (!TryBuildSignalBar())
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.WaitingForTrigger;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "SignalBarQualified");
            TryWaitForTrigger();
        }

        private void TryWaitForTrigger()
        {
            string armedEntryInvalidationReason = GetArmedEntryInvalidationReason();
            if (!string.IsNullOrEmpty(armedEntryInvalidationReason))
            {
                CancelPendingEntry(armedEntryInvalidationReason);
                if (_entryOrder == null && !_entryPending && !_hasWorkingEntry)
                    ResetSetupState(armedEntryInvalidationReason);
                return;
            }

            if (!_structureRoomValid)
                return;

            if (HasActiveManagedTradeContext())
            {
                PromoteToManagingTrade();
                return;
            }

            if (HasPendingOrWorkingEntryLifecycle())
                return;

            if (!SubmitPlannedEntry())
                return;

            _setupState = SecondLegSetupState.WaitingForTrigger;
            _lastStateTransitionUtc = DateTime.UtcNow;
        }

        private string GetEntryInvalidationReason()
        {
            if (FlattenBeforeClose && ClosedBarTimeHhmm() >= FlattenTimeHhmm)
                return "FlattenWindow";

            if (!_volatilityRegimeValid)
                return "AtrRegimeInvalid";

            return "TrendInvalid";
        }

        private string GetArmedEntryInvalidationReason()
        {
            if (!HasPlannedEntry())
                return string.Empty;

            if (FlattenBeforeClose && ClosedBarTimeHhmm() >= FlattenTimeHhmm)
                return "FlattenWindow";

            int totalPullbackBars = _pullbackLeg1.StartBar > 0
                ? ClosedBarIndex() - _pullbackLeg1.StartBar + 1
                : 0;
            if (totalPullbackBars > MaxPullbackBars)
                return "PullbackTooLong";

            double activePullbackExtreme = _impulse.Bias == SecondLegBias.Short
                ? Math.Max(_pullbackLeg2.High, ClosedBarHigh())
                : Math.Min(_pullbackLeg2.Low, ClosedBarLow());
            double retracement = ComputeRetracement(activePullbackExtreme);
            if (retracement > MaxPullbackRetracement)
                return "PullbackTooDeep";

            if (!SignalStillValid())
                return "EntryExpired";

            return string.Empty;
        }

        private bool HasQualifiedImpulse()
        {
            if (_activeBias == SecondLegBias.Neutral || ClosedBarCount() < V1ImpulseBars || _atrValue <= 0.0)
                return false;

            const int lookback = V1ImpulseBars;
            int startBar = ClosedBarIndex(lookback - 1);
            double high = ClosedBarHigh();
            double low = ClosedBarLow();
            int strongBars = 0;

            for (int barsAgo = 0; barsAgo < lookback; barsAgo++)
            {
                high = Math.Max(high, ClosedBarHigh(barsAgo));
                low = Math.Min(low, ClosedBarLow(barsAgo));

                double barRange = Math.Max(ClosedBarHigh(barsAgo) - ClosedBarLow(barsAgo), TickSize);
                double bodyPct = Math.Abs(ClosedBarClose(barsAgo) - ClosedBarOpen(barsAgo)) / barRange;
                bool directionalBar = _activeBias == SecondLegBias.Long
                    ? ClosedBarClose(barsAgo) > ClosedBarOpen(barsAgo)
                    : ClosedBarClose(barsAgo) < ClosedBarOpen(barsAgo);
                if (directionalBar && bodyPct >= V1StrongBodyPct)
                    strongBars++;
            }

            double impulseMove = high - low;
            if (impulseMove < (MinImpulseAtrMultiple * _atrValue))
                return false;

            if (strongBars < V1MinStrongBars)
                return false;

            if (_activeBias == SecondLegBias.Long)
            {
                if (!(ClosedBarClose() > ClosedBarOpen() && ClosedBarClose() > _emaFastValue))
                    return false;
            }
            else
            {
                if (!(ClosedBarClose() < ClosedBarOpen() && ClosedBarClose() < _emaFastValue))
                    return false;
            }

            _impulse = new ImpulseSnapshot
            {
                StartBar = startBar,
                EndBar = ClosedBarIndex(),
                High = high,
                Low = low,
                Range = impulseMove,
                Bias = _activeBias,
            };
            _lastImpulseStrongBars = strongBars;
            _lastImpulseMomentum = impulseMove / V1ImpulseBars;
            WriteEntryObservation("IMPULSE_QUALIFIED", $"high={high:F2} low={low:F2} move={impulseMove:F2} strongBars={strongBars}");
            return true;
        }

        private bool HasPullbackLeg1()
        {
            if (_impulse.EndBar >= ClosedBarIndex())
                return false;

            if (_pullbackLeg1.StartBar <= 0)
            {
                bool startingBar = _impulse.Bias == SecondLegBias.Long
                    ? (ClosedBarClose() < ClosedBarClose(1) || ClosedBarLow() < ClosedBarLow(1))
                    : (ClosedBarClose() > ClosedBarClose(1) || ClosedBarHigh() > ClosedBarHigh(1));
                if (!startingBar)
                    return false;

                _pullbackLeg1 = new PullbackSnapshot
                {
                    StartBar = ClosedBarIndex(),
                    EndBar = ClosedBarIndex(),
                    High = ClosedBarHigh(),
                    Low = ClosedBarLow(),
                    Range = ClosedBarHigh() - ClosedBarLow(),
                };
                return false;
            }

            _pullbackLeg1.EndBar = ClosedBarIndex();
            _pullbackLeg1.High = Math.Max(_pullbackLeg1.High, ClosedBarHigh());
            _pullbackLeg1.Low = Math.Min(_pullbackLeg1.Low, ClosedBarLow());
            _pullbackLeg1.Range = _pullbackLeg1.High - _pullbackLeg1.Low;

            int bars = _pullbackLeg1.EndBar - _pullbackLeg1.StartBar + 1;
            double retracement = ComputeRetracement(_impulse.Bias == SecondLegBias.Long ? _pullbackLeg1.Low : _pullbackLeg1.High);
            _lastImpulseRetracement = retracement;

            if (bars > MaxPullbackBars)
            {
                ResetSetupState("PullbackTooLong");
                return false;
            }

            if (retracement > MaxPullbackRetracement)
            {
                ResetSetupState("PullbackTooDeep");
                return false;
            }

            return retracement >= MinPullbackRetracement;
        }

        private bool HasPullbackLeg2()
        {
            if (_pullbackBounceBar < 0 || ClosedBarIndex() <= _pullbackBounceBar)
                return false;

            if (_pullbackLeg2.StartBar <= 0)
            {
                bool startingBar = _impulse.Bias == SecondLegBias.Long
                    ? (ClosedBarLow() < ClosedBarLow(1) || ClosedBarClose() < ClosedBarClose(1))
                    : (ClosedBarHigh() > ClosedBarHigh(1) || ClosedBarClose() > ClosedBarClose(1));
                if (!startingBar)
                    return false;

                _pullbackLeg2 = new PullbackSnapshot
                {
                    StartBar = ClosedBarIndex(),
                    EndBar = ClosedBarIndex(),
                    High = ClosedBarHigh(),
                    Low = ClosedBarLow(),
                    Range = ClosedBarHigh() - ClosedBarLow(),
                };
                return false;
            }

            _pullbackLeg2.EndBar = ClosedBarIndex();
            _pullbackLeg2.High = Math.Max(_pullbackLeg2.High, ClosedBarHigh());
            _pullbackLeg2.Low = Math.Min(_pullbackLeg2.Low, ClosedBarLow());
            _pullbackLeg2.Range = _pullbackLeg2.High - _pullbackLeg2.Low;

            int totalBars = ClosedBarIndex() - _pullbackLeg1.StartBar + 1;
            double retracement = ComputeRetracement(_impulse.Bias == SecondLegBias.Long ? _pullbackLeg2.Low : _pullbackLeg2.High);
            _lastImpulseRetracement = retracement;
            if (totalBars > MaxPullbackBars)
            {
                ResetSetupState("PullbackTooLong");
                return false;
            }

            if (retracement > MaxPullbackRetracement)
            {
                ResetSetupState("PullbackTooDeep");
                return false;
            }

            return retracement >= MinPullbackRetracement;
        }

        private bool HasPullbackLeg2Candidate()
        {
            if (_pullbackLeg2.StartBar <= 0)
            {
                RecordEntryBlock("Leg2CandidateMissing", $"startBar={_pullbackLeg2.StartBar}");
                return false;
            }

            int totalBars = ClosedBarIndex() - _pullbackLeg1.StartBar + 1;
            if (totalBars > MaxPullbackBars)
            {
                ResetSetupState("PullbackTooLong");
                return false;
            }

            if (!_volatilityRegimeValid)
            {
                ResetSetupState("AtrRegimeInvalid");
                return false;
            }

            if (!IsCorrectiveLeg2TrendValid())
            {
                ResetSetupState("CorrectiveSideInvalid");
                return false;
            }

            double retracement = ComputeRetracement(_impulse.Bias == SecondLegBias.Long ? _pullbackLeg2.Low : _pullbackLeg2.High);
            _lastImpulseRetracement = retracement;
            if (retracement < MinPullbackRetracement)
            {
                RecordEntryBlock("SecondLegTooShallow", $"retracement={retracement:F3} min={MinPullbackRetracement:F3}");
                return false;
            }

            if (retracement > MaxPullbackRetracement)
            {
                RecordEntryBlock("SecondLegTooDeep", $"retracement={retracement:F3} max={MaxPullbackRetracement:F3}");
                return false;
            }

            double impulseMomentum = _impulse.Range / V1ImpulseBars;
            double leg2CountertrendMove = _impulse.Bias == SecondLegBias.Long
                ? _separationHigh - _pullbackLeg2.Low
                : _pullbackLeg2.High - _separationLow;
            int leg2Bars = Math.Max(1, _pullbackLeg2.EndBar - _pullbackLeg2.StartBar + 1);
            double leg2Momentum = leg2CountertrendMove / leg2Bars;
            _lastLeg2Momentum = leg2Momentum;

            if (impulseMomentum <= 0.0 || leg2Momentum > impulseMomentum * SecondLegMaxMomentumRatio)
            {
                RecordEntryBlock("SecondLegTooStrong", $"leg2Momentum={leg2Momentum:F3} limit={(impulseMomentum * SecondLegMaxMomentumRatio):F3}");
                return false;
            }

            return true;
        }

        private bool IsCorrectiveLeg2TrendValid()
        {
            if (_emaSlow == null)
                return false;

            if (_impulse.Bias == SecondLegBias.Long)
                return ClosedBarClose() > _emaSlowValue;

            if (_impulse.Bias == SecondLegBias.Short)
                return ClosedBarClose() < _emaSlowValue;

            return false;
        }

        private bool TryBuildSignalBar()
        {
            if (ClosedBarIndex() <= _pullbackLeg2.EndBar)
            {
                RecordEntryBlock(
                    "SignalInvalid",
                    $"same-bar signal disallowed bar={ClosedBarIndex()} leg2EndBar={_pullbackLeg2.EndBar} nextEligibleBar={_pullbackLeg2.EndBar + 1}");
                return false;
            }

            double midpoint = ClosedBarLow() + ((ClosedBarHigh() - ClosedBarLow()) * 0.5);
            bool directionalSignal = _impulse.Bias == SecondLegBias.Long
                ? (ClosedBarLow() >= _pullbackLeg2.Low && ClosedBarClose() >= midpoint)
                : (ClosedBarHigh() <= _pullbackLeg2.High && ClosedBarClose() <= midpoint);
            if (!directionalSignal)
            {
                RecordEntryBlock(
                    "SignalInvalid",
                    _impulse.Bias == SecondLegBias.Long
                        ? $"signal bar midpoint/directional test failed close={ClosedBarClose():F2} midpoint={midpoint:F2} low={ClosedBarLow():F2} leg2Low={_pullbackLeg2.Low:F2}"
                        : $"signal bar midpoint/directional test failed close={ClosedBarClose():F2} midpoint={midpoint:F2} high={ClosedBarHigh():F2} leg2High={_pullbackLeg2.High:F2}");
                return false;
            }

            _signalBarHigh = ClosedBarHigh();
            _signalBarLow = ClosedBarLow();
            _signalBarIndex = ClosedBarIndex();
            int expiryBar = ClosedBarIndex() + MaxTriggerBars;

            double plannedEntryPrice;
            double plannedStopPrice;
            string entryReason;
            SecondLegBias plannedEntryBias;
            if (_impulse.Bias == SecondLegBias.Short)
            {
                plannedEntryPrice = Instrument.MasterInstrument.RoundToTickSize(
                    _signalBarLow - (EntryOffsetTicks * TickSize));
                plannedStopPrice = Instrument.MasterInstrument.RoundToTickSize(
                    _pullbackLeg2.High + (StopBufferTicks * TickSize));
                entryReason = "SecondLegShort";
                plannedEntryBias = SecondLegBias.Short;
            }
            else
            {
                plannedEntryPrice = Instrument.MasterInstrument.RoundToTickSize(
                    _signalBarHigh + (EntryOffsetTicks * TickSize));
                plannedStopPrice = Instrument.MasterInstrument.RoundToTickSize(
                    _pullbackLeg2.Low - (StopBufferTicks * TickSize));
                entryReason = "SecondLegLong";
                plannedEntryBias = SecondLegBias.Long;
            }

            double stopDistance = Math.Abs(plannedEntryPrice - plannedStopPrice);
            double riskPerContract = stopDistance * Instrument.MasterInstrument.PointValue;
            if (riskPerContract <= 0.0)
            {
                RecordEntryBlock("RiskTooSmall", $"riskPerContract={riskPerContract:F2}");
                return false;
            }

            if (_atrValue > 0.0 && stopDistance / _atrValue > MaxStopAtrMultiple)
            {
                RecordEntryBlock(
                    "StopTooWide",
                    $"entry={plannedEntryPrice:F2} stop={plannedStopPrice:F2} stopDistance={stopDistance:F2} atr={_atrValue:F2} ratio={(stopDistance / _atrValue):F3} cap={MaxStopAtrMultiple:F3}");
                return false;
            }

            int pendingEntryQuantity = Math.Max(0, (int)Math.Floor(RiskPerTrade / riskPerContract));
            if (pendingEntryQuantity <= 0)
            {
                RecordEntryBlock("RiskTooSmall", $"qty={pendingEntryQuantity} riskPerContract={riskPerContract:F2}");
                return false;
            }

            _structureRoomValid = EvaluateStructureRoom(plannedEntryPrice, plannedStopPrice, plannedEntryBias);
            if (!_structureRoomValid)
            {
                double roomR = (!double.IsNaN(_lastStructureRoom) && stopDistance > 0.0)
                    ? _lastStructureRoom / stopDistance
                    : double.NaN;
                RecordEntryBlock(
                    "StructureRoom",
                    $"entry={plannedEntryPrice:F2} stop={plannedStopPrice:F2} risk={stopDistance:F2} required={_lastStructureRequiredRoom:F2} room={_lastStructureRoom:F2} roomR={roomR:F2} level={_lastStructureLabel} structurePrice={_lastStructurePrice:F2}");
                return false;
            }

            string pendingEntrySignal = plannedEntryBias == SecondLegBias.Short
                ? SecondLegNt8SignalName.Entry(ClosedBarIndex().ToString(), "PE2S")
                : SecondLegNt8SignalName.Entry(ClosedBarIndex().ToString(), "PE2L");

            _plannedEntry = new PlannedEntry
            {
                Bias = plannedEntryBias,
                SignalName = pendingEntrySignal,
                Reason = entryReason,
                EntryPrice = plannedEntryPrice,
                InitialStopPrice = plannedStopPrice,
                Quantity = pendingEntryQuantity,
                ExpiryBar = expiryBar,
            };
            WriteEntryObservation(
                "ENTRY_ARMED",
                $"signal={pendingEntrySignal} bias={plannedEntryBias} entry={plannedEntryPrice:F2} stop={plannedStopPrice:F2} qty={pendingEntryQuantity} expiry={expiryBar}");
            return true;
        }

        private bool SignalStillValid()
        {
            return !HasPlannedEntry() || _plannedEntry.ExpiryBar < 0 || ClosedBarIndex() <= _plannedEntry.ExpiryBar;
        }

        private double ComputeFastSlopeAtrPct()
        {
            if (_atrValue <= 0.0 || ClosedBarIndex() < SlopeLookbackBars)
                return 0.0;

            double delta = ClosedBarFastEma() - ClosedBarFastEma(SlopeLookbackBars);
            return (delta / SlopeLookbackBars) / _atrValue;
        }

        private double ComputeAtrRegimeRatio()
        {
            if (_atrValue <= 0.0)
                return 0.0;

            int lookback = Math.Min(AtrRegimeLookback, ClosedBarCount());
            if (lookback <= 0)
                return 0.0;

            double sum = 0.0;
            for (int barsAgo = 0; barsAgo < lookback; barsAgo++)
                sum += ClosedBarAtrValue(barsAgo);

            double baseline = sum / lookback;
            return baseline > 0.0 ? _atrValue / baseline : 0.0;
        }

        private double ComputeRetracement(double pullbackExtreme)
        {
            if (_impulse.Range <= 0.0)
                return 1.0;

            if (_impulse.Bias == SecondLegBias.Short)
                return (pullbackExtreme - _impulse.Low) / _impulse.Range;

            return (_impulse.High - pullbackExtreme) / _impulse.Range;
        }

        private bool EvaluateStructureRoom(double entryPrice, double stopPrice, SecondLegBias bias)
        {
            _lastStructureLabel = string.Empty;
            _lastStructurePrice = double.NaN;
            _lastStructureRoom = double.NaN;

            if (!StructureFilterEnabled)
                return true;

            double risk = Math.Max(TickSize, Math.Abs(entryPrice - stopPrice));
            double requiredRoom = risk * MinRoomToStructureR;
            _lastStructureRequiredRoom = requiredRoom;
            if (bias == SecondLegBias.Short)
            {
                double nearestSupport = double.MinValue;
                string nearestSupportLabel = string.Empty;
                foreach (StructureLevel level in _structureLevels)
                {
                    if (!IsShortOpposingStructure(level.Kind))
                        continue;

                    double candidate = level.Price;
                    if (candidate < entryPrice && candidate > nearestSupport)
                    {
                        nearestSupport = candidate;
                        nearestSupportLabel = level.Label ?? level.Kind.ToString();
                    }
                }

                if (nearestSupport == double.MinValue)
                    return true;

                double room = entryPrice - nearestSupport;
                _lastStructureLabel = nearestSupportLabel;
                _lastStructurePrice = nearestSupport;
                _lastStructureRoom = room;
                return room >= requiredRoom;
            }

            double nearestResistance = double.MaxValue;
            string nearestResistanceLabel = string.Empty;
            foreach (StructureLevel level in _structureLevels)
            {
                if (!IsLongOpposingStructure(level.Kind))
                    continue;

                double candidate = level.Price;
                if (candidate > entryPrice && candidate < nearestResistance)
                {
                    nearestResistance = candidate;
                    nearestResistanceLabel = level.Label ?? level.Kind.ToString();
                }
            }

            if (nearestResistance == double.MaxValue)
                return true;

            double longRoom = nearestResistance - entryPrice;
            _lastStructureLabel = nearestResistanceLabel;
            _lastStructurePrice = nearestResistance;
            _lastStructureRoom = longRoom;
            return longRoom >= requiredRoom;
        }

        private bool IsLongOpposingStructure(StructureLevelKind kind)
        {
            return kind == StructureLevelKind.PriorDayHigh
                || kind == StructureLevelKind.OpeningRangeHigh
                || kind == StructureLevelKind.SwingHigh;
        }

        private bool IsShortOpposingStructure(StructureLevelKind kind)
        {
            return kind == StructureLevelKind.PriorDayLow
                || kind == StructureLevelKind.OpeningRangeLow
                || kind == StructureLevelKind.SwingLow;
        }

        private double HighestHigh(int lookback, int startBarsAgo)
        {
            int start = ResolveClosedBarBarsAgo(startBarsAgo);
            int end = ResolveClosedBarBarsAgo(lookback + startBarsAgo - 1);
            double value = High[start];
            for (int barsAgo = start; barsAgo <= end; barsAgo++)
                value = Math.Max(value, High[barsAgo]);
            return value;
        }

        private double LowestLow(int lookback, int startBarsAgo)
        {
            int start = ResolveClosedBarBarsAgo(startBarsAgo);
            int end = ResolveClosedBarBarsAgo(lookback + startBarsAgo - 1);
            double value = Low[start];
            for (int barsAgo = start; barsAgo <= end; barsAgo++)
                value = Math.Min(value, Low[barsAgo]);
            return value;
        }

    }
}
