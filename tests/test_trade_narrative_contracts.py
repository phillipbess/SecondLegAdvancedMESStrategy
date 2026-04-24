import os
from pathlib import Path

from src.scripts import trade_narrative


def touch(path: Path, text: str, mtime: int) -> None:
    path.write_text(text, encoding="utf-8")
    os.utime(path, (mtime, mtime))


def test_find_log_files_uses_single_latest_session_when_date_omitted(tmp_path, monkeypatch) -> None:
    monkeypatch.setattr(trade_narrative, "LOG_DIR", tmp_path)

    touch(tmp_path / "Patterns_20260422_MES_Playback101.txt", "patterns old", 100)
    touch(tmp_path / "Trades_20260422_MES_Playback101.txt", "trades old", 100)
    touch(tmp_path / "Risk_20260422_MES_Playback101.txt", "risk old", 100)

    touch(tmp_path / "Patterns_20260423_MES_Playback101.txt", "patterns new", 200)
    touch(tmp_path / "Trades_20260423_MES_Playback101.txt", "trades new", 210)
    touch(tmp_path / "Risk_20260423_MES_Playback101.txt", "risk new", 220)
    touch(tmp_path / "TradesCsv_20260423_MES_Playback101.csv", "trade csv new", 221)
    touch(tmp_path / "StopEvents_20260423_MES_Playback101.csv", "stop events new", 222)

    files, selected = trade_narrative.find_log_files()

    assert selected == "20260423"
    assert files["Patterns"].name == "Patterns_20260423_MES_Playback101.txt"
    assert files["Trades"].name == "Trades_20260423_MES_Playback101.txt"
    assert files["Risk"].name == "Risk_20260423_MES_Playback101.txt"
    assert files["TradesCsv"].name == "TradesCsv_20260423_MES_Playback101.csv"
    assert files["StopEvents"].name == "StopEvents_20260423_MES_Playback101.csv"


def test_build_trade_narratives_recovers_setup_and_trade_story(tmp_path, monkeypatch) -> None:
    monkeypatch.setattr(trade_narrative, "LOG_DIR", tmp_path)

    patterns = """=== NEW RUN === PATTERN ===
2026-04-02 11:35:00.000 | [ENTRY_ARMED] bar=1037 | time=11:30:00 | state=WaitingForSignalBar | bias=Long | trend=True | session=True | atrRegime=True | atr=15.18 | atrRatio=1.70 | slopeAtrPct=0.146 | impulseRange=80.50 | impulseStrongBars=2 | retracement=0.519 | leg2Momentum=4.450 | impulseMomentum=26.833 | structure=SWING_H@6644.50 room=22.00 required=20.25 | signalBar=1037 signalHigh=6622.25 signalLow=6604.75 | planned=Long qty=1 entry=6622.50 stop=6602.25 expiry=1040 | signal=PE2L_1037 bias=Long entry=6622.50 stop=6602.25 qty=1 expiry=1040
"""
    trades = """=== NEW RUN === TRADE ===
2026-04-02 11:35:00.000 | [ENTRY_SUBMIT] signal=PE2L_1037 trade=PE2L_1037 bias=Long qty=1 entry=6622.50 stop=6602.25 reason=SecondLegLong
2026-04-02 11:40:00.000 | [ENTRY_FILL] signal=PE2L_1037 trade=PE2L_1037 role=entry order=PE2L_1037 orderId=abc state=Filled execId=fill1 qty=1 fillPrice=6622.50 avgEntry=6622.50 posQty=1 sessionControlFlatten=False
2026-04-02 11:55:00.000 | [EXIT_FILL] signal=PE2L_1037 trade=PE2L_1037 role=protective order=StopLoss_PrimaryEntry|PE2L_1037 orderId=def state=Filled execId=fill2 qty=1 fillPrice=6602.25 marketPosition=Short
2026-04-02 11:55:00.000 | [TRADE_CLOSE] signal=PE2L_1037 trade=PE2L_1037 role=protective order=StopLoss_PrimaryEntry|PE2L_1037 orderId=def state=Filled execId=fill2 qty=1 exitPrice=6602.25 marketPosition=Short pnlCurrency=-101.25 pnlR=-1.00
"""
    risk = """=== NEW RUN === RISK ===
2026-04-02 11:40:00.000 | [FIRST_STOP_SLA] token=G8_FIRST_STOP_SLA [MODE=WORKING_LIKE][PASS] 0ms ctx=OnOrderUpdate.ProtectiveWorking trade=PE2L_1037
2026-04-02 11:40:00.000 | [STOP_SUBMIT] ctx=ExitController.EnsureProtectiveExit mode=initial qty=1 stop=6602.25 oco=SLA_OCO_0002 trade=PE2L_1037
"""
    debug = """=== NEW RUN === DEBUG ===
2026-04-02 11:35:00.000 | TRADE ID ANCHOR | ActiveTradeId restored from TradeID | reason=SubmitPlannedEntry tradeId=PE2L_1037
"""

    touch(tmp_path / "Patterns_20260423_MES_Playback101.txt", patterns, 100)
    touch(tmp_path / "Trades_20260423_MES_Playback101.txt", trades, 101)
    touch(tmp_path / "Risk_20260423_MES_Playback101.txt", risk, 102)
    touch(tmp_path / "Debug_20260423_MES_Playback101.txt", debug, 103)

    files, _ = trade_narrative.find_log_files("20260423")
    narratives = trade_narrative.build_trade_narratives(files)

    assert len(narratives) == 1
    trade = narratives[0]
    assert trade.trade_id == "PE2L_1037"
    assert trade.setup is not None
    assert trade.setup.signal_bar == "1037"
    assert trade.entry_price == 6622.50
    assert trade.exit_price == 6602.25
    assert trade.first_stop_sla_ms == 0
