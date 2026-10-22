@echo off
setlocal disabledelayedexpansion

rem ======================================================================
rem  IncrementBuildNumber.bat
rem
rem  Increments the 4th number (build) of the AssemblyVersion attribute
rem  in an AssemblyInfo.cs file, e.g. 2.0.1.1040 becomes 2.0.1.1041.
rem
rem  Usage:
rem      increment_version.bat ASSEMBLYINFO_PATH CONFIGURATION
rem
rem  Arguments:
rem      %1  ASSEMBLYINFO_PATH  (required)
rem          Path to the AssemblyInfo.cs file. May be absolute or relative.
rem          A relative path is resolved against the current working
rem          directory, not the folder this .bat is in.
rem          Quote it if it contains spaces.
rem          Example: "..\Properties\AssemblyInfo.cs"
rem
rem      %2  CONFIGURATION  (required in practice)
rem          Build configuration name, normally $(Configuration) from MSBuild.
rem          The build number is incremented only if this is "Release"
rem          (case-insensitive). Any other value, or a missing value,
rem          leaves the file untouched and exits with code 0.
rem
rem  Exit codes:
rem      0  success, or nothing to do because the configuration is not Release
rem      1  error: missing argument, file not found, AssemblyVersion line
rem         not found, 4th number not numeric, or the file could not be replaced
rem
rem  Notes:
rem      Only an uncommented line starting with [assembly: AssemblyVersion(
rem      is changed. Commented-out lines are ignored.
rem ======================================================================

if "%~1"=="" goto :usage

rem Only increment when the second argument is "release" (case-insensitive)
if /i not "%~2"=="release" (
    echo IncrementBuildNumber.bat: Configuration "%~2" is not Release, build number not incremented.
    exit /b 0
)

rem Resolve the argument (relative or absolute) to a full path
set "AIFILE=%~f1"
set "TMPFILE=%AIFILE%.tmp"

if not exist "%AIFILE%" goto :nofile

rem --- Pass 1: find the AssemblyVersion line (commented-out lines don't match)
set "LN="
set "OLDLINE="
for /f "tokens=1* delims=:" %%A in ('findstr /n /r /c:"^[ ]*\[assembly:[ ]*AssemblyVersion(" "%AIFILE%"') do (
    if not defined LN (
        set "LN=%%A"
        set "OLDLINE=%%B"
    )
)
if not defined LN goto :noversion

rem --- Split "3.0.1.1582" into its four parts
set "V=%OLDLINE:*AssemblyVersion("=%"
set "V=%V:"=#%"
for /f "tokens=1-4 delims=.#" %%a in ("%V%") do (
    set "VA=%%a"
    set "VB=%%b"
    set "VC=%%c"
    set "VD=%%d"
)
echo %VD%|findstr /r "^[0-9][0-9]*$" >nul || goto :badbuild

set /a NEWBUILD=VD+1
set "OLDVER=%VA%.%VB%.%VC%.%VD%"
set "NEWVER=%VA%.%VB%.%VC%.%NEWBUILD%"
call set "NEWLINE=%%OLDLINE:%OLDVER%=%NEWVER%%%"

rem --- Pass 2: copy every line to a temp file, substituting the new version line
(for /f "tokens=1* delims=:" %%A in ('findstr /n "^" "%AIFILE%"') do (
    if "%%A"=="%LN%" (call :emit_new) else (echo(%%B)
)) >"%TMPFILE%"

move /y "%TMPFILE%" "%AIFILE%" >nul || goto :movefail

echo Version: %OLDVER% -^> %NEWVER%
endlocal
exit /b 0

:emit_new
echo(%NEWLINE%
exit /b 0

:usage
echo Usage: %~nx0 path\to\AssemblyInfo.cs Configuration
echo   The build number is incremented only if Configuration is "Release".
exit /b 1

:nofile
echo ERROR: file not found: %AIFILE%
exit /b 1

:noversion
echo ERROR: AssemblyVersion line not found in %AIFILE%
exit /b 1

:badbuild
echo ERROR: 4th number of AssemblyVersion is missing or not numeric
exit /b 1

:movefail
echo ERROR: could not replace %AIFILE% (read-only or locked?)
del "%TMPFILE%" >nul 2>&1
exit /b 1