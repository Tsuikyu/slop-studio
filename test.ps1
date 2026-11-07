param(
 [string]$BuildDirectory=(Join-Path $PSScriptRoot 'build/cmake-release'),
 [string]$PackageDirectory=(Join-Path $PSScriptRoot 'dist/windows-x64'),
 [string]$CtestPath='ctest', [switch]$GuiSmoke, [switch]$HardwareLoopback
)
$ErrorActionPreference='Stop'
& $CtestPath --test-dir $BuildDirectory -C Release --output-on-failure
if($LASTEXITCODE -ne 0){throw 'CTest failed.'}
$verify=Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) 'verification'
New-Item -ItemType Directory -Path $verify -Force | Out-Null
if($GuiSmoke){
 $exe=Join-Path ([IO.Path]::GetFullPath($PackageDirectory)) 'SlopStudio.exe'
 foreach($language in @('en-US','de-DE','ja-JP','ko-KR')){
  $report=Join-Path $verify ('gui-'+$language+'.txt')
  $process=Start-Process -FilePath $exe -ArgumentList @('--smoke',('"'+$report+'"'),$language) -WindowStyle Hidden -PassThru
  if(-not $process.WaitForExit(45000)){$process.Kill();throw "GUI smoke timed out: $language"}
  if($process.ExitCode -ne 0){throw ([IO.File]::ReadAllText($report))}
  Get-Content -LiteralPath $report
 }
}
if($HardwareLoopback){& (Join-Path $BuildDirectory 'DeviceIntegrationTests.exe') $verify;if($LASTEXITCODE -ne 0){throw 'Device integration test failed.'}}
