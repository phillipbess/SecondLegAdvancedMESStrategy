from __future__ import annotations

import math


def evaluate_trend_context(case: dict) -> dict:
    close = case["close"]
    ema_slow = case["ema_slow"]
    slope = case["ema_fast_slope_atr_pct"]
    slope_min = case.get("slope_min_atr_pct_per_bar", 0.03)

    long_valid = close > ema_slow and slope >= slope_min
    short_valid = close < ema_slow and slope <= -slope_min

    trend_context_valid = long_valid or short_valid
    if long_valid and not short_valid:
        active_bias = "Long"
    elif short_valid and not long_valid:
        active_bias = "Short"
    else:
        active_bias = "Neutral"

    return {
        "active_bias": active_bias,
        "trend_context_valid": trend_context_valid,
        "long_votes": 0,
        "short_votes": 0,
    }


def evaluate_session_and_regime(case: dict) -> dict:
    session_valid = True

    flatten_window_active = case.get("flatten_window_active")
    if flatten_window_active is None:
        flatten_window_active = bool(
            case.get("flatten_before_close", False)
            and case.get("hhmm", 0) >= case.get("flatten_time_hhmm", 2359)
        )

    atr_value = case.get("atr_value", 0.0)
    atr_regime_ratio = case.get("atr_regime_ratio", 0.0)
    regime_valid = (
        atr_value > 0.0
        and atr_regime_ratio >= case["min_atr_regime_ratio"]
        and atr_regime_ratio <= case["max_atr_regime_ratio"]
    )

    max_trades_per_session = case.get("max_trades_per_session")
    trades_this_session = case.get("trades_this_session", 0)
    max_consecutive_losses = case.get("max_consecutive_losses")
    consecutive_losses = case.get("consecutive_losses", 0)
    daily_loss_limit_r = case.get("daily_loss_limit_r")
    session_realized_r = case.get("session_realized_r", 0.0)
    cooldown_until_bar = case.get("cooldown_until_bar", -1)
    current_bar = case.get("current_bar", 0)

    hard_risk_valid = True
    hard_risk_reason = ""
    if max_trades_per_session is not None and trades_this_session >= max_trades_per_session:
        hard_risk_valid = False
        hard_risk_reason = "TradeLimitHit"
    elif max_consecutive_losses is not None and consecutive_losses >= max_consecutive_losses:
        hard_risk_valid = False
        hard_risk_reason = "LossStreakHit"
    elif daily_loss_limit_r is not None and session_realized_r <= daily_loss_limit_r:
        hard_risk_valid = False
        hard_risk_reason = "DailyLossLimit"
    elif cooldown_until_bar >= 0 and current_bar < cooldown_until_bar:
        hard_risk_valid = False
        hard_risk_reason = "LossCooldown"

    return {
        "session_valid": session_valid,
        "regime_valid": regime_valid,
        "flatten_window_active": flatten_window_active,
        "hard_risk_valid": hard_risk_valid,
        "hard_risk_reason": hard_risk_reason,
        "participate": session_valid and regime_valid and not flatten_window_active and hard_risk_valid,
    }


