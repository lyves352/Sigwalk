@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================================================
rem Sigwalk one-click build and publish script
rem Usage:
rem   scripts\build.bat                       -> publish win-x64
rem   scripts\build.bat x86                   -> publish win-x86
rem   scripts\build.bat --arch x86            -> publish win-x86
rem   scripts\build.bat --skip-tests          -> skip unit tests
rem   scripts\build.bat --no-zip              -> do not create release ZIP
rem   scripts\build.bat --no-single-file      -> disable single-file publish
rem   scripts\build.bat --help                -> show help
rem
rem Optional environment variables:
rem   SKIP_TESTS=1
rem   SINGLE_FILE=0
rem   NO_ZIP=1
rem ==========================================================================

pushd "%~dp0.." || (
    echo [ERROR] Cannot enter project root.
    exit /b 1
)

rem ---- Default options -----------------------------------------------------
set "SIG_ARCH=x64"

if not defined SKIP_TESTS set "SKIP_TESTS=0"
if not defined NO_ZIP set "NO_ZIP=0"
if not defined SINGLE_FILE set "SINGLE_FILE=1"

rem ---- Parse command-line arguments ---------------------------------------
call :ParseArgs %* || goto :fail

if "!SIG_SHOW_HELP!"=="1" (
    popd
    exit /b 0
)

if /I not "!SIG_ARCH!"=="x64" if /I not "!SIG_ARCH!"=="x86" (
    echo [ERROR] Unsupported architecture "!SIG_ARCH!". Use x64 or x86.
    goto :fail
)

set "SIG_CONFIG=Release"
set "SIG_RID=win-!SIG_ARCH!"
set "SIG_OUTDIR=release\win\!SIG_ARCH!"

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
echo.
echo [INFO] Restoring NuGet packages...
"!SIG_DOTNET!" restore Sigwalk.sln || goto :fail

echo.
echo [INFO] Building solution in !SIG_CONFIG!...
"!SIG_DOTNET!" build Sigwalk.sln -c !SIG_CONFIG! --no-restore || goto :fail

rem ---- Tests ---------------------------------------------------------------
rem Sigwalk uses no workloads. Disabling the workload resolver also avoids an
rem MSB4242 crash on SDK installs that cannot parse global.json.

if /I "!SKIP_TESTS!"=="1" (
    echo.
    echo [INFO] Skipping tests because SKIP_TESTS=1.
) else (
    echo.
    echo [INFO] Running unit tests...
    "!SIG_DOTNET!" test tests\Sigwalk.Tests\Sigwalk.Tests.csproj -c !SIG_CONFIG! --no-build -p:MSBuildEnableWorkloadResolver=false || goto :fail
)

rem ---- Clean previous publish output ---------------------------------------
echo.
echo [INFO] Cleaning previous output: !SIG_OUTDIR!

if exist "!SIG_OUTDIR!" rd /s /q "!SIG_OUTDIR!"

if exist "!SIG_OUTDIR!" (
    echo [ERROR] Failed to clean output directory: !SIG_OUTDIR!
    goto :fail
)

rem ---- Publish -------------------------------------------------------------
set "SIG_SINGLEFILE_ARGS=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true"

if /I "!SINGLE_FILE!"=="0" (
    echo [INFO] Single-file publish disabled.
    set "SIG_SINGLEFILE_ARGS="
)

echo.
echo [INFO] Publishing !SIG_RID! self-contained...

"!SIG_DOTNET!" publish src\Sigwalk\Sigwalk.csproj -c !SIG_CONFIG! -r !SIG_RID! --self-contained true !SIG_SINGLEFILE_ARGS! -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -p:SatelliteResourceLanguages=zh-Hans -o "!SIG_OUTDIR!" || goto :fail

if not exist "!SIG_OUTDIR!\Sigwalk.exe" (
    echo [ERROR] Publish completed but !SIG_OUTDIR!\Sigwalk.exe was not found.
    goto :fail
)

rem ---- Version info --------------------------------------------------------
set "SIG_VERSION=1.0.0"

