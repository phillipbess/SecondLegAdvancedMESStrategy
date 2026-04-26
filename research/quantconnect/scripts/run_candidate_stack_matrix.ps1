param(
    [string]$ProjectId = "30609547",
    [int]$DelaySeconds = 25,
    [switch]$PushLocal,
    [switch]$Force,
    [string]$LocalProject = "SecondLegQCSpike",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\candidate_stack"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$periods = @(
    [ordered]@{ Token = "2021_2026"; Start = "2021-04-24"; End = "2026-04-23" },
    [ordered]@{ Token = "2021_2022"; Start = "2021-04-24"; End = "2022-04-23" },
    [ordered]@{ Token = "2022_2023"; Start = "2022-04-24"; End = "2023-04-23" },
    [ordered]@{ Token = "2023_2024"; Start = "2023-04-24"; End = "2024-04-23" },
    [ordered]@{ Token = "2024_2025"; Start = "2024-04-24"; End = "2025-04-23" },
    [ordered]@{ Token = "2025_2026"; Start = "2025-04-24"; End = "2026-04-23" }
)

$base = [ordered]@{
    entryMode = "CandidateStack"
    barMinutes = "5"
    openingRangeMinutes = "15"
    candidateStackOpeningSide = "both"
    candidateStackAfternoonSide = "long"
    openingAuctionModel = "accepted"
    openingAuctionConfirmBars = "2"
    openingAuctionFailureBars = "3"
    openingAuctionMaxSignalMinutes = "90"
    openingAuctionStopBufferTicks = "2"
    openingAuctionOneTradePerDay = "true"
    openingAuctionMinRangeAtr = "0.75"
    openingAuctionMaxRangeAtr = "2.5"
    openingAuctionMaxRoomR = "1.0"
    afternoonMomentumMeasureMinutes = "60"
    afternoonMomentumStartMinutes = "300"
    afternoonMomentumEndMinutes = "345"
    afternoonMomentumMinMoveAtr = "0.5"
    afternoonMomentumStopAtr = "0.75"
    profitTargetR = "1.5"
    maxOutcomeBars = "24"
    touchProbeR = "1.0"
    maxStopAtr = "3.0"
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
        $outputPath = Join-Path $outputDir "candidate_stack_oa_both_am_long_$($period.Token).txt"
        if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
            Write-Host "Skipping existing result: $(Split-Path -Leaf $outputPath)"
            continue
        }

        $args = @("cloud", "backtest", $ProjectId, "--name", "CandidateStack OAR broad + AM long $($period.Token)")
        $args += @("--parameter", "startDate", $period.Start)
        $args += @("--parameter", "endDate", $period.End)
        foreach ($key in $base.Keys) {
            $args += @("--parameter", $key, [string]$base[$key])
        }

        Write-Host "Running: CandidateStack OAR broad + AM long $($period.Token)"
        & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "LEAN backtest failed for '$($period.Token)' with exit code $LASTEXITCODE"
        }

        if ($DelaySeconds -gt 0) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}
finally {
    Pop-Location
}
