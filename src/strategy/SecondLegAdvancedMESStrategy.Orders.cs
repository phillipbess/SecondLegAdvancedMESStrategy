using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private string ResolveTradeLifecycleSignal(Order order)
        {
            if (!string.IsNullOrEmpty(order?.FromEntrySignal))
                return order.FromEntrySignal;

            if (!string.IsNullOrEmpty(_activeEntrySignal))
                return _activeEntrySignal;

            if (!string.IsNullOrEmpty(currentTradeID))
                return currentTradeID;

            return string.Empty;
        }

        private string ResolveTradeLifecycleRole(Order order)
        {
            if (order == null)
                return "unknown";

            if (TradeManagerIsPrimaryEntryOrder(order) || IsTrackedPrimaryEntryOrder(order))
                return "entry";

            if (IsStrategyFlattenOrder(order))
                return "flatten";

            if (IsStrategyProtectiveStopOrder(order))
                return "protective";

            return "unknown";
        }

        private void WriteTradeLifecycleLog(string eventName, Order order, params string[] detailFields)
        {
            string signal = ResolveTradeLifecycleSignal(order);
            string role = ResolveTradeLifecycleRole(order);
            string trade = currentTradeID ?? string.Empty;
            string orderName = order?.Name ?? string.Empty;
            string orderId = order?.OrderId ?? string.Empty;
            string orderState = order?.OrderState.ToString() ?? string.Empty;
            string fields = JoinStructuredFields(
                $"signal={signal}",
                $"trade={trade}",
                $"role={role}",
                !string.IsNullOrEmpty(orderName) ? $"order={orderName}" : string.Empty,
                !string.IsNullOrEmpty(orderId) ? $"orderId={orderId}" : string.Empty,
                !string.IsNullOrEmpty(orderState) ? $"state={orderState}" : string.Empty,
                detailFields);
            WriteTradeLog($"[{eventName}] {fields}");
        }

        private string BuildLatestClosedTradeSummary()
        {
            int lastProcessedTradeIndex = _lastProcessedTradeCount - 1;
            if (lastProcessedTradeIndex < 0 || lastProcessedTradeIndex >= SystemPerformance.AllTrades.Count)
                return string.Empty;

            Trade trade = SystemPerformance.AllTrades[lastProcessedTradeIndex];
            if (trade == null)
                return string.Empty;

            double riskBasis = initialTradeRisk > 0.0 ? initialTradeRisk : Math.Max(1.0, RiskPerTrade);
            double realizedR = trade.ProfitCurrency / riskBasis;
            return $" pnlCurrency={trade.ProfitCurrency:F2} pnlR={realizedR:F2}";
        }

        private bool HasPendingOrWorkingEntryLifecycle()
        {
            return _entryPending || _hasWorkingEntry;
        }

        private bool HasActiveManagedTradeContext()
        {
            return _entryFilledForActiveSignal || Position.Quantity != 0;
        }

        private void PromoteToManagingTrade()
        {
            if (_setupState == SecondLegSetupState.ManagingTrade)
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.ManagingTrade;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "PromoteToManagingTrade");
        }

        private void DemoteToWaitingForTrigger()
        {
            if (_setupState == SecondLegSetupState.WaitingForTrigger)
                return;

            SecondLegSetupState previousState = _setupState;
            _setupState = SecondLegSetupState.WaitingForTrigger;
            _lastStateTransitionUtc = DateTime.UtcNow;
            LogSetupStateTransition(previousState, _setupState, "DemoteToWaitingForTrigger");
        }

        private void SyncSetupStateWithEntryLifecycle()
        {
            if (HasActiveManagedTradeContext())
            {
                PromoteToManagingTrade();
                return;
            }

            if (_setupState == SecondLegSetupState.ManagingTrade
                && HasPendingOrWorkingEntryLifecycle()
                && HasPlannedEntry())
            {
                DemoteToWaitingForTrigger();
            }
        }

        private void HandleActiveTradeLifecycle()
        {
            if (HasPendingOrWorkingEntryLifecycle() && !HasActiveManagedTradeContext() && HasPlannedEntry())
            {
                DemoteToWaitingForTrigger();
                TryWaitForTrigger();
                return;
            }

            if (State != State.Realtime)
                UpdateManagedTradeProtection();

            if (CancelIfOppositeSignal
                && _hasWorkingEntry
                && !_entryFilledForActiveSignal
                && _activeBias != SecondLegBias.Neutral
                && HasPlannedEntry()
                && _activeBias != _plannedEntry.Bias)
            {
                CancelPendingEntry("OppositeSignal");
                if (_entryOrder == null && !_entryPending && !_hasWorkingEntry)
                    ResetSetupState("OppositeSignal");
                return;
            }

            if (_hasWorkingEntry && !_entryFilledForActiveSignal)
            {
                string armedEntryInvalidationReason = GetArmedEntryInvalidationReason();
                if (string.IsNullOrEmpty(armedEntryInvalidationReason))
                    armedEntryInvalidationReason = string.Empty;

                if (!string.IsNullOrEmpty(armedEntryInvalidationReason))
                {
                    CancelPendingEntry(armedEntryInvalidationReason);
                    if (_entryOrder == null && !_entryPending && !_hasWorkingEntry)
                        ResetSetupState(armedEntryInvalidationReason);
                    return;
                }
            }

            if (_tradeJustClosed)
            {
                _tradeJustClosed = false;
                ResetSetupState("TradeClosed");
                return;
            }

            if (!_hasWorkingEntry && !_entryFilledForActiveSignal && Position.Quantity == 0)
                ResetSetupState("ManagingNoPosition");
        }

        private bool SubmitPlannedEntry()
        {
            if (!HasPlannedEntry())
            {
                RecordEntryBlock("InvalidPlannedEntry", "plannedEntry=missing");
                return false;
            }

            if (HasWorkingPrimaryEntryForActiveSignal())
                return true;

            InitializeRuntimeCoreIfNeeded();
            if (!MaySubmitOrders("SubmitPlannedEntry"))
            {
                RecordEntryBlock("SubmissionAuthorityBlocked", "MaySubmitOrders=false");
                return false;
            }

            _activeEntrySignal = _plannedEntry.SignalName;
            _currentEntryTag = _plannedEntry.SignalName;
            currentTradeID = _plannedEntry.SignalName;
            _countedTradeSessionId = string.Empty;
            EnsureActiveTradeIdFromCurrentTradeId("SubmitPlannedEntry");

            tradeManager?.SetPlannedStop(_plannedEntry.InitialStopPrice, "SubmitPlannedEntry");
            ClearEntrySubmitInFlight("SubmitPlannedEntry");
            _entryPending = true;
            _hasWorkingEntry = true;
            _tradeJustClosed = false;
            _flattenInFlight = false;
            _bestFavorablePrice = _plannedEntry.EntryPrice;
            _simpleTrailArmed = false;

            tradeManager?.SubmitPrimaryEntry(_plannedEntry);
            WriteTradeContextLog(
                "ENTRY_SUBMIT",
                $"signal={_plannedEntry.SignalName}",
                $"trade={_plannedEntry.SignalName}",
                $"bias={_plannedEntry.Bias}",
                $"qty={_plannedEntry.Quantity}",
                $"entry={_plannedEntry.EntryPrice:F2}",
                $"stop={_plannedEntry.InitialStopPrice:F2}",
                $"reason={_plannedEntry.Reason}");

            RefreshRuntimeSnapshot("SubmitPlannedEntry");
            return true;
        }

        private void CancelPendingEntry(string reason)
        {
            if (CancelWorkingPrimaryEntries(reason))
            {
                RefreshRuntimeSnapshot(reason ?? "CancelPendingEntry");
                return;
            }

            if (_entryPending)
            {
                _hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();
                if (!_hasWorkingEntry)
                    _entryOrder = null;
                ClearEntrySubmitInFlight($"CancelPendingEntry.{reason ?? "Unknown"}");
                RefreshRuntimeSnapshot(reason ?? "CancelPendingEntryPendingHandle");
                return;
            }

            _entryPending = false;
            _hasWorkingEntry = false;
            _entryOrder = null;
            RefreshRuntimeSnapshot(reason ?? "CancelPendingEntry");
        }

        protected override void OnOrderUpdate(
            Order order,
            double limitPrice,
            double stopPrice,
            int quantity,
            int filled,
            double averageFillPrice,
            OrderState orderState,
            DateTime time,
            ErrorCode error,
            string comment)
        {
            if (!_hostShellReady || order == null)
                return;

            InitializeRuntimeCoreIfNeeded();
            SyncOrderMaintenance(order, orderState);

            if (TradeManagerIsPrimaryEntryOrder(order) || IsTrackedPrimaryEntryOrder(order))
            {
                BindPrimaryEntryTransportHandle(order, "OnOrderUpdate.Entry");
                TradeStateManager.AddActiveOrder(order);

                if (SecondLegOrderStateExtensions.IsWorkingLike(orderState))
                {
                    _hasWorkingEntry = true;
                    ClearEntrySubmitInFlight("OnOrderUpdate.EntryWorking");
                }
                else
                {
                    TradeStateManager.RemoveActiveOrder(order);
                    ClearEntrySubmitInFlight($"OnOrderUpdate.Entry{orderState}");
                    _entryPending = false;
                    _hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();
                    bool hasActualEntryFill = Math.Max(filled, order.Filled) > 0 || averageFillPrice > 0.0;

                    if (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
                    {
                        if (_entryOrder != null && string.Equals(_entryOrder.OrderId, order.OrderId, StringComparison.Ordinal))
                            _entryOrder = null;

                        if (!hasActualEntryFill
                            && !_entryFilledForActiveSignal
                            && Position.Quantity == 0
                            && !_hasWorkingEntry)
                        {
                            if (_flattenInFlight || isFinalizingTrade)
                                ResetTradeState();
                            else
                                ResetSetupState(orderState == OrderState.Rejected ? "EntryRejected" : "EntryCancelled");
                        }
                    }
                }
            }

            if (IsStrategyProtectiveStopOrder(order))
            {
                if (SecondLegOrderStateExtensions.IsWorkingLike(orderState))
                {
                    WriteRiskEvent(
                        "STOP_ACK",
                        $"state={orderState}",
                        $"qty={quantity}",
                        $"stop={stopPrice:F2}",
                        "ctx=OnOrderUpdate");
                    WriteRiskEvent(
                        "STOP_CONFIRMED",
                        $"state={orderState}",
                        $"qty={quantity}",
                        $"stop={stopPrice:F2}",
                        "ctx=OnOrderUpdate");
                    MaybeLogFirstStopSlaWorking(time, "OnOrderUpdate.ProtectiveWorking");
                    BindProtectiveTransportHandle(order, order.Oco ?? string.Empty, "OnOrderUpdate.Protective");
                    controllerStopPlaced = true;
                    hasWorkingStop = true;
                    workingStopPrice = stopPrice > 0.0 ? stopPrice : (HasPlannedEntry() ? _plannedEntry.InitialStopPrice : 0.0);
                    currentControllerStopPrice = workingStopPrice;
                    _coverageGraceUntil = DateTime.MinValue;
                    _stopSubmissionPending = false;
                    _stopSubmitInFlight = false;
                    SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                    SetProtectiveCoverageDisposition("covered-owned", "OnOrderUpdate");
                }
                else if (orderState == OrderState.Cancelled || orderState == OrderState.Rejected || orderState == OrderState.Filled)
                {
                    if (orderState == OrderState.Cancelled)
                    {
                        WriteRiskEvent(
                            "STOP_CANCELLED_ACK",
                            $"qty={quantity}",
                            $"filled={filled}",
                            $"stop={stopPrice:F2}",
                            "ctx=OnOrderUpdate");
                    }
                    else if (orderState == OrderState.Filled)
                    {
                        WriteRiskEvent(
                            "STOP_FILLED_ACK",
                            $"qty={quantity}",
                            $"filled={filled}",
                            $"stop={stopPrice:F2}",
                            "ctx=OnOrderUpdate");
                    }

                    bool isProtectiveReplaceTarget = IsProtectiveReplaceTarget(order);
                    bool preservePendingCoverageDuringReplace =
                        orderState == OrderState.Cancelled
                        && !isProtectiveReplaceTarget
                        && Position.Quantity != 0
                        && (_protectiveReplacePending || _stopSubmissionPending || _stopSubmitInFlight);

                    if (isProtectiveReplaceTarget)
                    {
                        _protectiveReplaceRejected = orderState == OrderState.Rejected;
                        if (orderState == OrderState.Rejected)
                            SetRecoveryHoldState(false, false, true, true, false, string.Empty);
                    }

                    _lastStopStateChangeAt = DateTime.UtcNow;
                    if (!preservePendingCoverageDuringReplace)
                    {
                        _stopSubmissionPending = false;
                        _stopSubmitInFlight = false;
                    }
                    RecalculateProtectiveCoverageState();
                    if (orderState != OrderState.Filled)
                        ValidateStopQuantity("OnOrderUpdate");
                    if (orderState == OrderState.Rejected
                        && Position.Quantity != 0
                        && !HasWorkingProtectiveCoverage())
                    {
                        WriteTradeLifecycleLog(
                            "EXIT_REJECTED",
                            order,
                            $"qty={quantity}",
                            $"filled={filled}",
                            $"avgFill={averageFillPrice:F2}",
                            $"comment={comment}");
                        RecordEntryBlock("ProtectiveStopRejected", $"order={order.Name} comment={comment}");
                        if (exitCtl != null)
                            exitCtl.OnOrderRejected(order);
                        else
                            TriggerFlatten("ProtectiveStopRejected");
                    }
                }
            }

            if (IsStrategyFlattenOrder(order))
            {
                if (SecondLegOrderStateExtensions.IsWorkingLike(orderState))
                {
                    BindFlattenTransportHandle(order, "OnOrderUpdate.Flatten");
                    WriteTradeLifecycleLog(
                        "FLATTEN_SUBMIT",
                        order,
                        $"qty={quantity}",
                        $"filled={filled}",
                        $"avgFill={averageFillPrice:F2}");
                }
                else if (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
                {
                    WriteTradeLifecycleLog(
                        orderState == OrderState.Rejected ? "FLATTEN_REJECTED" : "FLATTEN_CANCELLED",
                        order,
                        $"qty={quantity}",
                        $"filled={filled}",
                        $"avgFill={averageFillPrice:F2}",
                        $"comment={comment}");
                }
            }

            bool terminalOrderState =
                orderState == OrderState.Cancelled
                || orderState == OrderState.Rejected
                || orderState == OrderState.Filled;
            if (terminalOrderState)
                ReleaseBoundTransportHandle(order, $"OnOrderUpdate.{orderState}");

            if (error != ErrorCode.NoError && !string.IsNullOrEmpty(comment))
                RecordEntryBlock(
                    "EntryOrderError",
                    $"order={order.Name} state={orderState} error={error} comment={comment}");

            PumpExitOps("OnOrderUpdate");
            ContinueFlattenProtocol("OnOrderUpdate");
            RefreshRuntimeSnapshot("OnOrderUpdate");
        }

        protected override void OnExecutionUpdate(
            Execution execution,
            string executionId,
            double price,
            int quantity,
            MarketPosition marketPosition,
            string orderId,
            DateTime time)
        {
            if (!_hostShellReady || execution?.Order == null)
                return;

            InitializeRuntimeCoreIfNeeded();
            TradeStateManager.OnExecutionUpdate(execution, price);

            Order order = execution.Order;
            if (TradeManagerIsPrimaryEntryOrder(order) || IsTrackedPrimaryEntryOrder(order))
            {
                TrackCumulativeFillQuantity(Math.Max(order.Filled, quantity), "OnExecutionUpdate.Entry");

                if (!_entryFilledForActiveSignal)
                {
                    entryPositionSide = order.OrderAction == OrderAction.SellShort
                        ? MarketPosition.Short
                        : MarketPosition.Long;
                    entryFillTime = time;
                    AnchorEntryExecutionState(execution, price, "EntryFill");
                    CountTradeOnFirstEntryFill(execution);
                }

                double blendedEntryPrice = Position.AveragePrice > 0.0
                    ? Position.AveragePrice
                    : (order.AverageFillPrice > 0.0 ? order.AverageFillPrice : price);

                entryFillPrice = blendedEntryPrice;
                entryPrice = blendedEntryPrice;
                avgEntryPrice = blendedEntryPrice;
                entryQuantity = Math.Max(Math.Abs(Position.Quantity), Math.Max(order.Filled, quantity));
                initialPositionSize = entryQuantity;
                initialStopPrice = HasPlannedEntry() ? _plannedEntry.InitialStopPrice : currentControllerStopPrice;
                tradeRiskPerContract = Math.Max(TickSize, Math.Abs(entryPrice - initialStopPrice)) * Instrument.MasterInstrument.PointValue;
                initialTradeRisk = tradeRiskPerContract * Math.Max(1, entryQuantity);
                _bestFavorablePrice = entryPrice;

                _entryFilledForActiveSignal = true;
                _hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal()
                    || (order.OrderState != OrderState.Filled && SecondLegOrderStateExtensions.IsWorkingLike(order.OrderState));
                _entryPending = _hasWorkingEntry;
                _tradeJustClosed = false;
                tradeOpen = true;
                exitCtl?.OnEntryFilled(_activeEntrySignal, blendedEntryPrice, entryPositionSide, entryQuantity);
                ClearEntrySubmitInFlight("OnExecutionUpdate.EntryFill");
                bool sessionControlFlattenTriggered = MaybeTriggerSessionControlFlattenOnEntryFill(time, "EntryFill");
                ValidateStopQuantity("EntryFill");
                PromoteToManagingTrade();
                _accountPositionQuantity = Math.Abs(Position.Quantity);
                _accountAveragePrice = Position.AveragePrice;
                SaveStrategyState();
                if (sessionControlFlattenTriggered)
                    RecordEntryBlock("SessionControlFlatten", "entry fill hit session control");
                WriteTradeLifecycleLog(
                    "ENTRY_FILL",
                    order,
                    $"execId={executionId}",
                    $"qty={quantity}",
                    $"fillPrice={price:F2}",
                    $"avgEntry={entryPrice:F2}",
                    $"posQty={Position.Quantity}",
                    $"sessionControlFlatten={sessionControlFlattenTriggered}");
                PumpExitOps("OnExecutionUpdate.Entry");
                ContinueFlattenProtocol("OnExecutionUpdate.Entry");
                RefreshRuntimeSnapshot("OnExecutionUpdate.Entry");
                return;
            }

            bool hasActiveTradeContext =
                _entryFilledForActiveSignal
                || entryPositionSide != MarketPosition.Flat
                || entryFillTime != DateTime.MinValue
                || !string.IsNullOrEmpty(currentTradeID);

            if (hasActiveTradeContext
                && Position.Quantity == 0
                && IsOwnedTradeClosingExecutionOrder(order))
            {
                WriteTradeLifecycleLog(
                    "EXIT_FILL",
                    order,
                    $"execId={executionId}",
                    $"qty={quantity}",
                    $"fillPrice={price:F2}",
                    $"marketPosition={marketPosition}");
                UpdateSessionRiskFromCompletedTrades();
                WriteTradeLifecycleLog(
                    "TRADE_CLOSE",
                    order,
                    $"execId={executionId}",
                    $"qty={quantity}",
                    $"exitPrice={price:F2}",
                    $"marketPosition={marketPosition}",
                    BuildLatestClosedTradeSummary().Trim());
                _tradeJustClosed = true;
                if (!_flattenInFlight && !isFinalizingTrade)
                    ResetTradeState();
                PumpExitOps("OnExecutionUpdate.Flat");
                ContinueFlattenProtocol("OnExecutionUpdate.Flat");
                RefreshRuntimeSnapshot("OnExecutionUpdate.Flat");
            }
        }

        protected override void OnPositionUpdate(Cbi.Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            _accountPositionQuantity = Math.Abs(quantity);
            _accountAveragePrice = averagePrice;
            if (quantity != 0)
            {
                _hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();
                _entryPending = _hasWorkingEntry;
                _tradeJustClosed = false;
                tradeOpen = true;
                ValidateStopQuantity("OnPositionUpdate");
            }
            else
            {
                bool hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();
                _hasWorkingEntry = hasWorkingEntry;
                _entryPending = hasWorkingEntry;
                if (!_tradeJustClosed
                && !hasWorkingEntry
                && (_entryFilledForActiveSignal
                || entryPositionSide != MarketPosition.Flat
                || entryFillTime != DateTime.MinValue
                || !string.IsNullOrEmpty(currentTradeID)))
                {
                    UpdateSessionRiskFromCompletedTrades();
                    _tradeJustClosed = true;
                    if (!_flattenInFlight && !isFinalizingTrade)
                        ResetTradeState();
                }
            }

            WriteDebugEvent(
                "POSITION_UPDATE",
                $"marketPosition={marketPosition}",
                $"qty={quantity}",
                $"avg={averagePrice:F2}",
                $"activeSignal={_activeEntrySignal}",
                $"workingEntry={_hasWorkingEntry}",
                $"flattenInFlight={_flattenInFlight}",
                $"finalizing={isFinalizingTrade}");
            PumpExitOps("OnPositionUpdate");
            ContinueFlattenProtocol("OnPositionUpdate");
            RefreshRuntimeSnapshot("OnPositionUpdate");
        }

        private void SyncOrderMaintenance(Order order, OrderState orderState)
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();

            if (SecondLegOrderStateExtensions.IsWorkingLike(orderState))
                OrderMaintenance.TrackWorkingOrder(state, order, orderState);
            else
                OrderMaintenance.ReleaseTerminalOrder(state, order);

            ApplyOrderMaintenanceState(state);
        }

        private void RecalculateProtectiveCoverageState()
        {
            bool hasProtectiveCoverage = HasWorkingProtectiveCoverage();
            hasWorkingStop = hasProtectiveCoverage;
            controllerStopPlaced = hasProtectiveCoverage;

            if (!hasProtectiveCoverage)
            {
                workingStopPrice = 0.0;
                currentControllerStopPrice = 0.0;
                SetProtectiveCoverageDisposition(string.Empty, "RecalculateProtectiveCoverageState");
                return;
            }

            foreach (Order order in _workingOrders)
            {
                if (!IsWorkingProtectiveStopOrder(order))
                    continue;

                double stopPrice = order.StopPrice;
                if (stopPrice > 0.0)
                {
                    workingStopPrice = stopPrice;
                    currentControllerStopPrice = stopPrice;
                    break;
                }
            }

            SetProtectiveCoverageDisposition("covered-owned", "RecalculateProtectiveCoverageState");
        }

        private bool HasWorkingProtectiveCoverage()
        {
            foreach (Order order in _workingOrders)
            {
                if (IsWorkingProtectiveStopOrder(order))
                    return true;
            }

            return false;
        }

        private bool IsWorkingProtectiveStopOrder(Order order)
        {
            if (order == null || !SecondLegOrderStateExtensions.IsWorkingLike(order.OrderState))
                return false;

            return IsStrategyProtectiveStopOrder(order);
        }

        private bool IsStrategyProtectiveStopOrder(Order order)
        {
            if (order == null)
                return false;

            if (!SecondLegOrderExtensions.IsProtectiveStop(order))
                return false;

            if (TryGetOwnedProtectiveSignal(order, out string ownedSignal))
                return MatchesOwnedPrimaryEntrySignal(ownedSignal);

            return MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);
        }

        private bool IsTrackedPrimaryEntryOrder(Order order)
        {
            return IsPrimaryEntryOrder(order);
        }

        private bool IsOwnedTradeClosingExecutionOrder(Order order)
        {
            if (order == null)
                return false;

            return IsStrategyProtectiveStopOrder(order) || IsStrategyFlattenOrder(order);
        }

        private bool HasWorkingPrimaryEntryForActiveSignal()
        {
            if (_entryOrder != null
                && IsTrackedPrimaryEntryOrder(_entryOrder)
                && SecondLegOrderStateExtensions.IsWorkingLike(_entryOrder.OrderState))
            {
                return true;
            }

            foreach (Order order in _workingOrders)
            {
                if (IsTrackedPrimaryEntryOrder(order) && SecondLegOrderStateExtensions.IsWorkingLike(order.OrderState))
                    return true;
            }

            return false;
        }

        private bool CancelWorkingPrimaryEntries(string reason)
        {
            bool cancelled = false;
            string tag = reason ?? "CancelPendingEntry";
            var seenOrderIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            void CancelCandidate(Order candidate)
            {
                if (candidate == null
                    || !IsTrackedPrimaryEntryOrder(candidate)
                    || !CanCancelOrderForOrderMaintenance(candidate))
                {
                    return;
                }

                string orderId = candidate.OrderId ?? string.Empty;
                if (!string.IsNullOrEmpty(orderId) && seenOrderIds.Contains(orderId))
                    return;

                SafeCancelOrder(candidate, tag);

                if (!string.IsNullOrEmpty(orderId))
                    seenOrderIds.Add(orderId);
                cancelled = true;
            }

            CancelCandidate(_entryOrder);
            foreach (Order order in _workingOrders)
                CancelCandidate(order);

            if (cancelled)
            {
                _entryPending = false;
                _hasWorkingEntry = false;
                ClearEntrySubmitInFlight($"CancelWorkingPrimaryEntries.{tag}");
            }

            return cancelled;
        }

        private void UpdateSessionRiskFromCompletedTrades()
        {
            int tradeCount = SystemPerformance.AllTrades.Count;
            if (tradeCount <= _lastProcessedTradeCount)
                return;

            Trade trade = SystemPerformance.AllTrades[tradeCount - 1];
            if (trade == null)
                return;

            double pnlCurrency = trade.ProfitCurrency;
            double riskBasis = initialTradeRisk > 0.0 ? initialTradeRisk : Math.Max(1.0, RiskPerTrade);
            _sessionRealizedR += pnlCurrency / riskBasis;

            if (pnlCurrency < 0.0)
            {
                _consecutiveLosses++;
                if (CooldownBarsAfterLoss > 0)
                    _lossCooldownUntilBar = Math.Max(_lossCooldownUntilBar, ClosedBarIndex() + CooldownBarsAfterLoss);
            }
            else if (pnlCurrency > 0.0)
                _consecutiveLosses = 0;

            _lastProcessedTradeCount = tradeCount;
        }

        private bool UpdateManagedTradeProtection(double? marketPrice = null)
        {
            if (!_entryFilledForActiveSignal || Position.Quantity == 0 || initialStopPrice <= 0.0)
                return false;

            double updatedStop = currentControllerStopPrice > 0.0 ? currentControllerStopPrice : initialStopPrice;
            if (entryPositionSide == MarketPosition.Long)
            {
                double favorablePrice = marketPrice ?? ClosedBarHigh();
                _bestFavorablePrice = Math.Max(_bestFavorablePrice, favorablePrice);

                if (TrailEnabled)
                {
                    double triggerPrice = entryPrice + TrailTriggerPoints;
                    if (!_simpleTrailArmed && _bestFavorablePrice >= triggerPrice)
                    {
                        _simpleTrailArmed = true;
                        double lockStop = Instrument.MasterInstrument.RoundToTickSize(entryPrice + TrailLockPoints);
                        updatedStop = Math.Max(updatedStop, lockStop);
                    }

                    if (_simpleTrailArmed)
                    {
                        double trailStop = Instrument.MasterInstrument.RoundToTickSize(_bestFavorablePrice - TrailDistancePoints);
                        double lockStop = Instrument.MasterInstrument.RoundToTickSize(entryPrice + TrailLockPoints);
                        updatedStop = Math.Max(updatedStop, Math.Max(lockStop, trailStop));
                    }
                }
            }
            else if (entryPositionSide == MarketPosition.Short)
            {
                double favorablePrice = marketPrice ?? ClosedBarLow();
                _bestFavorablePrice = _bestFavorablePrice <= 0.0 ? favorablePrice : Math.Min(_bestFavorablePrice, favorablePrice);

                if (TrailEnabled)
                {
                    double triggerPrice = entryPrice - TrailTriggerPoints;
                    if (!_simpleTrailArmed && _bestFavorablePrice <= triggerPrice)
                    {
                        _simpleTrailArmed = true;
                        double lockStop = Instrument.MasterInstrument.RoundToTickSize(entryPrice - TrailLockPoints);
                        updatedStop = Math.Min(updatedStop, lockStop);
                    }

                    if (_simpleTrailArmed)
                    {
                        double trailStop = Instrument.MasterInstrument.RoundToTickSize(_bestFavorablePrice + TrailDistancePoints);
                        double lockStop = Instrument.MasterInstrument.RoundToTickSize(entryPrice - TrailLockPoints);
                        updatedStop = Math.Min(updatedStop, Math.Min(lockStop, trailStop));
                    }
                }
            }

            if (Math.Abs(updatedStop - currentControllerStopPrice) < TickSize * 0.5)
                return false;

            currentControllerStopPrice = updatedStop;
            workingStopPrice = updatedStop;

            Order workingStop = FindWorkingExitForRole(SecondLegOrderRole.StopLoss);
            if (HasWorkingProtectiveCoverage() || workingStop != null)
                exitCtl?.ChangeUnmanaged(workingStop, updatedStop, "SimpleTrail");
            else
                EnqueueEnsureProtectiveExit(updatedStop, Math.Abs(Position.Quantity), "StopLoss_PrimaryEntry", "SimpleTrailReArm");

            RefreshRuntimeSnapshot("UpdateManagedTradeProtection");
            return true;
        }
    }
}
