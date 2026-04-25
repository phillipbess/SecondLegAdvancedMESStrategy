param(
    [string]$ProjectId = "30609547",
    [string]$StartDate = "2021-04-24",
    [string]$EndDate = "2026-04-23",
    [int]$DelaySeconds = 8,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\mfe_exit_sweep"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$base = [ordered]@{
    entryMode = "VideoSecondEntryLite"
    sideFilter = "both"
    liteMinImpulseAtr = "0.75"
    liteMaxPullbackRetracement = "0.95"
    liteMaxLeg2Retracement = "0.95"
    liteMinSignalClosePct = "0.55"
    liteBlockedHours = "13"
    liteBlockedShortHours = "11"
    touchProbeR = "1.0"
    maxOutcomeBars = "24"
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

$scenarios = @()
foreach ($target in @("0.50", "0.75", "1.00", "1.25", "1.50", "2.00")) {
    $token = $target.Replace(".", "p")
    $scenarios += [ordered]@{
        Name = "SecondLeg exit target $target hold24 5y"
        File = "target_${token}_hold24_block13_short11_5y.txt"
        Params = Join-Params ([ordered]@{ profitTargetR = $target })
    }
}

foreach ($room in @("0.25", "0.50", "1.00")) {
    $token = $room.Replace(".", "p")
    $scenarios += [ordered]@{
        Name = "SecondLeg roomMax $room target2 hold24 5y"
        File = "room_max_${token}_target_2p00_hold24_block13_short11_5y.txt"
        Params = Join-Params ([ordered]@{ profitTargetR = "2.00"; liteEntryRoomMaxR = $room })
    }
}

foreach ($room in @("0.25", "0.50")) {
    $token = $room.Replace(".", "p")
    $scenarios += [ordered]@{
        Name = "SecondLeg long roomMax $room target2 hold24 5y"
        File = "long_room_max_${token}_target_2p00_hold24_block13_5y.txt"
        Params = Join-Params ([ordered]@{ sideFilter = "long"; liteBlockedShortHours = ""; profitTargetR = "2.00"; liteEntryRoomMaxR = $room })
    }
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
