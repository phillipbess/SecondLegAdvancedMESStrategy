param(
    [string]$ProjectId = "30609547",
    [string]$StartDate = "2021-04-24",
    [string]$EndDate = "2026-04-23",
    [int]$DelaySeconds = 45,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\video_lite"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$scenarios = @(
    [ordered]@{
        Name = "SecondLeg strict baseline 5y";
        File = "strict_baseline_5y.txt";
        Params = [ordered]@{
            entryMode = "StrictV1"; sideFilter = "both";
            secondLegMaxMomentumRatio = "0.80"; minRoomToStructureR = "1.00"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite both 5y imp0.75 retr0.95 no structure";
        File = "lite_both_5y_imp_0p75_retr_0p95_no_structure.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "both";
            liteMinImpulseAtr = "0.75"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "false"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite long 5y imp0.75 retr0.95 no structure";
        File = "lite_long_5y_imp_0p75_retr_0p95_no_structure.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "long";
            liteMinImpulseAtr = "0.75"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "false"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite short 5y imp0.75 retr0.95 no structure";
        File = "lite_short_5y_imp_0p75_retr_0p95_no_structure.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "short";
            liteMinImpulseAtr = "0.75"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "false"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite both 5y imp0.75 retr0.95 structure veto";
        File = "lite_both_5y_imp_0p75_retr_0p95_structure_veto.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "both";
            liteMinImpulseAtr = "0.75"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "true"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite long 5y imp0.50 retr0.95 no structure";
        File = "lite_long_5y_imp_0p50_retr_0p95_no_structure.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "long";
            liteMinImpulseAtr = "0.50"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "false"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite long 5y imp1.00 retr0.95 no structure";
        File = "lite_long_5y_imp_1p00_retr_0p95_no_structure.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "long";
            liteMinImpulseAtr = "1.00"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "false"
        }
    },
    [ordered]@{
        Name = "SecondLeg lite long 5y imp1.25 retr0.95 no structure";
        File = "lite_long_5y_imp_1p25_retr_0p95_no_structure.txt";
        Params = [ordered]@{
            entryMode = "VideoSecondEntryLite"; sideFilter = "long";
            liteMinImpulseAtr = "1.25"; liteMaxPullbackRetracement = "0.95"; liteStructureVetoEnabled = "false"
        }
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
