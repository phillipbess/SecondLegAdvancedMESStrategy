using System;
using System.Globalization;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private DateTime DateParameter(string key, DateTime fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            return DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value)
                ? value
                : fallback;
        }

        private double DoubleParameter(string key, double fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }

        private int IntParameter(string key, int fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        private string TextParameter(string key, string fallback)
        {
            string raw = GetParameter(key);
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        }

        private bool BoolParameter(string key, bool fallback)
        {
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            if (bool.TryParse(raw, out bool value))
                return value;
            if (raw == "1")
                return true;
            if (raw == "0")
                return false;
            return fallback;
        }

        private bool IsLiteMode()
        {
            return EntryMode == "videosecondentrylite" || EntryMode == "lite";
        }

        private string EntryModeToken()
        {
            if (IsIctSequencedMode())
                return "ICTSeq";
            if (IsIctMode())
                return "ICT";
            if (IsCamarillaMode())
                return "Camarilla";
            if (IsCandidateStackMode())
                return "CandidateStack";
            if (IsOpeningAuctionMode())
                return "OpenAuction";
            if (IsLiquiditySweepMode())
                return "LiquiditySweep";
            if (IsOpeningDriveMode())
                return "OpeningDrive";
            return IsLiteMode() ? "VideoSecondEntryLite" : "StrictV1";
        }

        private double EffectiveMinImpulseAtrMultiple()
        {
            return IsLiteMode() ? LiteMinImpulseAtrMultiple : MinImpulseAtrMultiple;
        }

        private double EffectiveMaxPullbackRetracement()
        {
            return IsLiteMode() ? LiteMaxPullbackRetracement : MaxPullbackRetracement;
        }

        private bool SideAllowed(Bias bias)
        {
            if (bias == Bias.Neutral)
                return true;
            if (SideFilter == "long")
                return bias == Bias.Long;
            if (SideFilter == "short")
                return bias == Bias.Short;
            return true;
        }
    }
}
