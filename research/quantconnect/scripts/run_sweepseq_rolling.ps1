param(
    [string]$ProjectId = "30609547",
    [int]$DelaySeconds = 20,
    [switch]$Force,
    [switch]$PushLocal,
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\sweepseq"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$periods = @(
    [ordered]@{ Token = "20260223_20260423"; Start = "2026-02-23"; End = "2026-04-23" },
    [ordered]@{ Token = "20251223_20260223"; Start = "2025-12-23"; End = "2026-02-23" },
    [ordered]@{ Token = "20251023_20251223"; Start = "2025-10-23"; End = "2025-12-23" },
    [ordered]@{ Token = "20250823_20251023"; Start = "2025-08-23"; End = "2025-10-23" },
    [ordered]@{ Token = "20250623_20250823"; Start = "2025-06-23"; End = "2025-08-23" },
    [ordered]@{ Token = "20250423_20250623"; Start = "2025-04-23"; End = "2025-06-23" }
)

$base = [ordered]@{
    entryMode = "SweepReclaimSequenced"
    barMinutes = "1"
    openingRangeMinutes = "15"
    sweepSeqLevels = "PDH,PDL,ORH,ORL,SWING_H,SWING_L"
    sweepSeqMinTicks = "1"
    sweepSeqMaxTicks = "16"
    sweepSeqReclaimBars = "3"
    sweepSeqMinSignalMinutes = "15"
    sweepSeqMaxSignalMinutes = "300"
    sweepSeqStopBufferTicks = "2"
    sweepSeqEntryOffsetTicks = "1"
    sweepSeqTriggerExpiryBars = "4"
    sweepSeqMinReclaimClosePct = "0.55"
    sweepSeqMinReclaimBodyPct = "0.35"
    sweepSeqMinDisplacementAtr = "0.25"
    sweepSeqOneTradePerLevel = "true"
    sweepSeqEntryType = "limit"
    touchProbeR = "1.0"
    maxStopAtr = "4.0"
    riskPerTrade = "150"
    maxRuntimeTradeRows = "10"
    profitTargetR = "1.5"
    maxOutcomeBars = "45"
}

$variants = @(
    [ordered]@{ Token = "limit_both"; Name = "SweepSeq limit both"; Side = "both" },
    [ordered]@{ Token = "limit_short"; Name = "SweepSeq limit short"; Side = "short" }
)

function Push-Project {
    $sourceProject = Join-Path $qcRoot "SecondLegQCSpike"
    $pushProject = Join-Path $qcRoot $CloudProjectName
    if (Test-Path -LiteralPath $pushProject) {
        $resolvedRoot = (Resolve-Path -LiteralPath $qcRoot).Path
        $resolvedPush = (Resolve-Path -LiteralPath $pushProject).Path
        if (-not $resolvedPush.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove push project outside QC root: $resolvedPush"
        }
        Remove-Item -LiteralPath $pushProject -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $pushProject | Out-Null
    Copy-Item -Path (Join-Path $sourceProject "*") -Destination $pushProject -Recurse -Force
    try {
        Write-Host "Pushing local project as '$CloudProjectName' to QuantConnect cloud"
        & $LeanPath @("cloud", "push", "--project", $CloudProjectName, "--force")
        if ($LASTEXITCODE -ne 0) {
            throw "LEAN cloud push failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        if (Test-Path -LiteralPath $pushProject) {
            Remove-Item -LiteralPath $pushProject -Recurse -Force
        }
    }
}

Push-Location $qcRoot
try {
    if ($PushLocal) {
        Push-Project
    }

    foreach ($period in $periods) {
        foreach ($variant in $variants) {
            $outputPath = Join-Path $outputDir "sweepseq_$($variant.Token)_$($period.Token).txt"
            if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
                Write-Host "Skipping existing result: $(Split-Path -Leaf $outputPath)"
                continue
            }

            $args = @("cloud", "backtest", $ProjectId, "--name", "$($variant.Name) $($period.Token)")
            $args += @("--parameter", "startDate", $period.Start)
            $args += @("--parameter", "endDate", $period.End)
            foreach ($key in $base.Keys) {
                $args += @("--parameter", $key, [string]$base[$key])
            }
            $args += @("--parameter", "sideFilter", $variant.Side)

            Write-Host "Running: $($variant.Name) $($period.Token)"
            & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
            if ($LASTEXITCODE -ne 0) {
                throw "LEAN backtest failed for '$($variant.Token)' '$($period.Token)' with exit code $LASTEXITCODE"
            }

            if ($DelaySeconds -gt 0) {
                Start-Sleep -Seconds $DelaySeconds
            }
        }
    }
}
finally {
    Pop-Location
}
