param(
    [string]$ProjectId = "30609547",
    [string]$StartDate = "2021-04-24",
    [string]$EndDate = "2026-04-23",
    [int]$DelaySeconds = 20,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\video_lite_v2"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$base = [ordered]@{
    entryMode = "VideoSecondEntryLite"
    liteMinImpulseAtr = "0.75"
    liteMaxPullbackRetracement = "0.95"
    liteMaxLeg2Retracement = "0.95"
    liteMinPullbackAtr = "0.00"
    liteMinLeg2Atr = "0.00"
    liteMinSignalClosePct = "0.55"
    liteSignalProgressRequired = "false"
    liteResetOnImpulseBreak = "false"
    liteForbidNegativeRetracement = "false"
    maxRuntimeTradeRows = "10"
}

function Join-Params {
    param([System.Collections.Specialized.OrderedDictionary]$Extra)

    $merged = [ordered]@{}
    foreach ($key in $base.Keys) {
        $merged[$key] = $base[$key]
    }
    foreach ($key in $Extra.Keys) {
        $merged[$key] = $Extra[$key]
    }
    return $merged
}

$scenarios = @(
    [ordered]@{
        Name = "SecondLeg V2 long quality 5y"
        File = "v2_long_quality_5y.txt"
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "long"
            liteMinImpulseAtr = "1.00"; liteMaxPullbackRetracement = "0.95"; liteMaxLeg2Retracement = "0.618"
            liteMinPullbackAtr = "0.10"; liteMinLeg2Atr = "0.05"; liteMinSignalClosePct = "0.60"
            liteSignalProgressRequired = "true"; liteResetOnImpulseBreak = "true"; liteForbidNegativeRetracement = "true"
            liteBlockedHours = "13"; maxRuntimeTradeRows = "10"
        }
    },
    [ordered]@{
        Name = "SecondLeg V2 long frequency 5y"
        File = "v2_long_frequency_5y.txt"
        Params = Join-Params ([ordered]@{ sideFilter = "long" })
    },
    [ordered]@{
        Name = "SecondLeg V2 both frequency 5y"
        File = "v2_both_frequency_5y.txt"
        Params = Join-Params ([ordered]@{ sideFilter = "both" })
    },
    [ordered]@{
        Name = "SecondLeg V2 both frequency block13 5y"
        File = "v2_both_frequency_block13_5y.txt"
        Params = Join-Params ([ordered]@{ sideFilter = "both"; liteBlockedHours = "13" })
    },
    [ordered]@{
        Name = "SecondLeg V2 both block13 short11 5y"
        File = "v2_both_frequency_block13_short11_5y.txt"
        Params = Join-Params ([ordered]@{ sideFilter = "both"; liteBlockedHours = "13"; liteBlockedShortHours = "11" })
    }
)

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

    foreach ($scenario in $scenarios) {
        $outputPath = Join-Path $outputDir $scenario.File
        if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
            Write-Host "Skipping existing result: $($scenario.File)"
            continue
        }

        $args = @("cloud", "backtest", $ProjectId, "--name", $scenario.Name)
        $args += @("--parameter", "startDate", $StartDate)
        $args += @("--parameter", "endDate", $EndDate)
        foreach ($key in $scenario.Params.Keys) {
            $args += @("--parameter", $key, [string]$scenario.Params[$key])
        }

        Write-Host "Running: $($scenario.Name)"
        & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "LEAN backtest failed for '$($scenario.Name)' with exit code $LASTEXITCODE"
        }

        if ($DelaySeconds -gt 0) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}
finally {
    Pop-Location
}