def evaluate_entry_qualification(case: dict) -> dict:
    tick_size = case.get("tick_size", 0.25)
    entry_offset_ticks = case.get("entry_offset_ticks", 1)
    stop_buffer_ticks = case.get("stop_buffer_ticks", 2)
    point_value = case.get("point_value", 5.0)
    risk_per_trade = case.get("risk_per_trade", 200.0)
    atr_value = case.get("atr_value", 0.0)
    max_stop_atr_multiple = case.get("max_stop_atr_multiple", 1.50)
    min_room_r = case.get("min_room_to_structure_r", 1.0)
    bias = case["bias"]

    if bias == "Short":
        entry_price = case["signal_bar_low"] - (entry_offset_ticks * tick_size)
        stop_price = case["pullback_leg2_high"] + (stop_buffer_ticks * tick_size)
    else:
        entry_price = case["signal_bar_high"] + (entry_offset_ticks * tick_size)
        stop_price = case["pullback_leg2_low"] - (stop_buffer_ticks * tick_size)

    stop_distance = abs(entry_price - stop_price)
    risk_per_contract = stop_distance * point_value
    if risk_per_contract <= 0.0:
        return {"accepted": False, "block_reason": "RiskTooSmall"}

    if atr_value > 0.0 and stop_distance / atr_value > max_stop_atr_multiple:
        return {"accepted": False, "block_reason": "StopTooWide", "entry_price": entry_price, "stop_price": stop_price}

    quantity = math.floor(risk_per_trade / risk_per_contract)
    if quantity <= 0:
        return {"accepted": False, "block_reason": "RiskTooSmall", "entry_price": entry_price, "stop_price": stop_price}

    structure_levels = case.get("structure_levels", [])
    structure_level_records = case.get("structure_level_records", [])
    if case.get("structure_filter_enabled", True) and (structure_levels or structure_level_records):
        required_room = stop_distance * min_room_r
        if structure_level_records:
            if bias == "Short":
                candidates = [
                    level["price"]
                    for level in structure_level_records
                    if level["kind"] in {"PriorDayLow", "OpeningRangeLow", "SwingLow"}
                    and level["price"] < entry_price
                ]
            else:
                candidates = [
                    level["price"]
                    for level in structure_level_records
                    if level["kind"] in {"PriorDayHigh", "OpeningRangeHigh", "SwingHigh"}
                    and level["price"] > entry_price
                ]
        else:
            candidates = [level for level in structure_levels if level < entry_price] if bias == "Short" else [level for level in structure_levels if level > entry_price]

        if bias == "Short":
            if candidates:
                room = entry_price - max(candidates)
                if room < required_room:
                    return {
                        "accepted": False,
                        "block_reason": "StructureRoom",
                        "entry_price": entry_price,
                        "stop_price": stop_price,
                        "quantity": quantity,
                    }
        else:
            if candidates:
                room = min(candidates) - entry_price
                if room < required_room:
                    return {
                        "accepted": False,
                        "block_reason": "StructureRoom",
                        "entry_price": entry_price,
                        "stop_price": stop_price,
                        "quantity": quantity,
                    }

    return {
        "accepted": True,
        "block_reason": "",
        "entry_price": entry_price,
        "stop_price": stop_price,
        "quantity": quantity,
    }


def evaluate_impulse_qualification(case: dict) -> dict:
    bias = case["bias"]
    bars = case["bars"]
    atr_value = case["atr_value"]
    ema_fast = case["ema_fast"]
    min_impulse_atr_multiple = case.get("min_impulse_atr_multiple", 1.25)
    strong_body_pct = case.get("strong_body_pct", 0.50)
    min_strong_bars = case.get("min_strong_bars", 2)
    tick_size = case.get("tick_size", 0.25)

    impulse_high = max(bar["h"] for bar in bars)
    impulse_low = min(bar["l"] for bar in bars)
    impulse_move = impulse_high - impulse_low

    if impulse_move < (min_impulse_atr_multiple * atr_value):
        return {
            "qualified": False,
            "block_reason": "ImpulseMoveTooSmall",
            "impulse_move": impulse_move,
            "strong_bars": 0,
        }

    strong_bars = 0
    for bar in bars:
        bar_range = max(bar["h"] - bar["l"], tick_size)
        body_pct = abs(bar["c"] - bar["o"]) / bar_range
        directional_bar = bar["c"] > bar["o"] if bias == "Long" else bar["c"] < bar["o"]
        if directional_bar and body_pct >= strong_body_pct:
            strong_bars += 1

    if strong_bars < min_strong_bars:
        return {
            "qualified": False,
            "block_reason": "NotEnoughStrongBars",
            "impulse_move": impulse_move,
            "strong_bars": strong_bars,
        }

    final_bar = bars[-1]
    final_direction_ok = final_bar["c"] > final_bar["o"] if bias == "Long" else final_bar["c"] < final_bar["o"]
    if not final_direction_ok:
        return {
            "qualified": False,
            "block_reason": "FinalBarWrongDirection",
            "impulse_move": impulse_move,
            "strong_bars": strong_bars,
        }

    final_ema_side_ok = final_bar["c"] > ema_fast if bias == "Long" else final_bar["c"] < ema_fast
    if not final_ema_side_ok:
        return {
            "qualified": False,
            "block_reason": "FinalBarWrongSideOfEma50",
            "impulse_move": impulse_move,
            "strong_bars": strong_bars,
        }

    return {
        "qualified": True,
        "block_reason": "",
        "impulse_move": impulse_move,
        "strong_bars": strong_bars,
    }


