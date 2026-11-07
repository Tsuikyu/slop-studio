param([string]$DependencyRoot=(Join-Path $PSScriptRoot 'third_party'))
$ErrorActionPreference='Stop'
function Get-OfficialRepo([string]$Url,[string]$Revision,[string]$Path,[string]$RequiredFile){
 if(Test-Path -LiteralPath (Join-Path $Path $RequiredFile)){Write-Output "Using bundled official headers: $Path";return}
 if((Test-Path -LiteralPath $Path) -and (Get-ChildItem -LiteralPath $Path -Force | Select-Object -First 1)){throw "Refusing to overwrite a nonempty dependency folder: $Path"}
 & git clone --no-checkout $Url $Path
 if($LASTEXITCODE -ne 0){throw "Clone failed: $Url"}
 & git -C $Path checkout --detach $Revision
 if($LASTEXITCODE -ne 0){throw "Pinned checkout failed: $Revision"}
}
$vst=Join-Path $DependencyRoot 'vst3sdk'
Get-OfficialRepo 'https://github.com/steinbergmedia/vst3sdk.git' '3cdf9ca5d1f5b1b21e0a86832aa4abe55607bd96' $vst 'LICENSE.txt'
Get-OfficialRepo 'https://github.com/steinbergmedia/vst3_pluginterfaces.git' '4f547e8e102b47de4a8b8aaf343c73b700786372' (Join-Path $vst 'pluginterfaces') 'vst/ivstaudioprocessor.h'
Get-OfficialRepo 'https://github.com/free-audio/clap.git' 'a47f6badb49d948fd009998f28309cdab78979c9' (Join-Path $DependencyRoot 'clap') 'include/clap/clap.h'

$companions=Join-Path $PSScriptRoot 'companions/third_party'
$link=Join-Path $companions 'link'
Get-OfficialRepo 'https://github.com/ableton/link.git' '5ba907ae027a8c2ec8824463e37296dcefc5e9be' $link 'include/ableton/Link.hpp'
Get-OfficialRepo 'https://github.com/chriskohlhoff/asio.git' '026dce530a6c39a8aa6d98e5177482310c4a01e5' (Join-Path $link 'modules/asio-standalone') 'include/asio.hpp'
$asio=Join-Path $companions 'asiosdk'
if(-not (Test-Path -LiteralPath (Join-Path $asio 'ASIOSDK/common/iasiodrv.h'))){
 New-Item -ItemType Directory -Path $asio -Force | Out-Null
 $archive=Join-Path $asio 'official-sdk.zip'
 Invoke-WebRequest -Uri 'https://download.steinberg.net/sdk_downloads/ASIO-SDK_2.3.4_2025-10-15.zip' -OutFile $archive
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne 'D5EBF0C20DD2C5F43771FD0C1418F4B361BF52434EE670097CFA6B3A335E2ECA'){throw 'Official ASIO SDK archive checksum mismatch.'}
 Expand-Archive -LiteralPath $archive -DestinationPath $asio
}
