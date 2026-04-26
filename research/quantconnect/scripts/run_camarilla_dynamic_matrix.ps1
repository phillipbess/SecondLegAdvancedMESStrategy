param(
    [string]$ProjectId = "30609547",
    [int]$DelaySeconds = 20,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\camarilla_dynamic"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$period = [ordered]@{ Token = "2021_2026"; Start = "2021-04-24"; End = "2026-04-23" }
$variants = @(
    [ordered]@{ Token = "dynamic_base"; Side = "both"; Model = "dynamic"; StopMode = "level"; StopAtr = "1.25"; Flat = "0.015"; Break = "0.025"; Confirm = "2"; Target = "1.5"; Hold = "24"; MaxStopAtr = "4.0"; Risk = "150"; PriorMax = "4.0" },
    [ordered]@{ Token = "tight_dynamic_1p25"; Side = "both"; Model = "dynamic"; StopMode = "tighter"; StopAtr = "1.25"; Flat = "0.025"; Break = "0.035"; Confirm = "2"; Target = "1.5"; Hold = "24"; MaxStopAtr = "4.0"; Risk = "150"; PriorMax = "999" },
    [ordered]@{ Token = "tight_dynamic_1p25_long"; Side = "long"; Model = "dynamic"; StopMode = "tighter"; StopAtr = "1.25"; Flat = "0.025"; Break = "0.035"; Confirm = "2"; Target = "1.5"; Hold = "24"; MaxStopAtr = "4.0"; Risk = "150"; PriorMax = "999" },
    [ordered]@{ Token = "atr_dynamic_1p0"; Side = "both"; Model = "dynamic"; StopMode = "atr"; StopAtr = "1.0"; Flat = "0.025"; Break = "0.035"; Confirm = "2"; Target = "1.5"; Hold = "24"; MaxStopAtr = "4.0"; Risk = "150"; PriorMax = "999" },
    [ordered]@{ Token = "raw_dynamic"; Side = "both"; Model = "dynamic"; StopMode = "level"; StopAtr = "1.25"; Flat = "0.025"; Break = "0.035"; Confirm = "2"; Target = "1.5"; Hold = "24"; MaxStopAtr = "8.0"; Risk = "1000"; PriorMax = "999" },
    [ordered]@{ Token = "raw_fade_short_15r"; Side = "short"; Model = "fade"; StopMode = "level"; StopAtr = "1.25"; Flat = "0.025"; Break = "0.035"; Confirm = "2"; Target = "1.5"; Hold = "24"; MaxStopAtr = "8.0"; Risk = "1000"; PriorMax = "999" }
)

$base = [ordered]@{
    entryMode = "CamarillaDynamic"
    barMinutes = "5"
    camarillaMinSignalMinutes = "15"
    camarillaMaxSignalMinutes = "300"
    camarillaSlopeLookbackBars = "6"
    camarillaStopBufferTicks = "2"
    camarillaMinPriorRangeAtr = "0.5"
    camarillaOneTradePerDay = "true"
    touchProbeR = "1.0"
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

    foreach ($variant in $variants) {
        $outputPath = Join-Path $outputDir "camarilla_$($variant.Token)_$($period.Token).txt"
        if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
            Write-Host "Skipping existing result: $(Split-Path -Leaf $outputPath)"
            continue
        }

        $args = @("cloud", "backtest", $ProjectId, "--name", "Camarilla $($variant.Token) $($period.Token)")
        $args += @("--parameter", "startDate", $period.Start)
        $args += @("--parameter", "endDate", $period.End)
        foreach ($key in $base.Keys) {
            $args += @("--parameter", $key, [string]$base[$key])
        }

        $args += @("--parameter", "sideFilter", $variant.Side)
        $args += @("--parameter", "camarillaModel", $variant.Model)
        $args += @("--parameter", "camarillaStopMode", $variant.StopMode)
        $args += @("--parameter", "camarillaStopAtr", $variant.StopAtr)
        $args += @("--parameter", "camarillaFlatSlopeThreshold", $variant.Flat)
        $args += @("--parameter", "camarillaBreakoutSlopeThreshold", $variant.Break)
        $args += @("--parameter", "camarillaBreakoutConfirmBars", $variant.Confirm)
        $args += @("--parameter", "camarillaMaxPriorRangeAtr", $variant.PriorMax)
        $args += @("--parameter", "profitTargetR", $variant.Target)
        $args += @("--parameter", "maxOutcomeBars", $variant.Hold)
        $args += @("--parameter", "maxStopAtr", $variant.MaxStopAtr)
        $args += @("--parameter", "riskPerTrade", $variant.Risk)

        Write-Host "Running: Camarilla $($variant.Token) $($period.Token)"
        & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "LEAN backtest failed for '$($variant.Token)' with exit code $LASTEXITCODE"
        }

        if ($DelaySeconds -gt 0) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}
finally {
    Pop-Location
}
