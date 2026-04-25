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
$outputDir = Join-Path $qcRoot "results\filtered_afternoon_compression"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$scenarios = @(
    [ordered]@{
        Name = "FAC long move1.5 dom3 loc0.7 box4 stop0.75 t1.5";
        File = "FAC_long_move1p5_dom3_loc0p7_box4_stop0p75_t1p5.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "long"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "36"; maxStopAtr = "2.5";
            afternoonCompressionMinMorningMoveAtr = "1.5"; afternoonCompressionMinStrongDominance = "3";
            afternoonCompressionMaxOpeningCounterStrongBars = "2"; afternoonCompressionMinMeasureCloseLocation = "0.7";
            afternoonCompressionMaxBoxAtr = "4.0"; afternoonCompressionStopAtr = "0.75";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "true"
        }
    },
    [ordered]@{
        Name = "FAC both move1.5 dom3 loc0.7 box4 stop0.75 t1.5";
        File = "FAC_both_move1p5_dom3_loc0p7_box4_stop0p75_t1p5.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "36"; maxStopAtr = "2.5";
            afternoonCompressionMinMorningMoveAtr = "1.5"; afternoonCompressionMinStrongDominance = "3";
            afternoonCompressionMaxOpeningCounterStrongBars = "2"; afternoonCompressionMinMeasureCloseLocation = "0.7";
            afternoonCompressionMaxBoxAtr = "4.0"; afternoonCompressionStopAtr = "0.75";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "false"
        }
    },
    [ordered]@{
        Name = "FAC both move1.0 dom2 loc0.65 box4 stop0.75 t1.5";
        File = "FAC_both_move1p0_dom2_loc0p65_box4_stop0p75_t1p5.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "1.5"; maxOutcomeBars = "36"; maxStopAtr = "2.5";
            afternoonCompressionMinMorningMoveAtr = "1.0"; afternoonCompressionMinStrongDominance = "2";
            afternoonCompressionMaxOpeningCounterStrongBars = "2"; afternoonCompressionMinMeasureCloseLocation = "0.65";
            afternoonCompressionMaxBoxAtr = "4.0"; afternoonCompressionStopAtr = "0.75";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "false"
        }
    },
    [ordered]@{
        Name = "FAC both move1.5 dom2 loc0.65 box5 stop1.0 t2.0";
        File = "FAC_both_move1p5_dom2_loc0p65_box5_stop1p0_t2p0.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "both"; barMinutes = "5";
            profitTargetR = "2.0"; maxOutcomeBars = "48"; maxStopAtr = "2.5";
            afternoonCompressionMinMorningMoveAtr = "1.5"; afternoonCompressionMinStrongDominance = "2";
            afternoonCompressionMaxOpeningCounterStrongBars = "2"; afternoonCompressionMinMeasureCloseLocation = "0.65";
            afternoonCompressionMaxBoxAtr = "5.0"; afternoonCompressionStopAtr = "1.0";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "false"
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
