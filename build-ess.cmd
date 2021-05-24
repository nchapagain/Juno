@echo Off
REM This script is used to build Juno Environment Selection Service - more specifically with the installer package to
REM be used by Ev2 deployment. This script is used as part of the official build process. 
REM Additionally, users can run this script on the desktop in order to generate packages as needed.

Set ExitCode=0

Echo [Building Juno Environment Selection Service]
echo --------------------------------------------------
call dotnet build "%~dp0Juno.EnvironmentSelection.WebAppHost\Juno.EnvironmentSelection.Api.WebAppHost.csproj" -r win-x64 -c Debug /t:CICDDeployment && echo: || Goto :Error
Goto :Finish

:Error
set ExitCode=%ERRORLEVEL%

:Finish
echo Juno Environment Selection Service Build Exit Code: %ExitCode%
exit /B %ExitCode%