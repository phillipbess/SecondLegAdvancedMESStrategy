using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies
{
    internal static class SubmissionAuthorityCtxTokens
    {
        public const string StopLoss = "StopLoss";
        public const string Protective = "PROTECTIVE";
        public const string ExitController = "ExitController";
        public const string Emergency = "EMERGENCY";
        public const string Adopt = "ADOPT";
        public const string Exit = "Exit";
    }

    internal enum SubmissionAuthorityCancellationScope
    {
        AllOrders = 0,
    }

    internal sealed class SubmissionAuthorityState
    {
        public bool IsFinalizingTrade { get; set; }
        public bool SuppressAllOrderSubmissions { get; set; }
        public bool TradeOpen { get; set; }
        public bool ControllerStopPlaced { get; set; }
        public bool AutoDisabled { get; set; }
        public bool GlobalKillSwitch { get; set; }
        public bool StopSubmitInFlight { get; set; }
        public bool StopSubmissionPending { get; set; }
        public DateTime LastStopSubmitAtUtc { get; set; }
        public DateTime LastStopSubmissionAtUtc { get; set; }
        public int PositionQuantity { get; set; }

        // TODO(host-shell): bind to the strategy's retry-correlation map so finalization
        // clears the same state the donor runtime clears.
        public IDictionary<string, string> PendingRetryCorrelationBySignal { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    internal sealed class SubmissionAuthorityHostContract
    {
        public Action<string> WriteDebugLog { get; set; }
        public Action<string> WriteRiskLog { get; set; }
        public Action<string> CancelAllWorkingChildrenAndWait { get; set; }
        public Action<string> PrintOrderHealthSummary { get; set; }
        public Action<string, SubmissionAuthorityCancellationScope> CancelAllWorkingOrders { get; set; }
        public Func<string, TimeSpan, DateTime, bool> EmitOncePer { get; set; }
        public Func<int> StopSubmitCooldownMsCurrent { get; set; }
        public Func<DateTime> NowEt { get; set; }
        public Func<DateTime, string> Stamp { get; set; }

        public bool IsActivationReady
        {
            get
            {
                return CancelAllWorkingChildrenAndWait != null
                    && PrintOrderHealthSummary != null
                    && CancelAllWorkingOrders != null
                    && StopSubmitCooldownMsCurrent != null
                    && NowEt != null
                    && Stamp != null;
            }
        }

        public string DescribeMissingActivationHooks()
        {
            List<string> missing = new List<string>();

            if (CancelAllWorkingChildrenAndWait == null)
                missing.Add(nameof(CancelAllWorkingChildrenAndWait));

            if (PrintOrderHealthSummary == null)
                missing.Add(nameof(PrintOrderHealthSummary));

            if (CancelAllWorkingOrders == null)
                missing.Add(nameof(CancelAllWorkingOrders));

            if (StopSubmitCooldownMsCurrent == null)
                missing.Add(nameof(StopSubmitCooldownMsCurrent));

            if (NowEt == null)
                missing.Add(nameof(NowEt));

            if (Stamp == null)
                missing.Add(nameof(Stamp));

            return missing.Count == 0 ? string.Empty : string.Join(", ", missing);
        }
    }

    internal sealed class SubmissionAuthorityScaffold
    {
        private readonly SubmissionAuthorityHostContract _host;

        public SubmissionAuthorityScaffold(SubmissionAuthorityHostContract host)
        {
            _host = host ?? new SubmissionAuthorityHostContract();
        }

        public void BeginAtomicFinalization(SubmissionAuthorityState state, string reason)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (state.IsFinalizingTrade)
            {
                Debug($"[FINALIZE] Already finalizing, ignoring duplicate call | reason={reason}");
                return;
            }

            state.IsFinalizingTrade = true;
            state.SuppressAllOrderSubmissions = true;
            state.TradeOpen = false;
            state.ControllerStopPlaced = false;
            state.PendingRetryCorrelationBySignal.Clear();

            if (!_host.IsActivationReady)
            {
                Debug(
                    "[FINALIZE][TODO] Host shell must wire cancel/health/timing hooks before live activation | missing="
                    + _host.DescribeMissingActivationHooks());
                return;
            }

            _host.CancelAllWorkingChildrenAndWait("[FINALIZE]");
            _host.PrintOrderHealthSummary("TradeComplete");
            Debug($"[FINALIZE] ATOMIC BEGIN | reason={reason} | GLOBAL HALT ACTIVE | {StampNow()}");
            _host.CancelAllWorkingOrders(reason ?? string.Empty, SubmissionAuthorityCancellationScope.AllOrders);
        }

        public bool MaySubmitOrders(SubmissionAuthorityState state, string context)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            context = context ?? string.Empty;

            if (IsProtectiveContext(context))
            {
                if (state.PositionQuantity == 0)
                {
                    Debug($"[GUARD] Protective stop blocked (flat) | qty={state.PositionQuantity} | context={context}");
                    return false;
                }

                if (state.StopSubmitInFlight || state.StopSubmissionPending)
                {
                    DateTime lastSubmitAt = state.LastStopSubmitAtUtc;
                    if (state.LastStopSubmissionAtUtc > lastSubmitAt)
                        lastSubmitAt = state.LastStopSubmissionAtUtc;

                    if (lastSubmitAt != DateTime.MinValue && _host.StopSubmitCooldownMsCurrent != null)
                    {
                        double msSinceSubmit = (DateTime.UtcNow - lastSubmitAt).TotalMilliseconds;
                        int cooldownMs = _host.StopSubmitCooldownMsCurrent();
                        if (msSinceSubmit > cooldownMs)
                        {
                            if (_host.EmitOncePer == null
                                || _host.EmitOncePer("submission_authority_stale_latch_release", TimeSpan.FromSeconds(5), DateTime.UtcNow))
                            {
                                Risk(
                                    $"[STOP_INFLIGHT_STALE] Released stale latch | age={msSinceSubmit:F0}ms > cooldown={cooldownMs}ms");
                            }

                            state.StopSubmitInFlight = false;
                            state.StopSubmissionPending = false;
                        }
                    }
                }

                if (state.StopSubmitInFlight)
                {
                    Debug($"[GUARD] Protective stop blocked by in-flight latch | context={context}");
                    return false;
                }

                Debug($"[GUARD] Protective stop allowed (autoDisabled={state.AutoDisabled}) | context={context}");
                return true;
            }

            if (state.GlobalKillSwitch)
            {
                Debug($"[GUARD] Order submission prevented by global kill switch | context={context}");
                return false;
            }

            if (state.SuppressAllOrderSubmissions)
            {
                Debug($"[GUARD] Order submission prevented by global suppression | context={context}");
                return false;
            }

            if (state.AutoDisabled)
            {
                Debug($"[GUARD] Order submission prevented by auto-disable | context={context}");
                return false;
            }

            if (state.IsFinalizingTrade)
            {
                Debug($"[GUARD] Order submission prevented by finalization | context={context}");
                return false;
            }

            if (state.PositionQuantity == 0 && ContainsToken(context, SubmissionAuthorityCtxTokens.Exit))
            {
                Debug($"[GUARD] Exit order prevented - position already flat | context={context}");
                return false;
            }

            return true;
        }

        private static bool IsProtectiveContext(string context)
        {
            return ContainsToken(context, SubmissionAuthorityCtxTokens.StopLoss)
                || ContainsToken(context, SubmissionAuthorityCtxTokens.Protective)
                || ContainsToken(context, SubmissionAuthorityCtxTokens.ExitController)
                || ContainsToken(context, SubmissionAuthorityCtxTokens.Emergency)
                || ContainsToken(context, SubmissionAuthorityCtxTokens.Adopt);
        }

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Debug(string message)
        {
            _host.WriteDebugLog?.Invoke(message);
        }

        private void Risk(string message)
        {
            _host.WriteRiskLog?.Invoke(message);
        }

        private string StampNow()
        {
            if (_host.NowEt == null || _host.Stamp == null)
                return DateTime.UtcNow.ToString("O");

            return _host.Stamp(_host.NowEt());
        }
    }
}
