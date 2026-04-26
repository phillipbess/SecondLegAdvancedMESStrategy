param(
    [string]$ProjectId = "30609547",
    [int]$DelaySeconds = 20,
    [switch]$PushLocal,
    [switch]$Force,
    [switch]$Yearly,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\ict"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$periods = @(
    [ordered]@{ Token = "2021_2026"; Start = "2021-04-24"; End = "2026-04-23" }
)

if ($Yearly) {
    $periods = @(
        [ordered]@{ Token = "2021_2026"; Start = "2021-04-24"; End = "2026-04-23" },
        [ordered]@{ Token = "2021_2022"; Start = "2021-04-24"; End = "2022-04-23" },
        [ordered]@{ Token = "2022_2023"; Start = "2022-04-24"; End = "2023-04-23" },
        [ordered]@{ Token = "2023_2024"; Start = "2023-04-24"; End = "2024-04-23" },
        [ordered]@{ Token = "2024_2025"; Start = "2024-04-24"; End = "2025-04-23" },
        [ordered]@{ Token = "2025_2026"; Start = "2025-04-24"; End = "2026-04-23" }
    )
}

$variants = @(
    [ordered]@{ Token = "silverbullet_am_2r"; Model = "silverbullet"; Liquidity = "swing,or,pd"; WindowStart = "30"; WindowEnd = "90"; Target = "2.0"; Hold = "45"; Disp = "0.50"; MaxSweep = "15"; MaxFvg = "10"; OneTrade = "true" },
    [ordered]@{ Token = "silverbullet_am_15r"; Model = "silverbullet"; Liquidity = "swing,or,pd"; WindowStart = "30"; WindowEnd = "90"; Target = "1.5"; Hold = "45"; Disp = "0.50"; MaxSweep = "15"; MaxFvg = "10"; OneTrade = "true" },
    [ordered]@{ Token = "silverbullet_pm_2r"; Model = "silverbullet"; Liquidity = "swing,or,pd"; WindowStart = "270"; WindowEnd = "330"; Target = "2.0"; Hold = "45"; Disp = "0.50"; MaxSweep = "15"; MaxFvg = "10"; OneTrade = "true" },
    [ordered]@{ Token = "silverbullet_pm_15r"; Model = "silverbullet"; Liquidity = "swing,or,pd"; WindowStart = "270"; WindowEnd = "330"; Target = "1.5"; Hold = "45"; Disp = "0.50"; MaxSweep = "15"; MaxFvg = "10"; OneTrade = "true" },
    [ordered]@{ Token = "judas_open_2r"; Model = "judas"; Liquidity = "or,pd"; WindowStart = "0"; WindowEnd = "60"; Target = "2.0"; Hold = "60"; Disp = "0.45"; MaxSweep = "20"; MaxFvg = "12"; OneTrade = "true" },
    [ordered]@{ Token = "judas_open_15r"; Model = "judas"; Liquidity = "or,pd"; WindowStart = "0"; WindowEnd = "60"; Target = "1.5"; Hold = "60"; Disp = "0.45"; MaxSweep = "20"; MaxFvg = "12"; OneTrade = "true" },
    [ordered]@{ Token = "ict2022_rth_2r"; Model = "2022"; Liquidity = "swing,or,pd"; WindowStart = "0"; WindowEnd = "300"; Target = "2.0"; Hold = "90"; Disp = "0.50"; MaxSweep = "24"; MaxFvg = "16"; OneTrade = "true" },
    [ordered]@{ Token = "ict2022_rth_15r"; Model = "2022"; Liquidity = "swing,or,pd"; WindowStart = "0"; WindowEnd = "300"; Target = "1.5"; Hold = "90"; Disp = "0.50"; MaxSweep = "24"; MaxFvg = "16"; OneTrade = "true" }
)

$base = [ordered]@{
    entryMode = "ICT"
    barMinutes = "1"
    openingRangeMinutes = "15"
    sideFilter = "both"
    ictLiquidityLookbackBars = "20"
    ictMssLookbackBars = "5"
    ictStopBufferTicks = "2"
    ictMinSweepTicks = "1"
    ictMinFvgTicks = "1"
    ictEntryFvgPct = "0.5"
    touchProbeR = "1.0"
    maxStopAtr = "4.0"
    riskPerTrade = "150"
    maxRuntimeTradeRows = "5"
}

Push-Location $qcRoot
try {
    if ($PushLocal) {
        $localPath = Join-Path $qcRoot $LocalProject
        $cloudNamePath = Join-Path $qcRoot $CloudProjectName
        if ($LocalProject -eq $CloudProjectName) {
            & $LeanPath cloud push --project $LocalProject --force
        }
        else {
            if (Test-Path -LiteralPath $cloudNamePath) {
                throw "Temporary push folder already exists: $cloudNamePath"
            }

            Copy-Item -LiteralPath $localPath -Destination $cloudNamePath -Recurse
            try {
                & $LeanPath cloud push --project $CloudProjectName --force
            }
            finally {
                $resolvedRoot = (Resolve-Path -LiteralPath $qcRoot).Path
                $resolvedTemp = (Resolve-Path -LiteralPath $cloudNamePath).Path
                if (-not $resolvedTemp.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Refusing to remove unexpected temp path: $resolvedTemp"
                }
                Remove-Item -LiteralPath $cloudNamePath -Recurse -Force
            }
        }

        if ($LASTEXITCODE -ne 0) {
            throw "LEAN cloud push failed with exit code $LASTEXITCODE"
        }
    }

    foreach ($period in $periods) {
        foreach ($variant in $variants) {
            $outputPath = Join-Path $outputDir "ict_$($variant.Token)_$($period.Token).txt"
            if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
                Write-Host "Skipping existing result: $(Split-Path -Leaf $outputPath)"
                continue
            }

            $args = @("cloud", "backtest", $ProjectId, "--name", "ICT $($variant.Token) $($period.Token)")
            $args += @("--parameter", "startDate", $period.Start)
            $args += @("--parameter", "endDate", $period.End)
            foreach ($key in $base.Keys) {
                $args += @("--parameter", $key, [string]$base[$key])
            }

            $args += @("--parameter", "ictModel", $variant.Model)
            $args += @("--parameter", "ictLiquiditySet", $variant.Liquidity)
            $args += @("--parameter", "ictWindowStartMinutes", $variant.WindowStart)
            $args += @("--parameter", "ictWindowEndMinutes", $variant.WindowEnd)
            $args += @("--parameter", "ictMinDisplacementAtr", $variant.Disp)
            $args += @("--parameter", "ictMaxBarsAfterSweep", $variant.MaxSweep)
            $args += @("--parameter", "ictMaxBarsAfterFvg", $variant.MaxFvg)
            $args += @("--parameter", "ictOneTradePerDay", $variant.OneTrade)
            $args += @("--parameter", "profitTargetR", $variant.Target)
            $args += @("--parameter", "maxOutcomeBars", $variant.Hold)

            Write-Host "Running: ICT $($variant.Token) $($period.Token)"
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
