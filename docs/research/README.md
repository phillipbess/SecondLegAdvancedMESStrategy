# Research

Use this folder for working notes, hypothesis tests, labeled examples, and early-stage
analysis that informs the strategy thesis.

Keep the contents lightweight and reviewable:

- date notes and experiments
- distinguish raw research from accepted decisions
- promote durable conclusions into ADRs or formal docs

## QuantConnect Reading Order

For the 2026-04-24 backtesting push, read the notes in this order:

1. [quantconnect-feasibility-spike-2026-04-24.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/research/quantconnect-feasibility-spike-2026-04-24.md)
2. [quantconnect-5y-sweep-2026-04-24.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/research/quantconnect-5y-sweep-2026-04-24.md)
3. [quantconnect-side-split-2026-04-24.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/research/quantconnect-side-split-2026-04-24.md)
4. [quantconnect-short-refinement-2026-04-24.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/research/quantconnect-short-refinement-2026-04-24.md)
5. [quantconnect-frequency-probe-2026-04-24.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/research/quantconnect-frequency-probe-2026-04-24.md)

Short version:

- strict 5-minute is clean but sparse
- short-only strict refinement is the best quality pocket so far
- loosening strict 5-minute increases trades but does not reach the desired monthly
  frequency and remains negative
- `VideoSecondEntryLite` exists to test the video idea separately instead of mutating
  the strict contract