def evaluate_golden_case(case: dict) -> dict:
    session_result = evaluate_session_and_regime(case["session_regime"])
    if session_result["flatten_window_active"]:
        return {
            "accepted": False,
            "armed": False,
            "stage": "session_regime",
            "block_reason": "FlattenWindow",
        }
    if not session_result.get("hard_risk_valid", True):
        return {
            "accepted": False,
            "armed": False,
            "stage": "session_regime",
            "block_reason": session_result.get("hard_risk_reason", "HardRiskBlocked"),
        }

    trend_result = evaluate_trend_context(case["trend_context"])
    if not trend_result["trend_context_valid"] or trend_result["active_bias"] == "Neutral":
        return {
            "accepted": False,
            "armed": False,
            "stage": "trend_context",
            "block_reason": "TrendInvalid",
        }

    if not session_result["regime_valid"]:
        return {
            "accepted": False,
            "armed": False,
            "stage": "session_regime",
            "block_reason": "AtrRegimeInvalid",
        }

    impulse_result = evaluate_impulse_qualification(case["impulse"])
    if not impulse_result["qualified"]:
        return {
            "accepted": False,
            "armed": False,
            "stage": "impulse",
            "block_reason": impulse_result["block_reason"] or "ImpulseInvalid",
        }

    pullback_result = evaluate_pullback_state_machine(case["pullback"])
    if pullback_result["final_state"] != "WaitingForTrigger":
        return {
            "accepted": False,
            "armed": False,
            "stage": "pullback",
            "block_reason": pullback_result["block_reason"] or "SignalInvalid",
        }

    entry_result = evaluate_entry_qualification(case["entry"])
    if not entry_result["accepted"]:
        return {
            "accepted": False,
            "armed": False,
            "stage": "entry",
            "block_reason": entry_result["block_reason"],
        }

    trigger_case = case.get("trigger", {})
    if trigger_case.get("bars_waited", 0) > trigger_case.get("max_trigger_bars", case["entry"].get("max_trigger_bars", 3)):
        return {
            "accepted": False,
            "armed": False,
            "stage": "trigger",
            "block_reason": "EntryExpired",
        }

    return {
        "accepted": True,
        "armed": True,
        "stage": "armed",
        "block_reason": "",
    }


