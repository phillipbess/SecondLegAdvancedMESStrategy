using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private HashSet<int> HourSetParameter(string key)
        {
            var result = new HashSet<int>();
            string raw = GetParameter(key);
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (string token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = token.Trim();
                if (trimmed.Contains("-"))
                {
                    string[] parts = trimmed.Split('-');
                    if (parts.Length == 2
                        && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)
                        && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int end))
                    {
                        int min = Math.Max(0, Math.Min(start, end));
                        int max = Math.Min(23, Math.Max(start, end));
                        for (int hour = min; hour <= max; hour++)
                            result.Add(hour);
                    }
                    continue;
                }

                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && value >= 0
                    && value <= 23)
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private bool PassesLiteTimeFilter(BarSnapshot bar)
        {
            if (!IsLiteMode())
                return true;

            int hour = bar.EndTime.Hour;
            if (!PassesHourSet(hour, LiteAllowedHours, LiteBlockedHours))
                return false;
            if (_impulse.Bias == Bias.Long)
                return PassesHourSet(hour, LiteAllowedLongHours, LiteBlockedLongHours);
            if (_impulse.Bias == Bias.Short)
                return PassesHourSet(hour, LiteAllowedShortHours, LiteBlockedShortHours);
            return true;
        }

        private static bool PassesHourSet(int hour, HashSet<int> allowed, HashSet<int> blocked)
        {
            if (allowed != null && allowed.Count > 0 && !allowed.Contains(hour))
                return false;
            return blocked == null || !blocked.Contains(hour);
        }

        private bool PassesLitePullbackShape(double retracement, BarSnapshot bar)
        {
            if (!IsLiteMode() || LiteMinPullbackAtr <= 0.0)
                return true;

            double pullbackMove = retracement * Math.Max(_impulse.Range, TickSize);
            return pullbackMove >= LiteMinPullbackAtr * Math.Max(bar.Atr, TickSize);
        }

        private bool PassesLiteLeg2Shape(double leg2Move, BarSnapshot bar)
        {
            if (!IsLiteMode() || LiteMinLeg2Atr <= 0.0)
                return true;

            return leg2Move >= LiteMinLeg2Atr * Math.Max(bar.Atr, TickSize);
        }

        private bool PassesLiteEntryRoomFilter(double roomToStructureR)
        {
            if (!IsLiteMode())
                return true;
            if (LiteEntryRoomMinR >= 0.0 && roomToStructureR < LiteEntryRoomMinR)
                return false;
            if (LiteEntryRoomMaxR >= 0.0 && roomToStructureR > LiteEntryRoomMaxR)
                return false;
            return true;
        }

        private int MinutesFromOpen(BarSnapshot bar)
        {
            return (int)(bar.EndTime.TimeOfDay - new TimeSpan(9, 30, 0)).TotalMinutes;
        }

        private static double CloseLocationPct(BarSnapshot bar)
        {
            double range = bar.High - bar.Low;
            if (range <= 0.0)
                return 0.5;
            return Clamp((bar.Close - bar.Low) / range, 0.0, 1.0);
        }

        private static double BodyPct(BarSnapshot bar)
        {
            double range = bar.High - bar.Low;
            if (range <= 0.0)
                return 0.0;
            return Math.Min(1.0, Math.Abs(bar.Close - bar.Open) / range);
        }

        private static double AtrDistance(double price, double reference, double atr)
        {
            return (price - reference) / Math.Max(atr, TickSize);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static string HourToken(HashSet<int> hours)
        {
            return hours == null || hours.Count == 0
                ? "none"
                : string.Join("|", hours.OrderBy(x => x).Select(x => x.ToString(CultureInfo.InvariantCulture)));
        }

        private static string HourKeyToken(HashSet<int> hours)
        {
            return hours == null || hours.Count == 0
                ? "none"
                : string.Join("-", hours.OrderBy(x => x).Select(x => x.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
