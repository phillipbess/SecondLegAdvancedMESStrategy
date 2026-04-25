# Brooks Label Review Pack

Generated from `ES=F` Yahoo 5-minute bars over the last `59` days.

This pack is for manual idea extraction, not final performance validation. The goal is to identify which first-hour auction contexts actually look like Brooks-quality trend-from-open or reversal/trap opportunities before we keep coding rules.

## Contents

- `brooks_label_review.html`: visual chart review sheet.
- `brooks_label_sheet.csv`: manifest with blank scoring columns.

## Sample

- Setups: `41`
- Long contexts: `24`
- Short contexts: `17`
- Average absolute first-hour move: `1.56 ATR`
- Hit 1R before opposite first-hour stop: `16/41`
- Positive EOD continuation: `19/41`
- Average continuation MFE: `0.67R`

## Auto-Triage Columns

- `entryPrice`: first-hour measurement close.
- `stopPrice`: opposite first-hour extreme.
- `riskPts`: distance from entry to stop.
- `continuationMfeR`: best same-direction excursion after measurement.
- `continuationMaeR`: worst adverse excursion after measurement.
- `hit1RBeforeStop`: conservative 5-minute bar check; same-bar target/stop ambiguity is counted as stopped.
- `continuationGrade`: quick bucket for sorting before visual labeling.

## Labeling Rubric

- `brooksQuality`: 0 means no trade, 1 means maybe, 2 means clean, 3 means textbook.
- `trendContext`: describe strong trend, trading range, failed breakout, reversal, or unclear.
- `pullbackQuality`: shallow, deep, two-legged, overlapping, climactic, or none.
- `signalBarQuality`: strong, weak, trap-like, bad location, or no signal.
- `label`: trade, skip, fade, or study.
