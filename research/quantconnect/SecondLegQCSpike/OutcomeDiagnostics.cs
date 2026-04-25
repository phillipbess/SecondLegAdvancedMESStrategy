using System;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private void RecordOutcomeBuckets(string outcome, double rMultiple)
        {
            string side = _virtualTrade.Bias == Bias.Long ? "L" : "S";
            RecordOutcomeBucket($"Side {side}", outcome, rMultiple);
            RecordOutcomeBucket($"H{_virtualTrade.SignalHour:00} {side}", outcome, rMultiple);
            RecordOutcomeBucket(RoomBucketName(_virtualTrade.RoomToStructureR), outcome, rMultiple);
        }

        private void RecordOutcomeBucket(string key, string outcome, double rMultiple)
        {
            if (!_outcomeBuckets.TryGetValue(key, out OutcomeBucket bucket))
            {
                bucket = new OutcomeBucket();
                _outcomeBuckets[key] = bucket;
            }

            bucket.Trades++;
            bucket.NetR += rMultiple;
            bucket.SumMfe += _virtualTrade.MaxFavorableR;
            bucket.SumMae += _virtualTrade.MaxAdverseR;
            if (_virtualTrade.TouchedProbe)
                bucket.TouchOneR++;
            if (outcome == "Target")
                bucket.TwoR++;
            else if (outcome == "Stop")
                bucket.Stops++;
            else
                bucket.Timeouts++;
        }

        private void PublishOutcomeBuckets()
        {
            foreach (var item in _outcomeBuckets.OrderBy(x => OutcomeBucketSortKey(x.Key)))
                SetRuntimeStatistic(item.Key, item.Value.ToRuntimeStat());
        }

        private static string RoomBucketName(double roomToStructureR)
        {
            if (roomToStructureR >= 100.0)
                return "Room Clear";
            if (roomToStructureR < 0.25)
                return "Room <0.25R";
            if (roomToStructureR < 0.50)
                return "Room <0.50R";
            if (roomToStructureR < 1.00)
                return "Room <1.00R";
            return "Room >=1R";
        }

        private static string OutcomeBucketSortKey(string key)
        {
            if (key.StartsWith("Side", StringComparison.Ordinal))
                return "0-" + key;
            if (key.StartsWith("H", StringComparison.Ordinal))
                return "1-" + key;
            return "2-" + key;
        }

        private sealed class OutcomeBucket
        {
            public int Trades;
            public int TouchOneR;
            public int TwoR;
            public int Stops;
            public int Timeouts;
            public double NetR;
            public double SumMfe;
            public double SumMae;

            public string ToRuntimeStat()
            {
                double avgR = Trades > 0 ? NetR / Trades : 0.0;
                double avgMfe = Trades > 0 ? SumMfe / Trades : 0.0;
                double avgMae = Trades > 0 ? SumMae / Trades : 0.0;
                return $"n={Trades} avg={avgR:0.00} net={NetR:0.0} 2R={TwoR} st={Stops} to={Timeouts} t1={TouchOneR} mfe={avgMfe:0.00} mae={avgMae:0.00}";
            }
        }
    }
}
