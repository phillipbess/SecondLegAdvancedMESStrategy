param(
  [string]$Path = "C:\Users\bessp\Documents\NinjaTrader 8\logs\SecondLegAdvancedMES",
  [bool]$Latest = $true,
  [ValidateSet('AllDay', 'Last', 'LastActive')][string]$RunSegment = 'AllDay',
  [string]$RunId = $null,
  [string]$LogDate = $null,
  [switch]$PassThru
)

function Get-Percentile([double[]]$vals, [double]$p) {
  if ($null -eq $vals -or $vals.Count -eq 0) { return [double]::NaN }
  $sorted = $vals | Sort-Object
  $idx = [math]::Ceiling($p * $sorted.Count) - 1
  if ($idx -lt 0) { $idx = 0 }
  if ($idx -ge $sorted.Count) { $idx = $sorted.Count - 1 }
  return [double]$sorted[$idx]
}

function Test-SegmentActive([string[]]$segment) {
  foreach ($l in $segment) {
    if ($l -match '\[(ENTRY_SUBMIT|ENTRY_FILL|STOP_ACK|STOP_CHANGE|TRADE_CLOSE|EXIT_FILL)\]') {
      return $true
    }
  }
  return $false
}

$files = Get-ChildItem -Recurse -File -Path $Path -Include Trades_*.txt, Risk_*.txt -ErrorAction SilentlyContinue
$csvFiles = Get-ChildItem -Recurse -File -Path $Path -Include TradesCsv_*.csv, StopEvents_*.csv -ErrorAction SilentlyContinue
if ($files.Count -eq 0) { Write-Host "No log files found under $Path"; exit 1 }

if ($RunId) {
  $files = $files | Where-Object { $_.Name -like ("*{0}*" -f $RunId) }
  $csvFiles = $csvFiles | Where-Object { $_.Name -like ("*{0}*" -f $RunId) }
  if ($files.Count -eq 0) { Write-Host "No files matched the specified RunId."; exit 1 }
}
elseif ($LogDate) {
  $files = $files | Where-Object { $_.Name -like ("*{0}*" -f $LogDate) }
  $csvFiles = $csvFiles | Where-Object { $_.Name -like ("*{0}*" -f $LogDate) }
  if ($files.Count -eq 0) { Write-Host "No files matched the specified LogDate."; exit 1 }
}
elseif ($Latest) {
  $latestFile = $files | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($null -ne $latestFile) {
    $latestDate = $latestFile.LastWriteTime.Date
    $files = $files | Where-Object { $_.LastWriteTime.Date -eq $latestDate }
    $csvFiles = $csvFiles | Where-Object { $_.LastWriteTime.Date -eq $latestDate }
    Write-Host ("Using latest log date: {0:yyyy-MM-dd} (files={1})" -f $latestDate, ($files.Count))
  }
}

$all = @()
foreach ($f in $files) {
  $lines = Get-Content -Path $f -ErrorAction SilentlyContinue
  if (-not $lines) { continue }

  $runStarts = @()
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -like '*=== NEW RUN*') { $runStarts += $i }
  }

  if ($runStarts.Count -eq 0 -or $RunSegment -eq 'AllDay') {
    $all += $lines
    continue
  }

  $chosenStart = $runStarts[-1]
  if ($RunSegment -eq 'LastActive') {
    for ($ri = $runStarts.Count - 1; $ri -ge 0; $ri--) {
      $s = $runStarts[$ri]
      if ($ri -lt $runStarts.Count - 1) { $e = $runStarts[$ri + 1] - 1 } else { $e = $lines.Count - 1 }
      $seg = $lines[$s..$e]
      if (Test-SegmentActive $seg) { $chosenStart = $s; break }
    }
  }

  $endIdx = $lines.Count - 1
  for ($ri = 0; $ri -lt $runStarts.Count - 1; $ri++) {
    if ($runStarts[$ri] -eq $chosenStart) { $endIdx = $runStarts[$ri + 1] - 1; break }
  }
  $all += $lines[$chosenStart..$endIdx]
}

$entrySubmits = 0
$entryFills = 0
$tradeCloses = 0
$wins = 0
$losses = 0
$netPnl = 0.0
$pnlR = @()
$stopSubmits = 0
$stopChanges = 0
$stopAcks = 0
$stopConfirmed = 0
$stopCancelledAck = 0
$stopFills = 0
$coverageStates = 0
$protectiveCoverage = 0
$flattenRequests = 0
$flattenSubmits = 0
$flattenCompletes = 0
$flattenRejects = 0
$recoveryResolutions = 0
$reconnectGrace = 0
$reconnectOutcomes = 0
$orphanChecks = 0
$orphanSweeps = 0
$doubleStops = 0
$qtyMismatch = 0
$ocoResubmits = 0
$adopts = 0
$omHealth = 0
$postFlatSignals = 0
$leakage = 0
$firstStopSla = @()
$firstStopSlaFails = 0
$tradesCsvFiles = @($csvFiles | Where-Object { $_.Name -like 'TradesCsv_*' })
$stopEventsCsvFiles = @($csvFiles | Where-Object { $_.Name -like 'StopEvents_*' })

