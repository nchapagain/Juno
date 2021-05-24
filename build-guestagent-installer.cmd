@echo Off
REM This script is used to build Juno agents that need to target multiple operating systems
REM (e.g. Windows, Linux). It is time-consuming to build the agent packages, so we do not build
REM them as part of the typical desktop/user build. This script is used as part of the official
REM build process. Additionally, users can run this script on the desktop in order to generate
REM packages as needed.

Set ExitCode=0

REM This operation uses a custom target defined in the Juno.GuestAgent.Installer.csproj file that defines the specific OS flavors.
Echo [Building Juno Guest Agent Installer]
echo --------------------------------------------------
call dotnet publish "%~dp0Juno.GuestAgent.Installer\Juno.GuestAgent.Installer.csproj" -r linux-x64 -c Debug -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimUnusedDependencies=true && echo: || Goto :Error
call dotnet publish "%~dp0Juno.GuestAgent.Installer\Juno.GuestAgent.Installer.csproj" -r linux-arm64 -c Debug -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimUnusedDependencies=true && echo: || Goto :Error
call dotnet publish "%~dp0Juno.GuestAgent.Installer\Juno.GuestAgent.Installer.csproj" -r win-x64 -c Debug -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimUnusedDependencies=true && echo: || Goto :Error

rem Note: There is currently no support for SingleFile/win-arm64. This is here to be ready for when support
rem is added in the future.
rem call dotnet publish "%~dp0Juno.GuestAgent.Installer\Juno.GuestAgent.Installer.csproj" -r win-arm64 -c Debug -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimUnusedDependencies=true && echo: || Goto :Error
Goto :Finish

:Error
set ExitCode=%ERRORLEVEL%

:Finish
echo Juno Guest Agent Installer Build Exit Code: %ExitCode%
exit /B %ExitCode%