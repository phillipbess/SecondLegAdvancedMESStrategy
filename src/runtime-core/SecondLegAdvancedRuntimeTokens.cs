internal static class RlLevelName
{
    public const string Support1 = "Support1";
    public const string Support2 = "Support2";
    public const string Support3 = "Support3";
    public const string PriorDayLow = "PriorDayLow";
}

namespace NinjaTrader.NinjaScript.Strategies
{
    internal static class CtxTokens
    {
        public const string Flatten = "FLATTEN";
        public const string Adopt = "ADOPT";
        public const string EntryFill = "ENTRYFILL";
        public const string EntryPartFill = "ENTRYPARTFILL";
        public const string Retry = "RETRY";
        public const string Reconnect = "RECONNECT";
        public const string SimplePrefix = "SIMPLE_";
        public const string Emergency = "EMERGENCY";
        public const string MaxRisk = "MAXRISK";
        public const string RiskGuard = "RISKGUARD";

        public const string StopLoss = "StopLoss";
        public const string TrailExit = "TrailExit";
        public const string Protective = "PROTECTIVE";
        public const string ExitController = "ExitController";
        public const string Exit = "Exit";
        public const string Close = "CLOSE";

        public const string SessionClose = "SessionClose";
        public const string Eod = "EOD";
        public const string Bulletproof = "BULLETPROOF";
        public const string Safety = "Safety";
        public const string Catastrophic = "Catastrophic";

        public const string StateRestoreRunner = "StateRestore_Runner";
        public const string StateRestoreFullPosition = "StateRestore_FullPosition";
        public const string RestoredStopLoss = "RestoredStopLoss";
        public const string RestoredProfitTarget = "RestoredProfitTarget";
        public const string RestoredRunnerStop = "RestoredRunnerStop";

        public const string SimpleTrailArming = "SIMPLE_Trail_Arming";
        public const string SimpleTrailUpdate = "SIMPLE_Trail_Update";
        public const string SimpleTrailExit = "SIMPLE_Trail_Exit";
    }

    internal static class NameTokens
    {
        public const string Flatten = "Flatten";
        public const string SafetyFlatten = "SafetyFlatten";
        public const string EmergencyFlatten = "EmergencyFlatten";

        public const string StopLossPrefix = "StopLoss_";
        public const string TrailExitPrefix = "TrailExit_";

        public const string StopLoss = "StopLoss";
        public const string TrailExit = "TrailExit";
        public const string ProfitTarget = "ProfitTarget";
        public const string Exit = "Exit";

        public const string Stop = "Stop";
        public const string Target = "Target";
        public const string Entry = "Entry";
        public const string RetrySuffix = "_Retry";
        public const string Runner = "Runner";
        public const string RunnerStop = "RunnerStop";

        public const string UPPER_TRAIL = "TRAIL";
        public const string UPPER_STOP = "STOP";
        public const string UPPER_FLATTEN = "FLATTEN";
        public const string UPPER_EMERGENCY = "EMERGENCY";
        public const string UPPER_PROFIT = "PROFIT";
        public const string UPPER_TARGET = "TARGET";
    }
}
