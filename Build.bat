@echo off
setlocal DisableDelayedExpansion
echo Copyright (c) 2026 Sakai. All rights reserved.
set "BV_FRAMEWORK=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319"
if exist "%BV_FRAMEWORK%\csc.exe" goto compiler_ready
set "BV_FRAMEWORK=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%BV_FRAMEWORK%\csc.exe" goto missing
:compiler_ready
cd /d "%SystemRoot%\System32"
if errorlevel 1 goto failed
"%BV_FRAMEWORK%\csc.exe" /nologo /noconfig /nostdlib+ /codepage:65001 /optimize+ /target:winexe /platform:anycpu /win32icon:"%~dp0BatteryView.ico" /win32manifest:"%~dp0BatteryView.manifest" /out:"%~dp0BatteryView.exe" /reference:"%BV_FRAMEWORK%\mscorlib.dll" /reference:"%BV_FRAMEWORK%\System.dll" /reference:"%BV_FRAMEWORK%\System.Core.dll" /reference:"%BV_FRAMEWORK%\System.Drawing.dll" /reference:"%BV_FRAMEWORK%\System.Windows.Forms.dll" /reference:"%BV_FRAMEWORK%\System.Management.dll" "%~dp0BatteryView.cs" "%~dp0Texts.cs" "%~dp0Window.cs" "%~dp0Monitoring.cs" "%~dp0SafeFiles.cs" "%~dp0DesktopShortcut.cs" "%~dp0SupportLink.cs" "%~dp0StartupFailure.cs"
if errorlevel 1 goto failed
echo Build passed. Open BatteryView.exe to use the app.
pause
exit /b 0
:missing
echo Windows .NET Framework compiler is unavailable.
goto stop
:failed
echo Build failed. Close BatteryView before rebuilding.
echo Use a writable folder and keep all source files together.
:stop
pause
exit /b 1
