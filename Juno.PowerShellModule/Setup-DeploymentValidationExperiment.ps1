# #############################################################################
# Powershell script to initiate a Deployment Validation experiment in Juno 
# by passing in the target URL and the Override file path that contains 
# the paremeters required by the script.
# TargetUrl can have the following two values
# https://junodev01experiments.azurewebsites.net (Dev Environment)
# https://junoprod01experiments.azurewebsites.net (Prod Environment)
# #############################################################################
param($templateOverWriteFile)
param($targetUrl)
Get-AccessToken -ServiceUri $targetUrl
Start-Experiment -ExecutionGoalTemplateId MCU2020.ExecutionGoalTemplate.v1.json -OverrideFile $templateOverWriteFile

