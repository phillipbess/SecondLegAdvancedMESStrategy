using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private string _loggingRunId = string.Empty;
        private string _loggingBasePath = string.Empty;
        private string _tradeLogPath = string.Empty;
        private string _patternLogPath = string.Empty;
        private string _riskLogPath = string.Empty;
        private string _debugLogPath = string.Empty;
        private bool _loggingInitialized;
        private readonly object _logFileLock = new object();
        private readonly HashSet<string> _logHeadersWritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _firstStopSlaLogged;
        private bool _firstStopSlaMissLogged;
        private static readonly string[] TradeLogEvents =
        {
            "ENTRY_SUBMIT",
            "ENTRY_FILL",
            "EXIT_FILL",
            "EXIT_REJECTED",
            "FLATTEN_REQUEST",
            "FLATTEN_SUBMIT",
            "FLATTEN_REJECTED",
            "FLATTEN_CANCELLED",
            "FLATTEN_COMPLETE",
            "TRADE_CLOSE",
            "RECOVERY_RESOLUTION",
            "RECOVERY_RECONNECT_GRACE",
            "RECOVERY_RECONNECT_OUTCOME",
            "PROTECTIVE_REPLACE_BEGIN",
            "PROTECTIVE_REPLACE_END",
            "PROTECTIVE_REPLACE_FAIL",
            "PROTECTIVE_REPLACE_COMMIT",
            "TRANSPORT_BIND",
            "TRANSPORT_RELEASE",
        };
        private static readonly string[] PatternLogEvents =
        {
            "ENTRY_STATE",
            "ENTRY_BLOCK",
            "ENTRY_ARMED",
            "IMPULSE_QUALIFIED",
        };
        private static readonly string[] RiskLogEvents =
        {
            "STOP_SUBMIT",
            "STOP_CHANGE",
            "STOP_ACK",
            "STOP_CONFIRMED",
            "STOP_CANCELLED_ACK",
            "STOP_FILLED_ACK",
            "STOP_QTY_MISMATCH",
            "DOUBLE STOP DETECTED",
            "ORPHAN_CHECK",
            "ORPHAN_SWEEP",
            "OM_HEALTH",
            "FIRST_STOP_SLA",
            "ADOPT",
            "OCO_RESUBMIT",
            "COVERAGE_STATE",
            "PROTECTIVE_COVERAGE",
            "FLATTEN_RECOVERY",
            "SUBMISSION_AUTHORITY",
        };
        private static readonly string[] DebugLogEvents =
        {
            "LOG_PATHS",
            "LOG_CLEANUP",
            "FLATTEN",
            "FLATTEN_REPRICE",
            "RECONNECT_GRACE",
            "RECONNECT_OBSERVATION",
            "ENTRY_BLOCK",
            "POSITION_UPDATE",
            "EXIT_OP_ENQ",
            "EXIT_OP_BEGIN",
            "EXIT_OP_END",
            "EXIT_OP_RETRY",
            "EXIT_OP_DROP",
            "EXIT_OP_CARVEOUT",
            "EXIT_OP_TIMEOUT",
            "EXIT_OP_ERROR",
            "EXIT_OP_TIMEOUT_RELEASE",
            "TRANSPORT_BIND",
            "TRANSPORT_RELEASE",
            "PROTECTIVE_REPLACE_BEGIN",
            "PROTECTIVE_REPLACE_END",
            "PROTECTIVE_REPLACE_FAIL",
            "PROTECTIVE_REPLACE_COMMIT",
            "CANCEL_ALL",
            "STATE_RESTORE",
            "STATE_SAVE_ERROR",
            "STATE_RESTORE_ERROR",
            "HARNESS_STATE",
            "TRADE_MANAGER",
        };

        private double _lastImpulseRetracement;
        private double _lastLeg2Momentum;
        private double _lastImpulseMomentum;
        private int _lastImpulseStrongBars;
        private string _lastStructureLabel = string.Empty;
        private double _lastStructurePrice = double.NaN;
        private double _lastStructureRoom = double.NaN;
        private double _lastStructureRequiredRoom = double.NaN;

        private void InitializeLogging()
        {
            if (_loggingInitialized)
                return;

            string accountName = Account?.DisplayName ?? Account?.Name ?? "UnknownAccount";
            string instrumentName = Instrument?.MasterInstrument?.Name ?? "UnknownInstrument";
            string safeAccountName = SanitizeStatePathSegment(accountName);
            string safeInstrumentName = SanitizeStatePathSegment(instrumentName);
            DateTime baseTime = (Bars != null && Bars.Count > 0) ? Time[0] : DateTime.Now;
            DateTime sessionDate = baseTime.Date;

            _loggingRunId = $"{sessionDate:yyyyMMdd}_{safeInstrumentName}_{safeAccountName}";
            _loggingBasePath = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "logs", "SecondLegAdvancedMES");
            Directory.CreateDirectory(_loggingBasePath);

            _tradeLogPath = Path.Combine(_loggingBasePath, $"Trades_{_loggingRunId}.txt");
            _patternLogPath = Path.Combine(_loggingBasePath, $"Patterns_{_loggingRunId}.txt");
            _riskLogPath = Path.Combine(_loggingBasePath, $"Risk_{_loggingRunId}.txt");
            _debugLogPath = Path.Combine(_loggingBasePath, $"Debug_{_loggingRunId}.txt");

            EnsureLogHeader(_tradeLogPath, "TRADE");
            EnsureLogHeader(_patternLogPath, "PATTERN");
            EnsureLogHeader(_riskLogPath, "RISK");
            EnsureLogHeader(_debugLogPath, "DEBUG");
            _loggingInitialized = true;

            WriteDebugLog($"[LOG PATHS] Trades={_tradeLogPath}");
            WriteDebugLog($"[LOG PATHS] Patterns={_patternLogPath}");
            WriteDebugLog($"[LOG PATHS] Risk={_riskLogPath}");
            WriteDebugLog($"[LOG PATHS] Debug={_debugLogPath}");
        }

        private void CleanupLogging()
        {
            if (!_loggingInitialized)
                return;

            WriteDebugLog($"[LOG CLEANUP] run={_loggingRunId}");
            _loggingInitialized = false;
            _logHeadersWritten.Clear();
            _loggingRunId = string.Empty;
            _loggingBasePath = string.Empty;
            _tradeLogPath = string.Empty;
            _patternLogPath = string.Empty;
            _riskLogPath = string.Empty;
            _debugLogPath = string.Empty;
        }

        private void EnsureLogHeader(string filePath, string logType)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            lock (_logFileLock)
            {
                if (_logHeadersWritten.Contains(filePath))
                    return;

                bool firstWrite = !File.Exists(filePath);
                using (var writer = new StreamWriter(filePath, true, Encoding.UTF8, 64 * 1024))
                {
                    if (firstWrite)
                    {
                        writer.WriteLine($"=== NEW RUN === {logType} ===");
                        writer.WriteLine($"Strategy: SecondLegAdvanced {StrategyVersion}");
                        writer.WriteLine($"RunID: {_loggingRunId}");
                        writer.WriteLine($"Instrument: {Instrument?.MasterInstrument?.Name ?? "UnknownInstrument"}");
                        writer.WriteLine($"Account: {Account?.DisplayName ?? Account?.Name ?? "UnknownAccount"}");
                        writer.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                        writer.WriteLine($"Schema: {DescribeLogSchema(logType)}");
                        writer.WriteLine($"Vocabulary: {string.Join(", ", GetLogVocabulary(logType))}");
                        writer.WriteLine("========================================");
                    }
                }

                _logHeadersWritten.Add(filePath);
            }
        }

        private void WriteToLogFile(string filePath, string logType, string message)
        {
            try
            {
                if (!_loggingInitialized)
                    InitializeLogging();

                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                EnsureLogHeader(filePath, logType);

                string timestamp = StampLoggingTime();
                lock (_logFileLock)
                {
                    using (var writer = new StreamWriter(filePath, true, Encoding.UTF8, 64 * 1024))
                    {
                        writer.WriteLine($"{timestamp} | {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"[LOG ERROR] type={logType} msg={ex.Message}");
            }
        }

        private string StampLoggingTime()
        {
            DateTime now = (Bars != null && Bars.Count > 0 && CurrentBar >= 0) ? Time[0] : DateTime.Now;
            return now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private static string DescribeLogSchema(string logType)
        {
            return logType switch
            {
                "TRADE" => "timestamp | [EVENT] signal=<id> trade=<id> role=<entry|protective|flatten|other> qty=<n> order=<name> orderId=<id> state=<state> key=value ...",
                "PATTERN" => "timestamp | [EVENT] bar=<index> time=<hh:mm:ss> state=<state> bias=<side> trend=<bool> session=<bool> atrRegime=<bool> atr=<value> atrRatio=<value> slopeAtrPct=<value> impulseRange=<value> impulseStrongBars=<n> retracement=<value> leg2Momentum=<value> impulseMomentum=<value> structure=<label> signalBar=<index|none> planned=<summary> | detail",
                "RISK" => "timestamp | [EVENT] key=value ...",
                "DEBUG" => "timestamp | [EVENT] key=value ...",
                _ => "timestamp | [EVENT] key=value ...",
            };
        }

        private static IReadOnlyList<string> GetLogVocabulary(string logType)
        {
            return logType switch
            {
                "TRADE" => TradeLogEvents,
                "PATTERN" => PatternLogEvents,
                "RISK" => RiskLogEvents,
                "DEBUG" => DebugLogEvents,
                _ => Array.Empty<string>(),
            };
        }

        private void WritePatternLog(string message)
        {
            WriteToLogFile(_patternLogPath, "PATTERN", message);
        }

        private void WriteTradeLog(string message)
        {
            WriteToLogFile(_tradeLogPath, "TRADE", message);
        }

        private static string JoinStructuredFields(params string[] fields)
        {
            if (fields == null || fields.Length == 0)
                return string.Empty;

            return string.Join(" ", fields.Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => field.Trim()));
        }

        private void WriteTradeContextLog(string eventName, params string[] detailFields)
        {
            string safeEventName = string.IsNullOrWhiteSpace(eventName) ? "UNKNOWN" : eventName.Trim();
            string[] baseFields =
            {
                $"signal={_activeEntrySignal}",
                $"trade={currentTradeID}",
            };
            string fields = JoinStructuredFields(baseFields.Concat(detailFields ?? Array.Empty<string>()).ToArray());
            string message = string.IsNullOrEmpty(fields)
                ? $"[{safeEventName}]"
                : $"[{safeEventName}] {fields}";
            WriteTradeLog(message);
        }

        private void WriteRiskLog(string message)
        {
            WriteToLogFile(_riskLogPath, "RISK", message);
        }

        private void WriteRiskEvent(string eventName, params string[] detailFields)
        {
            string safeEventName = string.IsNullOrWhiteSpace(eventName) ? "UNKNOWN" : eventName.Trim();
            string fields = JoinStructuredFields(detailFields);
            string message = string.IsNullOrEmpty(fields)
                ? $"[{safeEventName}]"
                : $"[{safeEventName}] {fields}";
            WriteRiskLog(message);
        }

        private void WriteDebugEvent(string eventName, params string[] detailFields)
        {
            string safeEventName = string.IsNullOrWhiteSpace(eventName) ? "UNKNOWN" : eventName.Trim();
            string fields = JoinStructuredFields(detailFields);
            string message = string.IsNullOrEmpty(fields)
                ? $"[{safeEventName}]"
                : $"[{safeEventName}] {fields}";
            WriteDebugLog(message);
        }

        private void WriteEntryObservation(string eventName, string detail = "")
        {
            string context = BuildEntryObservationContext(detail);
            WritePatternLog($"[{eventName}] {context}");
        }

        private void ResetOmCompatibilityTracking()
        {
            _firstStopSlaLogged = false;
            _firstStopSlaMissLogged = false;
        }

        private void MaybeLogFirstStopSlaWorking(DateTime eventTime, string context)
        {
            if (_firstStopSlaLogged || entryFillTime == DateTime.MinValue)
                return;

            double elapsedMs = Math.Max(0.0, (eventTime - entryFillTime).TotalMilliseconds);
            string verdict = elapsedMs <= 250.0 ? "PASS" : (elapsedMs <= 1500.0 ? "WARN" : "MISS");
            WriteRiskLog(
                $"[FIRST_STOP_SLA] token=G8_FIRST_STOP_SLA [MODE=WORKING_LIKE][{verdict}] {elapsedMs:F0}ms ctx={context}");
            _firstStopSlaLogged = true;
            _firstStopSlaMissLogged = string.Equals(verdict, "MISS", StringComparison.Ordinal);
        }

        private void MaybeLogFirstStopSlaMiss(DateTime eventTime, string context)
        {
            if (_firstStopSlaLogged || _firstStopSlaMissLogged || entryFillTime == DateTime.MinValue)
                return;

            double elapsedMs = Math.Max(0.0, (eventTime - entryFillTime).TotalMilliseconds);
            if (elapsedMs < 1500.0)
                return;

            WriteRiskLog($"[FIRST_STOP_SLA][MISS] >{elapsedMs:F0}ms without Working ctx={context}");
            _firstStopSlaMissLogged = true;
        }

        private void WriteOmHealthSummary(string context)
        {
            WriteRiskEvent(
                "OM_HEALTH",
                $"ctx={context}",
                $"trade={currentTradeID}",
                $"protectiveSubmits={_protectiveSubmitRequestCount}",
                $"protectiveCancels={_protectiveCancelRequestCount}",
                $"entryCancels={_entryCancelRequestCount}",
                $"flattenRequests={_flattenRequestCount}",
                $"coverage={_protectiveCoverageDisposition}",
                $"recovery={_recoveryResolution}",
                $"preservedProtective={_preservedProtectiveOrderCount}");
        }

        private void RecordEntryBlock(string reason, string detail = "")
        {
            _lastBlockReason = reason ?? string.Empty;
            WriteEntryObservation("ENTRY_BLOCK", $"reason={_lastBlockReason} | {detail}".Trim());
            WriteDebugLog($"[ENTRY_BLOCK] reason={_lastBlockReason} | state={_setupState} | detail={detail}");
        }

        private void LogSetupStateTransition(SecondLegSetupState previousState, SecondLegSetupState nextState, string reason, string detail = "")
        {
            if (previousState == nextState)
                return;

            WriteEntryObservation(
                "ENTRY_STATE",
                $"from={previousState} to={nextState} reason={reason} | {detail}".Trim());
        }

        private string BuildEntryObservationContext(string detail)
        {
            bool hasBarContext = Bars != null && CurrentBar >= 0 && Bars.Count > 0;
            bool priorRthUnavailable = UsePriorDayHighLow && (double.IsNaN(_priorRthHigh) || double.IsNaN(_priorRthLow));
            string structurePart = string.IsNullOrEmpty(_lastStructureLabel)
                ? (priorRthUnavailable
                    ? "structure=clear pdhPdl=unavailable(no_prior_rth)"
                    : "structure=clear")
                : $"structure={_lastStructureLabel}@{_lastStructurePrice:F2} room={_lastStructureRoom:F2} required={_lastStructureRequiredRoom:F2}";

            string signalPart = _signalBarIndex >= 0
                ? $"signalBar={_signalBarIndex} signalHigh={_signalBarHigh:F2} signalLow={_signalBarLow:F2}"
                : "signalBar=none";

            string plannedEntryPart = HasPlannedEntry()
                ? $"planned={_plannedEntry.Bias} qty={_plannedEntry.Quantity} entry={_plannedEntry.EntryPrice:F2} stop={_plannedEntry.InitialStopPrice:F2} expiry={_plannedEntry.ExpiryBar}"
                : "planned=none";

            string detailPart = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}";
            return string.Join(" | ", new[]
            {
                $"bar={(hasBarContext ? ClosedBarIndex().ToString(CultureInfo.InvariantCulture) : "n/a")}",
                $"time={(hasBarContext ? ClosedBarTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "n/a")}",
                $"state={_setupState}",
                $"bias={_activeBias}",
                $"trend={_trendContextValid}",
                $"session={_sessionFilterValid}",
                $"atrRegime={_volatilityRegimeValid}",
                $"atr={_atrValue:F2}",
                $"atrRatio={_atrRegimeRatio:F2}",
                $"slopeAtrPct={_emaFastSlopeAtrPct:F3}",
                $"impulseRange={_impulse.Range:F2}",
                $"impulseStrongBars={_lastImpulseStrongBars}",
                $"retracement={_lastImpulseRetracement:F3}",
                $"leg2Momentum={_lastLeg2Momentum:F3}",
                $"impulseMomentum={_lastImpulseMomentum:F3}",
                structurePart,
                signalPart,
                plannedEntryPart
            }) + detailPart;
        }
    }
}