def evaluate_armed_entry_lifecycle(case: dict) -> dict:
    tick_size = case.get("tick_size", 0.25)
    trail_enabled = case.get("trail_enabled", True)
    trail_trigger_points = case.get("trail_trigger_points", 15.0)
    trail_lock_points = case.get("trail_lock_points", 8.0)
    trail_distance_points = case.get("trail_distance_points", 10.0)
    entry_price = case["entry_price"]
    initial_stop_price = case["initial_stop_price"]
    bias = case["bias"]
    max_trigger_bars = case.get("max_trigger_bars", 3)

    def round_to_tick(value: float) -> float:
        return round(value / tick_size) * tick_size

    for index, bar in enumerate(case.get("pre_fill_bars", []), start=1):
        if bar.get("flatten_window_active", False):
            return {
                "final_state": "Reset",
                "block_reason": "FlattenWindow",
                "trail_armed": False,
                "final_stop": initial_stop_price,
                "target_used": False,
            }
        if bar.get("pullback_too_long", False):
            return {
                "final_state": "Reset",
                "block_reason": "PullbackTooLong",
                "trail_armed": False,
                "final_stop": initial_stop_price,
                "target_used": False,
            }
        if bar.get("pullback_too_deep", False):
            return {
                "final_state": "Reset",
                "block_reason": "PullbackTooDeep",
                "trail_armed": False,
                "final_stop": initial_stop_price,
                "target_used": False,
            }
        if bar.get("triggered", False):
            break
        if index > max_trigger_bars:
            return {
                "final_state": "Reset",
                "block_reason": "EntryExpired",
                "trail_armed": False,
                "final_stop": initial_stop_price,
                "target_used": False,
            }

    fill_event = case.get("fill_event")
    if not fill_event or not fill_event.get("filled", False):
        return {
            "final_state": "WaitingForTrigger",
            "block_reason": "",
            "trail_armed": False,
            "final_stop": initial_stop_price,
            "target_used": False,
        }

    updated_stop = initial_stop_price
    best_favorable_price = fill_event.get("fill_price", entry_price)
    trail_armed = False

    for price_point in case.get("post_fill_prices", []):
        if bias == "Long":
            favorable_price = price_point
            best_favorable_price = max(best_favorable_price, favorable_price)

            if trail_enabled:
                trigger_price = entry_price + trail_trigger_points
                if not trail_armed and best_favorable_price >= trigger_price:
                    trail_armed = True
                    lock_stop = round_to_tick(entry_price + trail_lock_points)
                    updated_stop = max(updated_stop, lock_stop)

                if trail_armed:
                    trail_stop = round_to_tick(best_favorable_price - trail_distance_points)
                    lock_stop = round_to_tick(entry_price + trail_lock_points)
                    updated_stop = max(updated_stop, max(lock_stop, trail_stop))
        else:
            favorable_price = price_point
            best_favorable_price = min(best_favorable_price, favorable_price)

            if trail_enabled:
                trigger_price = entry_price - trail_trigger_points
                if not trail_armed and best_favorable_price <= trigger_price:
                    trail_armed = True
                    lock_stop = round_to_tick(entry_price - trail_lock_points)
                    updated_stop = min(updated_stop, lock_stop)

                if trail_armed:
                    trail_stop = round_to_tick(best_favorable_price + trail_distance_points)
                    lock_stop = round_to_tick(entry_price - trail_lock_points)
                    updated_stop = min(updated_stop, min(lock_stop, trail_stop))

    return {
        "final_state": "ManagingTrade",
        "block_reason": "",
        "trail_armed": trail_armed,
        "final_stop": updated_stop,
        "target_used": False,
    }