for /f "delims=" %%v in ('"!SIG_DOTNET!" msbuild src\Sigwalk\Sigwalk.csproj -getProperty:Version -nologo') do set "SIG_VERSION=%%v"

echo.
echo [INFO] Version: !SIG_VERSION!

for %%F in ("!SIG_OUTDIR!\Sigwalk.exe") do (
    echo [INFO] Sigwalk.exe size: %%~zF bytes
)

rem ---- Optional ZIP --------------------------------------------------------
if /I not "!NO_ZIP!"=="1" (
    set "ZIP_PATH=release\Sigwalk-win-!SIG_ARCH!-!SIG_VERSION!.zip"

    echo.
    echo [INFO] Creating ZIP: !ZIP_PATH!

    if exist "!ZIP_PATH!" del /q "!ZIP_PATH!"

    powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '!SIG_OUTDIR!\*' -DestinationPath '!ZIP_PATH!' -Force"

    if errorlevel 1 (
        echo [WARN] ZIP creation failed; the publish output is still available.
    ) else (
        echo [OK] ZIP: !ZIP_PATH!
    )
)

echo.
echo [OK] Build and publish completed.
echo [OK] Architecture: !SIG_ARCH!
echo [OK] Version: !SIG_VERSION!
echo [OK] Output: !SIG_OUTDIR!\Sigwalk.exe
echo [OK] Run it on a Windows machine without .NET Runtime installed.

popd
exit /b 0


rem ==========================================================================
rem Parse command-line arguments
rem ==========================================================================

:ParseArgs
if "%~1"=="" exit /b 0

if /I "%~1"=="--help" goto :ShowHelp
if /I "%~1"=="-h" goto :ShowHelp

rem ---- Architecture --------------------------------------------------------
if /I "%~1"=="--arch" (
    if "%~2"=="" (
        echo [ERROR] --arch requires an argument.
        exit /b 1
    )

    set "SIG_ARCH=%~2"
    shift
    shift
    goto :ParseArgs
)

rem ---- Legacy positional architecture -------------------------------------
if /I "%~1"=="x64" (
    set "SIG_ARCH=x64"
    shift
    goto :ParseArgs
)

if /I "%~1"=="x86" (
    set "SIG_ARCH=x86"
    shift
    goto :ParseArgs
)

rem ---- Skip tests ----------------------------------------------------------
if /I "%~1"=="--skip-tests" (
    set "SKIP_TESTS=1"
    shift
    goto :ParseArgs
)

rem ---- Disable ZIP ---------------------------------------------------------
if /I "%~1"=="--no-zip" (
    set "NO_ZIP=1"
    shift
    goto :ParseArgs
)

rem ---- Disable single-file publish ----------------------------------------
if /I "%~1"=="--no-single-file" (
    set "SINGLE_FILE=0"
    shift
    goto :ParseArgs
)

echo [ERROR] Unknown argument: %~1
exit /b 1


rem ==========================================================================
rem Show help
rem ==========================================================================

:ShowHelp
set "SIG_SHOW_HELP=1"

echo.
echo Sigwalk Build and Publish
echo.
echo Usage:
echo   build.bat [architecture] [options]
echo.
echo Architecture:
echo   x64                    Build win-x64 (default)
echo   x86                    Build win-x86
echo   --arch x64^|x86         Specify target architecture
echo.
echo Options:
echo   --skip-tests           Skip unit tests
echo   --no-zip               Do not create release ZIP
echo   --no-single-file       Disable single-file publish
echo   -h, --help             Show this help
echo.
echo Environment variables:
echo   SKIP_TESTS=1           Skip unit tests
echo   NO_ZIP=1               Disable ZIP creation
echo   SINGLE_FILE=0          Disable single-file publish
echo.
echo Examples:
echo   build.bat
echo   build.bat x86
echo   build.bat --arch x64 --skip-tests
echo   build.bat x86 --no-zip
echo   build.bat --no-single-file --skip-tests

exit /b 0


rem ==========================================================================
rem Error handling
rem ==========================================================================

:fail
echo.
echo [ERROR] Build failed.
popd
exit /b 1