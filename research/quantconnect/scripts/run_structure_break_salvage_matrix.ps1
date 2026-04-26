param(
    [string]$ProjectId = "30609547",
    [string]$StartDate = "2021-04-24",
    [string]$EndDate = "2026-04-23",
    [int]$DelaySeconds = 25,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\structure_break_salvage"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$base = [ordered]@{
    entryMode = "VideoSecondEntryLite"
    sideFilter = "long"
    barMinutes = "5"
    liteMinImpulseAtr = "0.75"
    liteMaxPullbackRetracement = "0.95"
    liteMaxLeg2Retracement = "0.95"
    liteMinPullbackAtr = "0"
    liteMinLeg2Atr = "0"
    liteMinSignalClosePct = "0.55"
    liteUseStructureBreakEntry = "true"
    liteBlockedHours = "13"
    liteBlockedShortHours = ""
    touchProbeR = "1.0"
    maxOutcomeBars = "24"
    maxTriggerBars = "3"
    maxRuntimeTradeRows = "5"
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
        Name = "SBS long swingH room0.25 target1.5 hold24";
        File = "long_swing_h_room_0p25_target_1p50_hold24_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "SWING_H"; liteEntryRoomMaxR = "0.25"; profitTargetR = "1.50"; maxOutcomeBars = "24"
        })
    },
    [ordered]@{
        Name = "SBS long swingH room0.25 target2.5 hold36";
        File = "long_swing_h_room_0p25_target_2p50_hold36_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "SWING_H"; liteEntryRoomMaxR = "0.25"; profitTargetR = "2.50"; maxOutcomeBars = "36"
        })
    },
    [ordered]@{
        Name = "SBS long swingH room0.50 target1.5 hold24";
        File = "long_swing_h_room_0p50_target_1p50_hold24_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "SWING_H"; liteEntryRoomMaxR = "0.50"; profitTargetR = "1.50"; maxOutcomeBars = "24"
        })
    },
    [ordered]@{
        Name = "SBS long pdh+swingH room0.25 target1.5 hold24";
        File = "long_pdh_swing_h_room_0p25_target_1p50_hold24_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "PDH,SWING_H"; liteEntryRoomMaxR = "0.25"; profitTargetR = "1.50"; maxOutcomeBars = "24"
        })
    },
    [ordered]@{
        Name = "SBS long pdh+swingH room0.25 target2 hold24";
        File = "long_pdh_swing_h_room_0p25_target_2p00_hold24_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "PDH,SWING_H"; liteEntryRoomMaxR = "0.25"; profitTargetR = "2.00"; maxOutcomeBars = "24"
        })
    },
    [ordered]@{
        Name = "SBS long pdh+swingH room0.50 target1.5 hold24";
        File = "long_pdh_swing_h_room_0p50_target_1p50_hold24_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "PDH,SWING_H"; liteEntryRoomMaxR = "0.50"; profitTargetR = "1.50"; maxOutcomeBars = "24"
        })
    },
    [ordered]@{
        Name = "SBS long pdh+swingH room0.50 target2.5 hold36";
        File = "long_pdh_swing_h_room_0p50_target_2p50_hold36_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "PDH,SWING_H"; liteEntryRoomMaxR = "0.50"; profitTargetR = "2.50"; maxOutcomeBars = "36"
        })
    },
    [ordered]@{
        Name = "SBS long pdh+swingH room1.00 target1.5 hold24";
        File = "long_pdh_swing_h_room_1p00_target_1p50_hold24_block13_5y.txt";
        Params = Join-Params ([ordered]@{
            liteAllowedStructures = "PDH,SWING_H"; liteEntryRoomMaxR = "1.00"; profitTargetR = "1.50"; maxOutcomeBars = "24"
        })
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