foreach ($line in $all) {
  if ($line -notmatch '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \| \[[A-Z0-9_ ]+\]') { continue }

  if ($line -match '\[ENTRY_SUBMIT\]') { $entrySubmits++ }
  if ($line -match '\[ENTRY_FILL\]') { $entryFills++ }
  if ($line -match '\[TRADE_CLOSE\]') {
    $tradeCloses++
    $pnlMatch = [regex]::Match($line, 'pnlCurrency=([-0-9.]+)')
    if ($pnlMatch.Success) {
      $p = [double]$pnlMatch.Groups[1].Value
      $netPnl += $p
      if ($p -gt 0) { $wins++ }
      elseif ($p -lt 0) { $losses++ }
    }
    $rMatch = [regex]::Match($line, 'pnlR=([-0-9.]+)')
    if ($rMatch.Success) { $pnlR += [double]$rMatch.Groups[1].Value }
  }

  if ($line -match '\[STOP_SUBMIT\]') { $stopSubmits++ }
  if ($line -match '\[STOP_CHANGE\]') { $stopChanges++ }
  if ($line -match '\[STOP_ACK\]') { $stopAcks++ }
  if ($line -match '\[STOP_CONFIRMED\]') { $stopConfirmed++ }
  if ($line -match '\[STOP_CANCELLED_ACK\]') { $stopCancelledAck++ }
  if ($line -match '\[STOP_FILLED_ACK\]') { $stopFills++ }
  if ($line -match '\[COVERAGE_STATE\]') { $coverageStates++ }
  if ($line -match '\[PROTECTIVE_COVERAGE\]') { $protectiveCoverage++ }
  if ($line -match '\[FLATTEN_REQUEST\]') { $flattenRequests++ }
  if ($line -match '\[FLATTEN_SUBMIT\]') { $flattenSubmits++ }
  if ($line -match '\[FLATTEN_COMPLETE\]') { $flattenCompletes++ }
  if ($line -match '\[FLATTEN_REJECTED\]') { $flattenRejects++ }
  if ($line -match '\[RECOVERY_RESOLUTION\]') { $recoveryResolutions++ }
  if ($line -match '\[RECOVERY_RECONNECT_GRACE\]') { $reconnectGrace++ }
  if ($line -match '\[RECOVERY_RECONNECT_OUTCOME\]') { $reconnectOutcomes++ }
  if ($line -match '\[ORPHAN_CHECK\]') { $orphanChecks++ }
  if ($line -match '\[ORPHAN_SWEEP\]') { $orphanSweeps++ }
  if ($line -match '\[DOUBLE STOP DETECTED\]') { $doubleStops++ }
  if ($line -match '\[STOP_QTY_MISMATCH\]') { $qtyMismatch++ }
  if ($line -match '\[OCO_RESUBMIT\]') { $ocoResubmits++ }
  if ($line -match '\[ADOPT\]') { $adopts++ }
  if ($line -match '\[OM_HEALTH\]') { $omHealth++ }
  if ($line -match '\[STRICT\] Managed API leakage detected') { $leakage++ }

  if ($line -match '\[FIRST_STOP_SLA\]') {
    $m = [regex]::Match($line, '(\d+)ms')
    if ($m.Success) { $firstStopSla += [double]$m.Groups[1].Value }
    if ($line -match '\[FAIL\]') { $firstStopSlaFails++ }
  }

  if ($line -match '\[EXIT_OP_DROP\].*flat' -or $line -match 'position flat at execution') {
    $postFlatSignals++
  }
}

$avgR = if ($pnlR.Count -gt 0) { ($pnlR | Measure-Object -Average).Average } else { [double]::NaN }
$winRate = if ($tradeCloses -gt 0) { [math]::Round(($wins / $tradeCloses) * 100, 1) } else { [double]::NaN }
$firstStopP95 = Get-Percentile $firstStopSla 0.95

$score = 100
$gateMessages = New-Object System.Collections.Generic.List[string]
$hardFail = $false

# G1: No post-flat submissions / execution while flat
if ($postFlatSignals -gt 0) {
  $gateMessages.Add(("G1 FAIL: post-flat execution signals detected ({0})" -f $postFlatSignals))
  $score -= 10
  $hardFail = $true
} else {
  $gateMessages.Add("G1 PASS: no post-flat execution signals detected")
}

# G2: First stop SLA
$g2Fail = $false
if ($firstStopSla.Count -eq 0) { $g2Fail = $true }
elseif ($firstStopP95 -gt 750) { $g2Fail = $true }
if ($firstStopSlaFails -gt 0) { $g2Fail = $true }
if ($g2Fail) {
  $gateMessages.Add(("G2 FAIL: first-stop SLA breach (count={0}, p95={1})" -f $firstStopSla.Count, ($(if ([double]::IsNaN($firstStopP95)) { 'n/a' } else { '{0:N0}ms' -f $firstStopP95 }))))
  $score -= 25
  $hardFail = $true
} else {
  $gateMessages.Add(("G2 PASS: first-stop SLA healthy (p95={0:N0}ms, samples={1})" -f $firstStopP95, $firstStopSla.Count))
}