def evaluate_pullback_state_machine(case: dict) -> dict:
    bars = case["bars"]
    bias = case["bias"]
    impulse_high = case["impulse_high"]
    impulse_low = case["impulse_low"]
    impulse_bars = case.get("impulse_bars", 3)
    min_pullback_bars = case.get("min_pullback_bars", 3)
    max_pullback_bars = case.get("max_pullback_bars", 12)
    min_pullback_retracement = case.get("min_pullback_retracement", 0.236)
    max_pullback_retracement = case.get("max_pullback_retracement", 0.618)
    second_leg_max_momentum_ratio = case.get("second_leg_max_momentum_ratio", 0.80)

    impulse_move = max(impulse_high - impulse_low, case.get("tick_size", 0.25))
    impulse_momentum = impulse_move / max(impulse_bars, 1)

    state = "TrackingPullbackLeg1"
    block_reason = ""
    pullback_leg1 = None
    pullback_leg2 = None
    pullback_bars = 0
    separation_high = None
    separation_low = None
    state_path = [state]

    def retracement(extreme: float) -> float:
        if bias == "Short":
            return (extreme - impulse_low) / impulse_move
        return (impulse_high - extreme) / impulse_move

    def fail(reason: str) -> dict:
        final_state_path = list(state_path)
        if not final_state_path or final_state_path[-1] != "Reset":
            final_state_path.append("Reset")
        return {
            "final_state": "Reset",
            "block_reason": reason,
            "signal_valid": False,
            "state_path": final_state_path,
        }

    def advance_state(next_state: str) -> None:
        nonlocal state
        if state != next_state:
            state = next_state
            state_path.append(next_state)

    for index, bar in enumerate(bars):
        previous = bars[index - 1] if index > 0 else None
        trend_valid = bar.get("trend_valid", True)
        corrective_ema200_valid = bar.get("corrective_ema200_valid", True)
        if state not in {"TrackingPullbackLeg2", "WaitingForSignalBar"} and not trend_valid:
            return fail("TrendInvalid")
        if not bar.get("atr_regime_valid", True):
            return fail("AtrRegimeInvalid")

        if state == "TrackingPullbackLeg1":
            if pullback_leg1 is None:
                if previous is None:
                    starting_bar = False
                elif bias == "Long":
                    starting_bar = bar["c"] < previous["c"] or bar["l"] < previous["l"]
                else:
                    starting_bar = bar["c"] > previous["c"] or bar["h"] > previous["h"]
                if not starting_bar:
                    continue
                pullback_leg1 = {
                    "start": index,
                    "end": index,
                    "high": bar["h"],
                    "low": bar["l"],
                }
                pullback_bars = 1
                continue

            pullback_leg1["end"] = index
            pullback_leg1["high"] = max(pullback_leg1["high"], bar["h"])
            pullback_leg1["low"] = min(pullback_leg1["low"], bar["l"])
            pullback_bars = pullback_leg1["end"] - pullback_leg1["start"] + 1

            extreme = pullback_leg1["low"] if bias == "Long" else pullback_leg1["high"]
            current_retracement = retracement(extreme)
            if pullback_bars > max_pullback_bars:
                return fail("PullbackTooLong")
            if current_retracement > max_pullback_retracement:
                return fail("PullbackTooDeep")
            if pullback_bars >= min_pullback_bars and current_retracement >= min_pullback_retracement:
                advance_state("TrackingSeparation")
            continue

        if state == "TrackingSeparation":
            if pullback_bars > max_pullback_bars:
                return fail("PullbackTooLong")

            if bias == "Long":
                separation = (
                    previous is not None
                    and bar["l"] >= pullback_leg1["low"]
                    and bar["c"] > previous["c"]
                    and bar["h"] > previous["h"]
                )
                if separation:
                    separation_high = bar["h"]
                    separation_low = bar["l"]
                    advance_state("TrackingPullbackLeg2")
                    continue

                extends_pullback = (
                    previous is not None
                    and (bar["l"] < pullback_leg1["low"] or bar["c"] < previous["c"])
                )
                if extends_pullback:
                    pullback_leg1["end"] = index
                    pullback_leg1["high"] = max(pullback_leg1["high"], bar["h"])
                    pullback_leg1["low"] = min(pullback_leg1["low"], bar["l"])
                    pullback_bars = pullback_leg1["end"] - pullback_leg1["start"] + 1
                    if retracement(pullback_leg1["low"]) > max_pullback_retracement:
                        return fail("PullbackTooDeep")
                    if pullback_bars > max_pullback_bars:
                        return fail("PullbackTooLong")
            else:
                separation = (
                    previous is not None
                    and bar["h"] <= pullback_leg1["high"]
                    and bar["c"] < previous["c"]
                    and bar["l"] < previous["l"]
                )
                if separation:
                    separation_high = bar["h"]
                    separation_low = bar["l"]
                    advance_state("TrackingPullbackLeg2")
                    continue

                extends_pullback = (
                    previous is not None
                    and (bar["h"] > pullback_leg1["high"] or bar["c"] > previous["c"])
                )
                if extends_pullback:
                    pullback_leg1["end"] = index
                    pullback_leg1["high"] = max(pullback_leg1["high"], bar["h"])
                    pullback_leg1["low"] = min(pullback_leg1["low"], bar["l"])
                    pullback_bars = pullback_leg1["end"] - pullback_leg1["start"] + 1
                    if retracement(pullback_leg1["high"]) > max_pullback_retracement:
                        return fail("PullbackTooDeep")
                    if pullback_bars > max_pullback_bars:
                        return fail("PullbackTooLong")
            continue

        if state == "TrackingPullbackLeg2":
            if pullback_leg2 is None:
                if previous is None:
                    starting_bar = False
                elif bias == "Long":
                    starting_bar = bar["l"] < previous["l"] or bar["c"] < previous["c"]
                else:
                    starting_bar = bar["h"] > previous["h"] or bar["c"] > previous["c"]
                if not starting_bar:
                    continue
                pullback_leg2 = {
                    "start": index,
                    "end": index,
                    "high": bar["h"],
                    "low": bar["l"],
                }
            else:
                pullback_leg2["end"] = index
                pullback_leg2["high"] = max(pullback_leg2["high"], bar["h"])
                pullback_leg2["low"] = min(pullback_leg2["low"], bar["l"])

            total_pullback_bars = index - pullback_leg1["start"] + 1
            leg2_extreme = pullback_leg2["low"] if bias == "Long" else pullback_leg2["high"]
            current_retracement = retracement(leg2_extreme)
            if total_pullback_bars > max_pullback_bars:
                return fail("PullbackTooLong")
            if current_retracement > max_pullback_retracement:
                return fail("PullbackTooDeep")
            if current_retracement < min_pullback_retracement:
                continue

            leg2_countertrend_move = (
                separation_high - pullback_leg2["low"]
                if bias == "Long"
                else pullback_leg2["high"] - separation_low
            )
            leg2_bars = pullback_leg2["end"] - pullback_leg2["start"] + 1
            leg2_momentum = leg2_countertrend_move / max(leg2_bars, 1)
            if leg2_momentum > impulse_momentum * second_leg_max_momentum_ratio:
                block_reason = "SecondLegTooStrong"
                continue

            if not corrective_ema200_valid:
                return fail("CorrectiveSideInvalid")

            block_reason = ""
            advance_state("WaitingForSignalBar")
            continue

        if state == "WaitingForSignalBar":
            if not corrective_ema200_valid:
                return fail("CorrectiveSideInvalid")

            extends_leg2_extreme = False
            if bias == "Long":
                continues_countertrend = previous is not None and (
                    bar["l"] < pullback_leg2["low"] or bar["c"] < previous["c"]
                )
            else:
                continues_countertrend = previous is not None and (
                    bar["h"] > pullback_leg2["high"] or bar["c"] > previous["c"]
                )

            if continues_countertrend:
                if bias == "Long":
                    extends_leg2_extreme = bar["l"] < pullback_leg2["low"]
                else:
                    extends_leg2_extreme = bar["h"] > pullback_leg2["high"]

                pullback_leg2["end"] = index
                pullback_leg2["high"] = max(pullback_leg2["high"], bar["h"])
                pullback_leg2["low"] = min(pullback_leg2["low"], bar["l"])

                total_pullback_bars = index - pullback_leg1["start"] + 1
                leg2_extreme = pullback_leg2["low"] if bias == "Long" else pullback_leg2["high"]
                current_retracement = retracement(leg2_extreme)
                if total_pullback_bars > max_pullback_bars:
                    return fail("PullbackTooLong")
                if current_retracement > max_pullback_retracement:
                    return fail("PullbackTooDeep")

                leg2_countertrend_move = (
                    separation_high - pullback_leg2["low"]
                    if bias == "Long"
                    else pullback_leg2["high"] - separation_low
                )
                leg2_bars = pullback_leg2["end"] - pullback_leg2["start"] + 1
                leg2_momentum = leg2_countertrend_move / max(leg2_bars, 1)
                if leg2_momentum > impulse_momentum * second_leg_max_momentum_ratio:
                    block_reason = "SecondLegTooStrong"
                    advance_state("TrackingPullbackLeg2")
                    continue

            if extends_leg2_extreme:
                block_reason = "SignalInvalid"
                advance_state("TrackingPullbackLeg2")
                continue

            midpoint = bar["l"] + ((bar["h"] - bar["l"]) * 0.5)
            signal_valid = (
                bar["l"] >= pullback_leg2["low"] and bar["c"] >= midpoint
                if bias == "Long"
                else bar["h"] <= pullback_leg2["high"] and bar["c"] <= midpoint
            )
            if signal_valid:
                block_reason = ""
                advance_state("WaitingForTrigger")
                return {
                    "final_state": "WaitingForTrigger",
                    "block_reason": "",
                    "signal_valid": True,
                    "state_path": list(state_path),
                }

    return {
        "final_state": state,
        "block_reason": block_reason,
        "signal_valid": False,
        "state_path": list(state_path),
    }
