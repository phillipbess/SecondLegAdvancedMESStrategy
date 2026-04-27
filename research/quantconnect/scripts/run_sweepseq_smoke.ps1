param(
    [string]$ProjectId = "30609547",
    [string]$CloudProjectName = "SecondLegQCSpike 1",
    [string]$StartDate = "2026-02-23",
    [string]$EndDate = "2026-04-23",
    [int]$DelaySeconds = 8,
    [switch]$PushLocal,
    [string]$LeanPath = "$env:LOCALAPPDATA\Packages\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\LocalCache\local-packages\Python311\Scripts\lean.exe"
)

$ErrorActionPreference = "Stop"

$qcRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $qcRoot "results\sweepseq"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

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
    touchProbeR = "1.0"
    maxStopAtr = "4.0"
    riskPerTrade = "150"
    maxRuntimeTradeRows = "10"
    profitTargetR = "1.5"
    maxOutcomeBars = "45"
}

$variants = @(
    [ordered]@{ Token = "stop_both"; Name = "SweepSeq stop-entry both"; EntryType = "stop"; Side = "both" },
    [ordered]@{ Token = "limit_both"; Name = "SweepSeq retest-limit both"; EntryType = "limit"; Side = "both" },
    [ordered]@{ Token = "stop_short"; Name = "SweepSeq stop-entry short"; EntryType = "stop"; Side = "short" },
    [ordered]@{ Token = "limit_short"; Name = "SweepSeq retest-limit short"; EntryType = "limit"; Side = "short" }
)

Push-Location $qcRoot
try {
    if ($PushLocal) {
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

    $dateToken = "$($StartDate.Replace('-', ''))_$($EndDate.Replace('-', ''))"
    foreach ($variant in $variants) {
        $outputPath = Join-Path $outputDir "sweepseq_$($variant.Token)_$dateToken.txt"
        $args = @("cloud", "backtest", $ProjectId, "--name", "$($variant.Name) $dateToken")
        $args += @("--parameter", "startDate", $StartDate)
        $args += @("--parameter", "endDate", $EndDate)
        foreach ($key in $base.Keys) {
            $args += @("--parameter", $key, [string]$base[$key])
        }
        $args += @("--parameter", "sweepSeqEntryType", $variant.EntryType)
        $args += @("--parameter", "sideFilter", $variant.Side)

        Write-Host "Running: $($variant.Name) $dateToken"
        & $LeanPath @args 2>&1 | Tee-Object -FilePath $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "LEAN backtest failed for '$($variant.Token)' with exit code $LASTEXITCODE"
        }

        if ($DelaySeconds -gt 0) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}
finally {
    Pop-Location
}
