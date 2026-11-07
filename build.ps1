param(
 [string]$BuildDirectory=(Join-Path $PSScriptRoot 'build/cmake-release'),
 [string]$OutputDirectory=(Join-Path $PSScriptRoot 'dist/windows-x64'),
 [string]$CmakePath='cmake', [string]$NinjaPath='ninja', [string]$LlvmMingwRoot='',
 [string]$Generator='Ninja', [switch]$SkipTests
)
$ErrorActionPreference='Stop'
$BuildDirectory=[IO.Path]::GetFullPath($BuildDirectory)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(-not (Test-Path -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'))){throw '.NET Framework x64 C# compiler is required.'}
New-Item -ItemType Directory -Path $BuildDirectory,$OutputDirectory -Force | Out-Null
$probe=Join-Path $OutputDirectory ('.write-probe-'+[Guid]::NewGuid().ToString('N'))
[IO.File]::WriteAllText($probe,'probe');Remove-Item -LiteralPath $probe
$configure=@('-S',$PSScriptRoot,'-B',$BuildDirectory,'-G',$Generator,'-DCMAKE_BUILD_TYPE=Release',('-DSLOPX_PACKAGE_DIR='+$OutputDirectory.Replace([char]92,[char]47)))
if($LlvmMingwRoot){$configure+=@('-DCMAKE_TOOLCHAIN_FILE=cmake/llvm-mingw.cmake',('-DLLVM_MINGW_ROOT='+[IO.Path]::GetFullPath($LlvmMingwRoot).Replace([char]92,[char]47)))}
elseif($Generator -like 'Visual Studio*'){$configure+=@('-A','x64')}
if($Generator -eq 'Ninja'){$ninjaCommand=Get-Command $NinjaPath -ErrorAction Stop;$configure+=('-DCMAKE_MAKE_PROGRAM='+$ninjaCommand.Source.Replace([char]92,[char]47))}
& $CmakePath @configure
if($LASTEXITCODE -ne 0){throw 'CMake configure failed.'}
& $CmakePath --build $BuildDirectory --config Release --parallel 3
if($LASTEXITCODE -ne 0){throw 'CMake build failed.'}
if(-not $SkipTests){$ctest=Join-Path (Split-Path (Get-Command $CmakePath).Source) 'ctest.exe';& $ctest --test-dir $BuildDirectory -C Release --output-on-failure;if($LASTEXITCODE -ne 0){throw 'CTest failed.'}}
foreach($name in @('SlopStudio.exe','SlopNative.dll','SlopAudio.dll','SlopControl.dll','Companions/SlopBridge.exe')){
 $path=Join-Path $OutputDirectory $name
 $bytes=[IO.File]::ReadAllBytes($path);$offset=[BitConverter]::ToInt32($bytes,0x3c)
 if([BitConverter]::ToUInt16($bytes,$offset+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$offset+24) -ne 0x20b){throw "$name is not AMD64 PE32+."}
}
$revision=Join-Path $PSScriptRoot 'sdk-revisions.txt';if(Test-Path -LiteralPath $revision){Copy-Item -LiteralPath $revision -Destination $OutputDirectory -Force}
Write-Output "Built and verified: $OutputDirectory"
