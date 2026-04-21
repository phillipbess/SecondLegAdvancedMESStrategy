using System;
using NinjaTrader.Cbi;

internal static class Nt8SignalName
{
    public const int MaxLen = 50;

    public static string Entry(string seed, string flavor = "PE")
    {
        string normalizedSeed = SafeSeed(seed);
        return Clip($"{flavor}_{normalizedSeed}");
    }

    public static string ExitFromEntry(string entryTag, string role = "SL")
    {
        string tag = string.IsNullOrEmpty(entryTag) ? "ENTRY" : entryTag.Replace(" ", "");
        if (tag.StartsWith("PE_"))
            tag = tag.Substring(3);
        if (tag.Length > 12)
            tag = tag.Substring(0, 12);
        return Clip($"{role}_{tag}");
    }

    public static string Clip(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return "TAG";
        return tag.Length > MaxLen ? tag.Substring(0, MaxLen) : tag;
    }

    private static string SafeSeed(string seed)
    {
        string value = string.IsNullOrEmpty(seed) ? Guid.NewGuid().ToString("N") : seed;
        string compact = value.Replace("-", "").Replace(":", "").Replace("{", "").Replace("}", "");
        return compact.Length > 8 ? compact.Substring(0, 8) : compact;
    }
}

internal enum ExitFlowState
{
    Flat = 0,
    Live = 1,
    Flattening = 2,
    PostSweep = 3,
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public enum OrderRole
    {
        Unknown = 0,
        Entry = 1,
        StopLoss = 2,
        ProfitTarget = 3,
        Flatten = 4,
    }

    public static class OrderStateExtensions
    {
        public static bool IsWorkingLike(OrderState state)
        {
            return state == OrderState.Submitted
                || state == OrderState.Accepted
                || state == OrderState.Working
                || state == OrderState.PartFilled
                || state == OrderState.ChangeSubmitted
                || state == OrderState.ChangePending
                || state == OrderState.CancelPending;
        }

        public static bool IsActiveLike(OrderState state)
        {
            return state == OrderState.Submitted
                || state == OrderState.Accepted
                || state == OrderState.Working;
        }
    }

    public static class OrderExtensions
    {
        public static bool IsFlattenLike(this Order order)
        {
            if (order == null || string.IsNullOrEmpty(order.Name))
                return false;

            return order.Name.IndexOf(NameTokens.SafetyFlatten, StringComparison.OrdinalIgnoreCase) >= 0
                || order.Name.IndexOf(NameTokens.EmergencyFlatten, StringComparison.OrdinalIgnoreCase) >= 0
                || order.Name.IndexOf(NameTokens.Flatten, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsProtectiveStop(this Order order)
        {
            if (order == null || string.IsNullOrEmpty(order.Name))
                return false;

            return order.Name.StartsWith(NameTokens.StopLossPrefix, StringComparison.Ordinal)
                || order.Name.StartsWith(NameTokens.TrailExitPrefix, StringComparison.Ordinal);
        }
    }
}
