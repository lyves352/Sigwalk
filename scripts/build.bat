@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================================================
rem Sigwalk one-click build and publish script
rem Usage:
rem   scripts\build.bat            -> publish win-x64
rem   scripts\build.bat x86        -> publish win-x86
rem Optional environment variables:
rem   SKIP_TESTS=1                 -> skip unit tests
rem   SINGLE_FILE=0                -> disable single-file publish
rem   NO_ZIP=1                     -> do not create release ZIP
rem ==========================================================================

pushd "%~dp0.." || (echo [ERROR] Cannot enter project root. & exit /b 1)

set "SIG_ARCH=%~1"
if "%SIG_ARCH%"=="" set "SIG_ARCH=x64"
if /I not "%SIG_ARCH%"=="x64" if /I not "%SIG_ARCH%"=="x86" (
  echo [ERROR] Unsupported architecture "%SIG_ARCH%". Use x64 or x86.
  goto :fail
)

set "SIG_CONFIG=Release"
set "SIG_RID=win-%SIG_ARCH%"
set "SIG_OUTDIR=release\win\%SIG_ARCH%"

rem ---- Locate .NET SDK -----------------------------------------------------
where dotnet >nul 2>nul
if not errorlevel 1 (
  set "SIG_DOTNET=dotnet"
) else (
  if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "SIG_DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
  ) else if exist "%ProgramFiles(x86)%\dotnet\dotnet.exe" (
    set "SIG_DOTNET=%ProgramFiles(x86)%\dotnet\dotnet.exe"
  ) else (
    echo [ERROR] .NET SDK not found. Install .NET 8 SDK or add dotnet to PATH.
    goto :fail
  )
)

echo [INFO] Using dotnet: !SIG_DOTNET!
"!SIG_DOTNET!" --version
if errorlevel 1 (
  echo [ERROR] dotnet --version failed.
  goto :fail
)

set "SDKMAJOR="
for /f "tokens=1 delims=." %%v in ('"!SIG_DOTNET!" --version') do set "SDKMAJOR=%%v"
if not defined SDKMAJOR (
  echo [ERROR] Cannot determine .NET SDK version.
  goto :fail
)
if !SDKMAJOR! LSS 8 (
  echo [ERROR] .NET SDK 8.0 or later is required. Current major version: !SDKMAJOR!
  goto :fail
)

rem ---- Restore, build ------------------------------------------------------
echo [INFO] Restoring NuGet packages...
"!SIG_DOTNET!" restore Sigwalk.sln || goto :fail

echo [INFO] Building solution in %SIG_CONFIG%...
"!SIG_DOTNET!" build Sigwalk.sln -c %SIG_CONFIG% --no-restore || goto :fail

rem ---- Tests ---------------------------------------------------------------
if /I "%SKIP_TESTS%"=="1" (
  echo [INFO] Skipping tests because SKIP_TESTS=1.
) else (
  echo [INFO] Running unit tests...
  "!SIG_DOTNET!" test tests\Sigwalk.Tests\Sigwalk.Tests.csproj -c %SIG_CONFIG% --no-build || goto :fail
)

rem ---- Clean previous publish output (release folder only) -----------------
echo [INFO] Cleaning previous output: %SIG_OUTDIR%
if exist "%SIG_OUTDIR%" rd /s /q "%SIG_OUTDIR%"
if exist "%SIG_OUTDIR%" (
  echo [ERROR] Failed to clean output directory: %SIG_OUTDIR%
  goto :fail
)

rem ---- Publish -------------------------------------------------------------
set "SIG_SINGLEFILE_ARGS=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true"
if /I "%SINGLE_FILE%"=="0" (
  echo [INFO] Single-file publish disabled because SINGLE_FILE=0.
  set "SIG_SINGLEFILE_ARGS="
)

echo [INFO] Publishing %SIG_RID% self-contained...
"!SIG_DOTNET!" publish src\Sigwalk\Sigwalk.csproj -c %SIG_CONFIG% -r %SIG_RID% --self-contained true !SIG_SINGLEFILE_ARGS! -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -p:SatelliteResourceLanguages=zh-Hans -o "%SIG_OUTDIR%" || goto :fail

if not exist "%SIG_OUTDIR%\Sigwalk.exe" (
  echo [ERROR] Publish completed but %SIG_OUTDIR%\Sigwalk.exe was not found.
  goto :fail
)

rem ---- Version info --------------------------------------------------------
set "SIG_VERSION=1.0.0"
for /f "delims=" %%v in ('"!SIG_DOTNET!" msbuild src\Sigwalk\Sigwalk.csproj -getProperty:Version -nologo') do set "SIG_VERSION=%%v"

echo [INFO] Version: !SIG_VERSION!
for %%F in ("%SIG_OUTDIR%\Sigwalk.exe") do echo [INFO] Sigwalk.exe size: %%~zF bytes

rem ---- Optional ZIP --------------------------------------------------------
if /I not "%NO_ZIP%"=="1" (
  set "ZIP_PATH=release\Sigwalk-win-%SIG_ARCH%-!SIG_VERSION!.zip"
  echo [INFO] Creating ZIP: !ZIP_PATH!
  if exist "!ZIP_PATH!" del /q "!ZIP_PATH!"
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%SIG_OUTDIR%\*' -DestinationPath '!ZIP_PATH!' -Force"
  if errorlevel 1 (
    echo [WARN] ZIP creation failed; the publish output is still available.
  ) else (
    echo [OK] ZIP: !ZIP_PATH!
  )
)

echo.
echo [OK] Build and publish completed.
echo [OK] Output: %SIG_OUTDIR%\Sigwalk.exe
echo [OK] Run it on a Windows machine without .NET Runtime installed.
popd
exit /b 0

:fail
echo.
echo [ERROR] Build failed.
popd
exit /b 1
