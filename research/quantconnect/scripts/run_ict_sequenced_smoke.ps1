param(
    [string]$ProjectId = "30609547",
    [string]$StartDate = "2026-02-23",
    [string]$EndDate = "2026-04-23",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\ict_sequenced"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$variants = @(
    [ordered]@{ Token = "pdh_2r"; Name = "ICTSeq PDH 2R second bars"; Model = "silverbullet"; Side = "short"; Liquidity = "pd"; WindowStart = "30"; WindowEnd = "90"; Target = "2.0"; Hold = "45"; MaxSweep = "15"; MaxFvg = "10" },
    [ordered]@{ Token = "2022_rth_15r"; Name = "ICTSeq 2022 RTH 1.5R second bars"; Model = "2022"; Side = "both"; Liquidity = "swing,or,pd"; WindowStart = "0"; WindowEnd = "300"; Target = "1.5"; Hold = "90"; MaxSweep = "24"; MaxFvg = "16" }
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
    foreach ($variant in $variants) {
        $dateToken = "$($StartDate.Replace('-', ''))_$($EndDate.Replace('-', ''))"
        $outputPath = Join-Path $outputDir "ictseq_$($variant.Token)_$dateToken.txt"
        $args = @("cloud", "backtest", $ProjectId, "--name", "$($variant.Name) $dateToken")
        $args += @("--parameter", "startDate", $StartDate)
        $args += @("--parameter", "endDate", $EndDate)
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

        Write-Host "Running: $($variant.Name) $dateToken"
        & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "LEAN backtest failed for '$($variant.Token)' with exit code $LASTEXITCODE"
        }

        Start-Sleep -Seconds 8
    }
}
finally {
    Pop-Location
}