# G4: Qty parity / double-stop safety
if ($qtyMismatch -gt 0 -or $doubleStops -gt 0) {
  $gateMessages.Add(("G4 FAIL: qty parity issues detected (QtyMismatch={0}, DoubleStops={1})" -f $qtyMismatch, $doubleStops))
  $score -= 20
  $hardFail = $true
} else {
  $gateMessages.Add("G4 PASS: no qty mismatch or double-stop signals detected")
}

# G5: Flatten / orphan hygiene
if ($orphanSweeps -gt 0 -or $flattenRejects -gt 0) {
  $gateMessages.Add(("G5 FAIL: flatten/orphan issues detected (OrphanSweeps={0}, FlattenRejects={1})" -f $orphanSweeps, $flattenRejects))
  $score -= 15
  $hardFail = $true
} else {
  $gateMessages.Add("G5 PASS: no orphan sweeps or flatten rejects detected")
}

# G6: Reconnect behavior
if ($reconnectGrace -gt 0 -and $reconnectOutcomes -eq 0) {
  $gateMessages.Add(("G6 FAIL: reconnect grace observed without outcome (Grace={0}, Outcomes={1})" -f $reconnectGrace, $reconnectOutcomes))
  $score -= 10
  $hardFail = $true
} else {
  $gateMessages.Add(("G6 PASS: reconnect telemetry coherent (Grace={0}, Outcomes={1})" -f $reconnectGrace, $reconnectOutcomes))
}

# G7: Managed API leakage
if ($leakage -gt 0) {
  $gateMessages.Add(("G7 FAIL: managed API leakage detected ({0})" -f $leakage))
  $score -= 10
  $hardFail = $true
} else {
  $gateMessages.Add("G7 PASS: no managed API leakage detected")
}

# G8: OCO hygiene
if ($ocoResubmits -gt 0) {
  $gateMessages.Add(("G8 WARN: OCO resubmits detected ({0})" -f $ocoResubmits))
  $score -= 5
} else {
  $gateMessages.Add("G8 PASS: no OCO resubmits detected")
}

# G10: OM health / recovery sanity
if ($omHealth -lt $tradeCloses) {
  $gateMessages.Add(("G10 WARN: OM_HEALTH count ({0}) trails trade closes ({1})" -f $omHealth, $tradeCloses))
  $score -= 5
} else {
  $gateMessages.Add(("G10 PASS: OM_HEALTH coverage present ({0})" -f $omHealth))
}

$summary = [PSCustomObject]@{
  EntrySubmits = $entrySubmits
  EntryFills = $entryFills
  TradeCloses = $tradeCloses
  Wins = $wins
  Losses = $losses
  WinRatePct = $winRate
  NetPnl = [math]::Round($netPnl, 2)
  AvgR = if ([double]::IsNaN($avgR)) { $null } else { [math]::Round($avgR, 2) }
  StopSubmits = $stopSubmits
  StopChanges = $stopChanges
  StopAcks = $stopAcks
  StopConfirmed = $stopConfirmed
  StopCancelledAck = $stopCancelledAck
  StopFills = $stopFills
  CoverageStates = $coverageStates
  ProtectiveCoverageEvents = $protectiveCoverage
  FirstStopSlaCount = $firstStopSla.Count
  FirstStopSlaP95Ms = if ([double]::IsNaN($firstStopP95)) { $null } else { [math]::Round($firstStopP95, 0) }
  FirstStopSlaFailures = $firstStopSlaFails
  FlattenRequests = $flattenRequests
  FlattenSubmits = $flattenSubmits
  FlattenCompletes = $flattenCompletes
  FlattenRejects = $flattenRejects
  RecoveryResolutions = $recoveryResolutions
  ReconnectGraceEvents = $reconnectGrace
  ReconnectOutcomes = $reconnectOutcomes
  OrphanChecks = $orphanChecks
  OrphanSweeps = $orphanSweeps
  QtyMismatch = $qtyMismatch
  DoubleStops = $doubleStops
  OcoResubmits = $ocoResubmits
  Adopts = $adopts
  OmHealth = $omHealth
  PostFlatSignals = $postFlatSignals
  ManagedLeakage = $leakage
  TradesCsvFiles = $tradesCsvFiles.Count
  StopEventsCsvFiles = $stopEventsCsvFiles.Count
  Score = $score
  OverallStatus = $(if ($hardFail -or $score -lt 90) { "FAIL" } else { "PASS" })
}

Write-Host ""
Write-Host "===== SecondLegAdvanced OM Metrics Summary ====="
$summary | Format-List

Write-Host ""
Write-Host "===== SecondLegAdvanced OM Readiness Gates ====="
foreach ($message in $gateMessages) {
  Write-Host $message
}

Write-Host ""
Write-Host "===== SecondLegAdvanced OM Weighted Score ====="
Write-Host ("Final Score = {0}/100" -f $score)
if ($hardFail -or $score -lt 90) {
  Write-Host ("OVERALL: FAIL (score {0})" -f $score)
} else {
  Write-Host ("OVERALL: PASS (score {0})" -f $score)
}

if ($PassThru) {
  $summary
}
