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
$outputDir = Join-Path $qcRoot "results\afternoon_compression"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$scenarios = @(
    [ordered]@{
        Name = "AC long 5y move0.5 box3.0 stop0.75 t1.5";
        File = "AC_long_move0p5_box3p0_stop0p75_t1p5_5y.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "long"; profitTargetR = "1.5";
            maxOutcomeBars = "24"; maxStopAtr = "2.0";
            afternoonCompressionMinMorningMoveAtr = "0.5"; afternoonCompressionMaxBoxAtr = "3.0"; afternoonCompressionStopAtr = "0.75";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "true"
        }
    },
    [ordered]@{
        Name = "AC long 5y move0.5 box4.0 stop0.75 t1.5";
        File = "AC_long_move0p5_box4p0_stop0p75_t1p5_5y.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "long"; profitTargetR = "1.5";
            maxOutcomeBars = "24"; maxStopAtr = "2.0";
            afternoonCompressionMinMorningMoveAtr = "0.5"; afternoonCompressionMaxBoxAtr = "4.0"; afternoonCompressionStopAtr = "0.75";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "true"
        }
    },
    [ordered]@{
        Name = "AC long 5y move0.75 box4.0 stop0.75 t1.5";
        File = "AC_long_move0p75_box4p0_stop0p75_t1p5_5y.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "long"; profitTargetR = "1.5";
            maxOutcomeBars = "24"; maxStopAtr = "2.0";
            afternoonCompressionMinMorningMoveAtr = "0.75"; afternoonCompressionMaxBoxAtr = "4.0"; afternoonCompressionStopAtr = "0.75";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "true"
        }
    },
    [ordered]@{
        Name = "AC long 5y move1.0 box5.0 stop1.0 t2";
        File = "AC_long_move1p0_box5p0_stop1p0_t2p0_5y.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "long"; profitTargetR = "2.0";
            maxOutcomeBars = "24"; maxStopAtr = "2.0";
            afternoonCompressionMinMorningMoveAtr = "1.0"; afternoonCompressionMaxBoxAtr = "5.0"; afternoonCompressionStopAtr = "1.0";
            afternoonCompressionStartMinutes = "180"; afternoonCompressionEndMinutes = "300";
            afternoonCompressionBreakoutEndMinutes = "360"; afternoonCompressionLongOnly = "true"
        }
    },
    [ordered]@{
        Name = "AC both 5y move0.75 box4.0 stop0.75 t1.5";
        File = "AC_both_move0p75_box4p0_stop0p75_t1p5_5y.out.txt";
        Params = [ordered]@{
            entryMode = "AfternoonCompression"; sideFilter = "both"; profitTargetR = "1.5";
            maxOutcomeBars = "24"; maxStopAtr = "2.0";
            afternoonCompressionMinMorningMoveAtr = "0.75"; afternoonCompressionMaxBoxAtr = "4.0"; afternoonCompressionStopAtr = "0.75";
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
