param(
    [string]$InputDir = "$(Split-Path -Parent $PSScriptRoot)\results\video_lite",
    [string]$OutputCsv = "$(Split-Path -Parent $PSScriptRoot)\results\video_lite\video_lite_summary.csv",
    [double]$Months = 60.0,
    [switch]$IncludeSmoke
)

$ErrorActionPreference = "Stop"

function Get-TableStat {
    param(
        [string[]]$Lines,
        [string]$Key
    )

    foreach ($line in $Lines) {
        if ($line -notmatch '^\|') {
            continue
        }

        $cells = $line -split '\|' | ForEach-Object { $_.Trim() }
        $cells = $cells | Where-Object { $_ -ne "" }
        for ($i = 0; $i -lt ($cells.Count - 1); $i += 2) {
            if ($cells[$i] -eq $Key) {
                return $cells[$i + 1]
            }
        }
    }

    return ""
}

function Get-BacktestId {
    param([string[]]$Lines)

    foreach ($line in $Lines) {
        if ($line -match '^Backtest id:\s*(?<id>\S+)') {
            return $Matches.id
        }
    }

    return ""
}

function To-Number {
    param([string]$Value)

    $clean = ($Value -replace '[,$%]', '').Trim()
    if ($clean -eq "") {
        return $null
    }

    $number = 0.0
    if ([double]::TryParse($clean, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
        return $number
    }

    return $null
}

$files = Get-ChildItem -LiteralPath $InputDir -Filter "*.txt" | Sort-Object Name
if (-not $IncludeSmoke) {
    $files = $files | Where-Object { $_.BaseName -notlike "smoke_*" }
}

$rows = foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName
    $triggered = To-Number (Get-TableStat $lines "Triggered")
    $netR = To-Number (Get-TableStat $lines "Net R")
    $avgR = To-Number (Get-TableStat $lines "Avg R")
    $win2R = To-Number (Get-TableStat $lines "Win 2R")
    $stops = To-Number (Get-TableStat $lines "Stops")
    $timeouts = To-Number (Get-TableStat $lines "Timeouts")

    [pscustomobject]@{
        variant = $file.BaseName
        triggered = $triggered
        approx_trades_per_month = if ($null -ne $triggered -and $Months -gt 0) { [math]::Round($triggered / $Months, 1) } else { $null }
        win_2r = $win2R
        stops = $stops
        timeouts = $timeouts
        net_r = $netR
        avg_r = $avgR
        armed = To-Number (Get-TableStat $lines "Armed")
        long_armed = To-Number (Get-TableStat $lines "Long Armed")
        short_armed = To-Number (Get-TableStat $lines "Short Armed")
        structure_room_blocks = To-Number (Get-TableStat $lines "StructureRoom")
        backtest_id = Get-BacktestId $lines
    }
}

$rows | Export-Csv -NoTypeInformation -Path $OutputCsv
$rows | Format-Table -AutoSize
