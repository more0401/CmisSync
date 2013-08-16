@echo off

REM call %~dp0\..\Common\Plugins\build.cmd

set WinDirNet=%WinDir%\Microsoft.NET\Framework
set msbuild="%WinDirNet%\v4.0\msbuild.exe"
if not exist %msbuild% set msbuild="%WinDirNet%\v4.0.30319\msbuild.exe"
set wixBinDir=%WIX%\bin

if not exist ..\..\bin mkdir ..\..\bin
copy Pixmaps\app.ico ..\..\bin\

%msbuild% /t:Clean,Build /p:Configuration=Debug /p:Platform="Any CPU" "%~dp0\CmisSync.sln"

if "%1"=="installer" (
	if exist "%wixBinDir%" (
	  if exist "%~dp0\DataSpaceSync.msi" del "%~dp0\DataSpaceSync.msi"
		"%wixBinDir%\heat.exe" dir "%~dp0\..\..\bin\plugins" -cg pluginsComponentGroup -gg -scom -sreg -sfrag -srd -dr PLUGINS_DIR -var wix.pluginsdir -o plugins.wxs
		"%wixBinDir%\candle" "%~dp0\CmisSync.wxs" -ext WixUIExtension -ext WixUtilExtension -ext WiXNetFxExtension
		"%wixBinDir%\candle" "%~dp0\plugins.wxs" -ext WixUIExtension -ext WixUtilExtension -ext WiXNetFxExtension
		"%wixBinDir%\light" -ext WixUIExtension -ext WixUtilExtension -ext WiXNetFxExtension -cultures:en-us CmisSync.wixobj plugins.wixobj -droot="%~dp0\..\.." -dpluginsdir="%~dp0\..\..\bin\plugins"  -o DataSpaceSync.msi 
		if exist "%~dp0\DataSpaceSync.msi" echo DataSpaceSync.msi created.
	) else (
		echo Not building installer ^(could not find wix, Windows Installer XML toolset^)
		echo wix is available at http://wix.sourceforge.net/
	)
) else echo Not building installer, as it was not requested. ^(Issue "build.cmd installer" to build installer ^)

