param([string]$ToolsRoot=(Join-Path $PSScriptRoot 'build/tools'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $ToolsRoot -Force | Out-Null
$packages=@(
 @{Name='cmake';Url='https://github.com/Kitware/CMake/releases/download/v4.4.4/cmake-4.4.4-windows-x86_64.zip';Sha='BACE36E94B31C68AB6FA295F26DFA11219E0701CF7C94B0284A7D1CB13DAC536'},
 @{Name='ninja';Url='https://github.com/ninja-build/ninja/releases/download/v1.13.2/ninja-win.zip';Sha='07FC8261B42B20E71D1720B39068C2E14FFCEE6396B76FB7A795FB460B78DC65'},
 @{Name='llvm-mingw';Url='https://github.com/mstorsjo/llvm-mingw/releases/download/20261020/llvm-mingw-20261020-ucrt-x86_64.zip';Sha='FD5462B7F5D93B2FF981FCD3F9C7E427E47F3203C64EA91C27BCC444EEBD74CD'}
)
foreach($package in $packages){
 $archive=Join-Path $ToolsRoot ($package.Name+'.zip')
 if(-not(Test-Path -LiteralPath $archive)){Invoke-WebRequest -Uri $package.Url -OutFile $archive}
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.Sha){throw "Checksum mismatch: $archive"}
 $destination=Join-Path $ToolsRoot $package.Name
 if(-not(Test-Path -LiteralPath $destination)){Expand-Archive -LiteralPath $archive -DestinationPath $destination}
}
Write-Output 'Portable official tools downloaded and SHA-256 checked. See docs/build-and-test.md.'
