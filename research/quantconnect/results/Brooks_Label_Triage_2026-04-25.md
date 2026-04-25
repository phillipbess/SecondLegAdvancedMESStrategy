# Brooks Label Triage - 2026-04-25

Input: `research/quantconnect/labeling/brooks_label_review/brooks_label_sheet.csv`

Source: public Yahoo `ES=F` 5-minute proxy, last `59` days available at generation time.

This is not final strategy validation. It is a fast triage layer before manual chart labeling.

## Forward Outcome Proxy

For each first-hour context:

- Entry proxy: the 10:30 ET measurement close.
- Direction: same direction as the first-hour move.
- Stop proxy: opposite first-hour extreme.
- Target proxy: `1R` from entry.
- Ambiguity rule: if target and stop are both touched inside the same 5-minute bar, count it as stopped.

## Result

- Contexts: `41`
- Hit 1R before stop: `16/41` (`39.0%`)
- Positive EOD continuation: `19/41` (`46.3%`)
- Average continuation MFE: `0.67R`
- Average EOD continuation: `-4.98 ES points`

Outcome buckets:

- `hit-1r`: `16`
- `stopped`: `13`
- `failed-eod`: `6`
- `positive-eod`: `6`

## Initial Read

The broad first-hour continuation idea still does not look strong enough by itself.

What this says:

- Some first-hour contexts have tradable follow-through, but not enough to blindly buy/sell the 10:30 continuation.
- A clean 1R continuation appears in less than half the recent sample.
- The negative average EOD continuation warns that the first-hour direction often does not own the whole day.
- If there is an edge, it is probably not "first hour strong, enter same direction." It is narrower: the pullback quality, signal-bar quality, trap location, and whether the first hour is trend or trading range must matter.

## Best Next Filter Ideas

These should be reviewed visually before coding:

- Continuation only after a shallow pullback that does not cross the first-hour midpoint.
- Skip mixed first hours where both strong bull and strong bear bars appear.
- Require the post-10:30 entry to occur after a two-legged pullback, not at the 10:30 close.
- Separate trend-from-open days from reversal/trading-range days; do not combine them.

## Next Research Step

Manual-label the chart deck, but prioritize the highest-information groups:

- Review all `hit-1r` charts and mark which ones were actually tradable.
- Review all `stopped` charts and mark what made them bad.
- Compare visual traits between those two groups.

Only after that should we code another Brooks-derived test. The next coded version should use a real post-measurement pullback trigger, not a blind first-hour continuation entry.
