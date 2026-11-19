@echo off
rem Builds MudCalcPro.exe using the C# compiler that ships with Windows (.NET Framework 4).
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo .NET Framework 4 compiler not found. > build_log.txt
  type build_log.txt
  pause
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:MudCalcPro.exe /r:System.dll /r:System.Core.dll /r:System.Data.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs > build_log.txt 2>&1
if errorlevel 1 (
  type build_log.txt
  echo.
  echo BUILD FAILED - see build_log.txt
  pause
  exit /b 1
)
echo BUILD OK >> build_log.txt
echo Build OK - starting MudCalcPro.exe
start "" "%~dp0MudCalcPro.exe"
