$ErrorActionPreference='Stop'
$fgDir='C:\Users\Public\StutterFixTrace\fg27'
$fgEnd=(Get-Date).AddHours(3)
$fgStatus=Join-Path $fgDir 'agent2-status.json'
@{status='ready';time=(Get-Date -Format o)} | ConvertTo-Json | Set-Content $fgStatus
while((Get-Date) -lt $fgEnd) {
 $fgCommand=Join-Path $fgDir 'command2.json'
 if(Test-Path -LiteralPath $fgCommand) {
  $fgRequest=Get-Content -LiteralPath $fgCommand -Raw | ConvertFrom-Json
  Remove-Item -LiteralPath $fgCommand
  if($fgRequest.action -eq 'quit') {break}
  if($fgRequest.action -ne 'pm' -or $fgRequest.name -notmatch '^[a-zA-Z0-9_-]+$' -or [int]$fgRequest.pid -le 0) {throw 'Invalid request'}
  $fgTarget=Get-Process -Id ([int]$fgRequest.pid)
  if($fgTarget.ProcessName -ne 'A Dance of Fire and Ice') {throw 'Unexpected target'}
  $fgOutput=Join-Path $fgDir ($fgRequest.name+'.csv')
  if(Test-Path -LiteralPath $fgOutput) {throw 'Preserve existing CSV'}
  $fgArgs=@('--process_id', [string]$fgRequest.pid,'--session_name',('SF27-'+$fgRequest.name),'--output_file',$fgOutput,'--v1_metrics','--qpc_time','--timed','240','--terminate_after_timed','--terminate_on_proc_exit','--no_console_stats')
  $fgPm=Start-Process -FilePath 'C:\SFBundle\PresentMon.exe' -ArgumentList $fgArgs -PassThru -WindowStyle Hidden
  @{status='recording';name=$fgRequest.name;pid=$fgPm.Id;time=(Get-Date -Format o)} | ConvertTo-Json | Set-Content $fgStatus
    while(!$fgPm.HasExited) {
   $fgStop=Join-Path $fgDir ('stop-'+$fgRequest.name)
   if(Test-Path -LiteralPath $fgStop) {
    # PresentMon emits an expected warning on stderr for this command even
    # when exit=0. Preserve it without treating stderr itself as failure.
    $fgPriorPreference=$ErrorActionPreference
    try {
     $ErrorActionPreference='Continue'
     & 'C:\SFBundle\PresentMon.exe' --session_name ('SF27-'+$fgRequest.name) --terminate_existing_session 2>&1 | Out-File (Join-Path $fgDir ($fgRequest.name+'-stop.log')) -Encoding utf8
     $fgStopExit=$LASTEXITCODE
    } finally {$ErrorActionPreference=$fgPriorPreference}
    if($fgStopExit -ne 0){throw "PresentMon stop exit $fgStopExit"}
    Remove-Item -LiteralPath $fgStop
   }
   Start-Sleep -Milliseconds 100
   $fgPm.Refresh()
  }
  @{status='finished';name=$fgRequest.name;exit=$fgPm.ExitCode;time=(Get-Date -Format o)} | ConvertTo-Json | Set-Content $fgStatus
 }
 Start-Sleep -Milliseconds 100
}
@{status='ended';time=(Get-Date -Format o)} | ConvertTo-Json | Set-Content $fgStatus
