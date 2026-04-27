param(
    [string]$ProjectId = "30609547",
    [int]$DelaySeconds = 8,
    [switch]$Force,
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\ict_sequenced"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$periods = @(
    [ordered]@{ Token = "20260223_20260423"; Start = "2026-02-23"; End = "2026-04-23" },
    [ordered]@{ Token = "20251223_20260223"; Start = "2025-12-23"; End = "2026-02-23" },
    [ordered]@{ Token = "20251023_20251223"; Start = "2025-10-23"; End = "2025-12-23" },
    [ordered]@{ Token = "20250823_20251023"; Start = "2025-08-23"; End = "2025-10-23" },
    [ordered]@{ Token = "20250623_20250823"; Start = "2025-06-23"; End = "2025-08-23" },
    [ordered]@{ Token = "20250423_20250623"; Start = "2025-04-23"; End = "2025-06-23" }
)

$variants = @(
    [ordered]@{ Token = "pdh_2r"; Name = "ICTSeq PDH 2R"; Model = "silverbullet"; Side = "short"; Liquidity = "pd"; WindowStart = "30"; WindowEnd = "90"; Target = "2.0"; Hold = "45"; MaxSweep = "15"; MaxFvg = "10" },
    [ordered]@{ Token = "2022_rth_15r"; Name = "ICTSeq 2022 RTH 1.5R"; Model = "2022"; Side = "both"; Liquidity = "swing,or,pd"; WindowStart = "0"; WindowEnd = "300"; Target = "1.5"; Hold = "90"; MaxSweep = "24"; MaxFvg = "16" }
)

$base = [ordered]@{
    entryMode = "ICTSequenced"
    barMinutes = "1"
    openingRangeMinutes = "15"
    ictLiquidityLookbackBars = "20"
    ictMssLookbackBars = "5"
    ictStopBufferTicks = "2"
    ictMinSweepTicks = "1"
    ictMinFvgTicks = "1"
    ictEntryFvgPct = "0.5"
    touchProbeR = "1.0"
    maxStopAtr = "4.0"
    riskPerTrade = "150"
    maxRuntimeTradeRows = "10"
    ictOneTradePerDay = "true"
    ictMinDisplacementAtr = "0.5"
}

Push-Location $qcRoot
try {
    foreach ($period in $periods) {
        foreach ($variant in $variants) {
            $outputPath = Join-Path $outputDir "ictseq_$($variant.Token)_$($period.Token).txt"
            if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
                Write-Host "Skipping existing result: $(Split-Path -Leaf $outputPath)"
                continue
            }

            $args = @("cloud", "backtest", $ProjectId, "--name", "$($variant.Name) $($period.Token)")
            $args += @("--parameter", "startDate", $period.Start)
            $args += @("--parameter", "endDate", $period.End)
            foreach ($key in $base.Keys) {
                $args += @("--parameter", $key, [string]$base[$key])
            }

            $args += @("--parameter", "ictModel", $variant.Model)
            $args += @("--parameter", "sideFilter", $variant.Side)
            $args += @("--parameter", "ictLiquiditySet", $variant.Liquidity)
            $args += @("--parameter", "ictWindowStartMinutes", $variant.WindowStart)
            $args += @("--parameter", "ictWindowEndMinutes", $variant.WindowEnd)
            $args += @("--parameter", "ictMaxBarsAfterSweep", $variant.MaxSweep)
            $args += @("--parameter", "ictMaxBarsAfterFvg", $variant.MaxFvg)
            $args += @("--parameter", "profitTargetR", $variant.Target)
            $args += @("--parameter", "maxOutcomeBars", $variant.Hold)

            Write-Host "Running: $($variant.Name) $($period.Token)"
            & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
            if ($LASTEXITCODE -ne 0) {
                throw "LEAN backtest failed for '$($variant.Token)' '$($period.Token)' with exit code $LASTEXITCODE"
            }

            if ($DelaySeconds -gt 0) {
                Start-Sleep -Seconds $DelaySeconds
            }
        }
    }
}
finally {
    Pop-Location
}
