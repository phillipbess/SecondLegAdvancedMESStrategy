param(
    [string]$ProjectId = "30609547",
    [string]$StartDate = "2021-04-24",
    [string]$EndDate = "2026-04-23",
    [int]$DelaySeconds = 30,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\brooks_filtered_tfo"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$scenarios = @(
    [ordered]@{
        Name = "Brooks filtered TFO both m60 move1.5 dom3 loc0.7";
        File = "BTFO_filtered_both_m60_move1p5_dom3_loc0p7.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "60"; maxStopAtr = "2.5";
            brooksMeasureMinutes = "60"; brooksMaxSignalMinutes = "240";
            brooksMinMoveAtr = "1.5"; brooksMinStrongBars = "3";
            brooksMinStrongDominance = "3"; brooksMaxOpeningCounterStrongBars = "2";
            brooksMinMeasureCloseLocation = "0.7"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "false"
        }
    },
    [ordered]@{
        Name = "Brooks filtered TFO long m60 move1.5 dom3 loc0.7";
        File = "BTFO_filtered_long_m60_move1p5_dom3_loc0p7.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "long"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "60"; maxStopAtr = "2.5";
            brooksMeasureMinutes = "60"; brooksMaxSignalMinutes = "240";
            brooksMinMoveAtr = "1.5"; brooksMinStrongBars = "3";
            brooksMinStrongDominance = "3"; brooksMaxOpeningCounterStrongBars = "2";
            brooksMinMeasureCloseLocation = "0.7"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "false"
        }
    },
    [ordered]@{
        Name = "Brooks filtered TFO both m60 move2.0 dom3 loc0.7";
        File = "BTFO_filtered_both_m60_move2p0_dom3_loc0p7.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "60"; maxStopAtr = "2.5";
            brooksMeasureMinutes = "60"; brooksMaxSignalMinutes = "240";
            brooksMinMoveAtr = "2.0"; brooksMinStrongBars = "3";
            brooksMinStrongDominance = "3"; brooksMaxOpeningCounterStrongBars = "2";
            brooksMinMeasureCloseLocation = "0.7"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "false"
        }
    },
    [ordered]@{
        Name = "Brooks filtered TFO both m60 move1.5 dom2 loc0.65";
        File = "BTFO_filtered_both_m60_move1p5_dom2_loc0p65.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "60"; maxStopAtr = "2.5";
            brooksMeasureMinutes = "60"; brooksMaxSignalMinutes = "240";
            brooksMinMoveAtr = "1.5"; brooksMinStrongBars = "3";
            brooksMinStrongDominance = "2"; brooksMaxOpeningCounterStrongBars = "2";
            brooksMinMeasureCloseLocation = "0.65"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "false"
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
