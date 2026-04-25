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
$outputDir = Join-Path $qcRoot "results\brooks_tfo"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$scenarios = @(
    [ordered]@{
        Name = "Brooks TFO 5m long m60 move0.75 strong3";
        File = "BTFO_5m_long_m60_move0p75_strong3.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "long"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "24"; maxStopAtr = "2.0";
            brooksMeasureMinutes = "60"; brooksMaxSignalMinutes = "180";
            brooksMinMoveAtr = "0.75"; brooksMinStrongBars = "3"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.65"; brooksRequireEmaSide = "true"
        }
    },
    [ordered]@{
        Name = "Brooks TFO 5m long m30 move0.5 strong2";
        File = "BTFO_5m_long_m30_move0p5_strong2.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "long"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "24"; maxStopAtr = "2.0";
            brooksMeasureMinutes = "30"; brooksMaxSignalMinutes = "180";
            brooksMinMoveAtr = "0.5"; brooksMinStrongBars = "2"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "true"
        }
    },
    [ordered]@{
        Name = "Brooks TFO 5m both m30 move0.5 strong2";
        File = "BTFO_5m_both_m30_move0p5_strong2.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "24"; maxStopAtr = "2.0";
            brooksMeasureMinutes = "30"; brooksMaxSignalMinutes = "180";
            brooksMinMoveAtr = "0.5"; brooksMinStrongBars = "2"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "true"
        }
    },
    [ordered]@{
        Name = "Brooks TFO 5m both m60 move0.75 strong3";
        File = "BTFO_5m_both_m60_move0p75_strong3.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "24"; maxStopAtr = "2.0";
            brooksMeasureMinutes = "60"; brooksMaxSignalMinutes = "240";
            brooksMinMoveAtr = "0.75"; brooksMinStrongBars = "3"; brooksMinPullbackAtr = "0.25";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "true"
        }
    },
    [ordered]@{
        Name = "Brooks TFO 1m long m30 move0.5 strong6";
        File = "BTFO_1m_long_m30_move0p5_strong6.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "long"; barMinutes = "1";
            profitTargetR = "1.5"; maxOutcomeBars = "120"; maxStopAtr = "2.0";
            brooksMeasureMinutes = "30"; brooksMaxSignalMinutes = "180";
            brooksMinMoveAtr = "0.5"; brooksMinStrongBars = "6"; brooksMinPullbackAtr = "0.20";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "true"
        }
    },
    [ordered]@{
        Name = "Brooks TFO 1m both m30 move0.5 strong6";
        File = "BTFO_1m_both_m30_move0p5_strong6.out.txt";
        Params = [ordered]@{
            entryMode = "BrooksTFO"; sideFilter = "both"; barMinutes = "1";
            profitTargetR = "1.5"; maxOutcomeBars = "120"; maxStopAtr = "2.0";
            brooksMeasureMinutes = "30"; brooksMaxSignalMinutes = "180";
            brooksMinMoveAtr = "0.5"; brooksMinStrongBars = "6"; brooksMinPullbackAtr = "0.20";
            brooksMaxRetrace = "0.75"; brooksRequireEmaSide = "true"
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
