@echo Off

if /i "%~1" == "" Goto :Usage
if /i "%~1" == "/?" Goto :Usage
if /i "%~1" == "-?" Goto :Usage
if /i "%~1" == "--help" Goto :Usage

set PackageVersion=%~1

REM This operation uses a custom target defined in the Juno.HostAgent.csproj file that defines the specific OS flavors to 
REM build and additionally creates a NuGet package containing each of the flavors in separate directories.
Echo [Packaging Juno Host Agent]
echo --------------------------------------------------
call dotnet pack %~dp0Juno.HostAgent\Juno.HostAgent.csproj -c Debug -p:Version=%PackageVersion% -p:GeneratePackageOnBuild=true && echo: || Goto :Error
Goto :End

:Usage
echo Invalid Usage. The package version must be provided on the command line
echo:
echo Usage:
echo %~0 {packageVersion}
echo:
echo Examples:
echo %~0 1.2.3
Goto :End

:Error
set ExitCode=%ERRORLEVEL%

:End
rem Reset environment variables
set PackageVersion=

exit /B %ERRORLEVEL%