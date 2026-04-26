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
$outputDir = Join-Path $qcRoot "results\opening_auction_room_filtered"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not (Test-Path -LiteralPath $LeanPath)) {
    throw "LEAN CLI was not found at '$LeanPath'. Pass -LeanPath with the full lean.exe path."
}

$base = [ordered]@{
    entryMode = "OpeningAuction"
    sideFilter = "both"
    barMinutes = "5"
    openingRangeMinutes = "15"
    openingAuctionModel = "accepted"
    openingAuctionConfirmBars = "2"
    openingAuctionFailureBars = "3"
    openingAuctionMaxSignalMinutes = "90"
    openingAuctionStopBufferTicks = "2"
    openingAuctionOneTradePerDay = "true"
    touchProbeR = "1.0"
    maxStopAtr = "3.0"
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
        Name = "OAR accepted 15m roomMax1 range0.75-2 t1.5";
        File = "accepted_15m_roommax1_range0p75_2p0_t1p5_hold18.txt";
        Params = Join-Params ([ordered]@{
            profitTargetR = "1.5"; maxOutcomeBars = "18"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.0"; openingAuctionMaxRoomR = "1.0"
        })
    },
    [ordered]@{
        Name = "OAR accepted 15m roomMax1 range0.75-2.5 t1.5";
        File = "accepted_15m_roommax1_range0p75_2p5_t1p5_hold24.txt";
        Params = Join-Params ([ordered]@{
            profitTargetR = "1.5"; maxOutcomeBars = "24"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.5"; openingAuctionMaxRoomR = "1.0"
        })
    },
    [ordered]@{
        Name = "OAR accepted 15m roomMax0.75 range0.75-2.5 t1.5";
        File = "accepted_15m_roommax0p75_range0p75_2p5_t1p5_hold24.txt";
        Params = Join-Params ([ordered]@{
            profitTargetR = "1.5"; maxOutcomeBars = "24"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.5"; openingAuctionMaxRoomR = "0.75"
        })
    },
    [ordered]@{
        Name = "OAR accepted 30m roomMax1 range0.5-2 t1.5";
        File = "accepted_30m_roommax1_range0p5_2p0_t1p5_hold24.txt";
        Params = Join-Params ([ordered]@{
            openingRangeMinutes = "30"; openingAuctionMaxSignalMinutes = "120"; profitTargetR = "1.5"; maxOutcomeBars = "24"; openingAuctionMinRangeAtr = "0.5"; openingAuctionMaxRangeAtr = "2.0"; openingAuctionMaxRoomR = "1.0"
        })
    },
    [ordered]@{
        Name = "OAR accepted 15m roomMax1 long range0.75-2 t1.5";
        File = "accepted_15m_long_roommax1_range0p75_2p0_t1p5_hold18.txt";
        Params = Join-Params ([ordered]@{
            sideFilter = "long"; profitTargetR = "1.5"; maxOutcomeBars = "18"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.0"; openingAuctionMaxRoomR = "1.0"
        })
    },
    [ordered]@{
        Name = "OAR accepted 15m roomMax1 short range0.75-2 t1.5";
        File = "accepted_15m_short_roommax1_range0p75_2p0_t1p5_hold18.txt";
        Params = Join-Params ([ordered]@{
            sideFilter = "short"; profitTargetR = "1.5"; maxOutcomeBars = "18"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.0"; openingAuctionMaxRoomR = "1.0"
        })
    },
    [ordered]@{
        Name = "OAR accepted 15m roomMax1 short sig30-90 range0.75-2 t1.5";
        File = "accepted_15m_short_sig30_90_roommax1_range0p75_2p0_t1p5_hold18.txt";
        Params = Join-Params ([ordered]@{
            sideFilter = "short"; profitTargetR = "1.5"; maxOutcomeBars = "18"; openingAuctionMinSignalMinutes = "30"; openingAuctionMaxSignalMinutes = "90"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.0"; openingAuctionMaxRoomR = "1.0"
        })
    },
    [ordered]@{
        Name = "OAR accepted 15m roomMax1 both sig30-90 range0.75-2 t1.5";
        File = "accepted_15m_both_sig30_90_roommax1_range0p75_2p0_t1p5_hold18.txt";
        Params = Join-Params ([ordered]@{
            sideFilter = "both"; profitTargetR = "1.5"; maxOutcomeBars = "18"; openingAuctionMinSignalMinutes = "30"; openingAuctionMaxSignalMinutes = "90"; openingAuctionMinRangeAtr = "0.75"; openingAuctionMaxRangeAtr = "2.0"; openingAuctionMaxRoomR = "1.0"
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
