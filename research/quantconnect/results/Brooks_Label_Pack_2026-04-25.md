# Brooks Label Pack - 2026-04-25

Goal: after the Brooks-style deterministic tests came in far below the `6R-8R/month` target, build a visual review pack so we can identify what the human eye sees that the simple rules are missing.

## What Changed

- Added a QuantConnect `BrooksLabelPack` mode that exports labeled first-hour and TFO-pullback windows.
- Added `research/quantconnect/scripts/build_brooks_label_review.py` as a local fallback label-pack builder.
- Generated a review deck from public `ES=F` 5-minute Yahoo data.

## QuantConnect Run

The cloud project compiled and ran successfully.

Best run:

- Backtest: `Brooks label pack 100 context`
- Backtest id: `6bcd9219fdf9738ca49ef1409083f543`
- Export key: `30609547/brooks_label_pack_20210424_20260423_bar_5_setups_100_measure_60_move_0p75_strong_3_ctx_1_0p25.csv`
- Runtime stats: `LabelContext:13`, `LabelNoTfo:4`

Important limitation: the current QuantConnect account can save ObjectStore exports, but ObjectStore download is blocked by the account's data licensing tier. Because of that, QC can validate compile/run behavior, but it cannot currently be our practical chart-label export path.

## Local Review Pack

Generated command:

```powershell
python research\quantconnect\scripts\build_brooks_label_review.py --symbol "ES=F" --days 59 --max-setups 100 --out-dir research\quantconnect\labeling\brooks_label_review
```

Outputs:

- `research/quantconnect/labeling/brooks_label_review/brooks_label_review.html`
- `research/quantconnect/labeling/brooks_label_review/brooks_label_sheet.csv`
- `research/quantconnect/labeling/brooks_label_review/README.md`

Sample size:

- Fetched bars: `3239`
- Setups: `41`
- Source: public Yahoo `ES=F` 5-minute proxy

## Research Read

This does not rescue the strategy yet. It gives us a better next step.

The deterministic Brooks tests showed that plain first-hour trend continuation and simple opening reversal rules are too weak. The label pack lets us inspect real chart windows and mark:

- which first-hour drives are actually clean,
- which are noisy auctions that should be skipped,
- which pullbacks are shallow/constructive versus deep/overlapping,
- which continuation attempts have signal-bar quality,
- which examples are traps or failed breakouts.

If the best manually labeled charts share repeatable traits, we can encode those traits and test again. If the high-quality labels still do not produce strong forward excursion, we should kill the Brooks path cleanly.

## Next Step

Review the HTML chart deck and fill `brooks_label_sheet.csv`.

Minimum useful labeling target:

- `30+` charts scored.
- Mark `brooksQuality` from `0` to `3`.
- Mark `label` as `trade`, `skip`, `fade`, or `study`.
- Add notes for any recurring trait we are not currently measuring.

After labeling, run a trait analysis to compare high-quality labels against low-quality labels and turn the differences into one or two new testable filters.
