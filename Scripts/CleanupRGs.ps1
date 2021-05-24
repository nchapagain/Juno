<#
.SYNOPSIS
Delete Resource Groups

.DESCRIPTION
This script will delete all resource group for the subscription that has the "experimentId" tag. 
Later, we should add a createdOn time tag and run the script only for resources that are X days old.

.PARAMETER SubscriptionId
The subscriptionId to cleanup

.PARAMETER ExperimentIds
Optional set of specific experiment IDs for which the target resource groups to delete are associated (e.g. by tags).

.PARAMETER ResourceGroups
Optional set of specific resource groups to delete.

.NOTES
Prerequisites: Azure powershell

Install Azure powershell here: https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-3.8.0

You must run Login-AzAccount in the current PowerShell session before running this script.
#>
param (
    [Parameter(Mandatory=$true)]
    [Alias("subscription")]
    [string] $SubscriptionId,

    [Parameter(Mandatory=$false)]
    [Alias("experiments")]
    [string[]] $ExperimentIds,

    [Parameter(Mandatory=$false)]
    [Alias("groups")]
    [string[]] $ResourceGroups
)

#
# Will queue up a series of jobs to allow deletes to execute in parallel.
#
Function DeleteInParallel(
    [Microsoft.Azure.Commands.Profile.Models.Core.PSAzureContext]$context,
    [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup] $resourceGroup)
{
    $parallelJobCount = 25

    $waiting = $false
    while ($true)
    {
        # check existing jobs to see whats already running
        $jobs = Get-Job -State "Running"
        if (!$jobs)
        {
            break
        }
        if ($jobs.ChildJobs.Count -lt $parallelJobCount)
        {
            # we have room to schedule more jobs
            break
        }
        # wait for existing jobs to complete
        if (!$waiting)
        {
            Write-Verbose "Waiting for max $($jobs.ChildJobs.Count) jobs to complete."
        }
        $waiting = $true
        start-sleep 5
    }

    $ScriptBlock = {
        # Pass the context so that we don't have to login inside each job.
        param($context, $resourceGroup)
        
        Remove-AzResourceGroup -Name $resourceGroup.ResourceGroupName -AzContext $context -Force
        Write-Host $resourceGroup
    }

    Write-Progress "Deleting'$($resourceGroup.ResourceGroupName)'..."
    $null = Start-Job $ScriptBlock -ArgumentList @($context, $resourceGroup) -Verbose
}

#
# Wait for parallel delete jobs to finish and print out the results of those jobs.
#
Function WaitForDeleteJobs
{
    While ($true)
    {
        $jobs = Get-Job -State "Running"
        if ($jobs)
        {
            $count = $jobs.ChildJobs.Count
            Write-Verbose "Waiting for $count jobs to complete"
            Start-Sleep 15
        }
        else 
        {
            break
        }
    }

    # This prints out the result of each job
    Get-Job | Receive-Job
}

# Check if sign in already
$azContext = Get-AzContext;
if ([string]::IsNullOrEmpty($azContext.Account)) 
{
   # sign in
   Write-Verbose "Logging in...";
   Login-AzAccount;
   $azContext = Get-AzContext;
}

Select-AzSubscription -SubscriptionId $SubscriptionId
$subscriptionResourceGroups = Get-AzResourceGroup
$resourceGroupsToDelete = @();

if ($ExperimentIds -and $ResourceGroups)
{
    throw "Invalid usage. Defining a set of experiment IDs and resource group names is not a supported scenario."
}

foreach ($subscriptionResourceGroup in $subscriptionResourceGroups)
{
    # this was part of juno experiment. once we have the created date, we can be smarter but for now
    # we delete anything that was part of Juno experiments
    $experimentIdTag = "experimentId";
    if($subscriptionResourceGroup.Tags -and $subscriptionResourceGroup.Tags.Contains($experimentIdTag))
    {
        if ($ResourceGroups)
        {
            if (-not ($ResourceGroups | %{ $_.ToLower() }).Contains($subscriptionResourceGroup.ResourceGroupName.ToLower()))
            {
                continue;
            }
        }
        elseif ($ExperimentIds)
        {
            $experimentId = $subscriptionResourceGroup.Tags[$experimentIdTag].ToLower().Trim();
            $experimentIdMatches = ($ExperimentIds | %{ $_.ToLower().Trim() }).Contains($experimentId)

            if (-not $experimentIdMatches)
            {
                continue;
            }
        }

        $resourceGroupsToDelete += $subscriptionResourceGroup;
    }
}

Write-Verbose "[Resource Groups to Delete]:"
$resourceGroupsToDelete | %{ Write-Verbose $_.ResourceGroupName }

foreach ($resourceToDelete in $resourceGroupsToDelete)
{
    DeleteInParallel -context $azContext -resourceGroup $resourceToDelete
}

WaitForDeleteJobs
