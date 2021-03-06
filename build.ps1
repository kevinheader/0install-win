Param ([Switch]$Deploy, [Switch]$Machine)
$ErrorActionPreference = "Stop"

$RootDir = $(Split-Path -Path $MyInvocation.MyCommand.Definition -Parent)
pushd $RootDir

src\build.ps1
doc\build.ps1
release\build.ps1
powershell\build.ps1

if ($Deploy) {
  if ($Machine) {
    build\Bootstrap\zero-install.exe --feed="$RootDir\release\ZeroInstall-$(Get-Content ..\VERSION).xml" maintenance --batch --machine
  } else {
    build\Bootstrap\zero-install.exe --feed="$RootDir\release\ZeroInstall-$(Get-Content ..\VERSION).xml" maintenance --batch
  }
}

popd
