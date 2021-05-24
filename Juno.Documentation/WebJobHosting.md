# Juno webjob/runtime Service Hosting
The following documentation describes important details necessary to understand when hosting Juno webjob/(execution management) services in various
environments (e.g. Azure Web App, Local Machine). The information provided is the result of the learning experience through
which the team went while developing Juno webjob services.

#### References

* Run background tasks with WebJobs in Azure App Service
  * https://docs.microsoft.com/en-us/azure/app-service/webjobs-create
* Develop and deploy WebJobs using Visual Studio - Azure App Service
  * https://docs.microsoft.com/en-us/azure/app-service/webjobs-dotnet-deploy-vs
* Get started with the Azure WebJobs SDK for event-driven background processing
  * https://docs.microsoft.com/en-us/azure/app-service/webjobs-sdk-get-started
* How to use the Azure WebJobs SDK for event-driven background processing
  * https://docs.microsoft.com/en-us/azure/app-service/webjobs-sdk-how-to
* WebJob Configurations
  * https://github.com/projectkudu/kudu/wiki/WebJobs

## Hosting/Debugging on the Local Machine
The following documentation describes how to run the Juno runtime execution service host on your local system. The hosting executable uses local environment
variables to determine the specifics of hosting.  Use the following steps to run the API hosted on your local machine.

#### Define Environment Variables
Define the following environment variables on your local system (System or User-level):

* **ASPNETCORE_ENVIRONMENT**  
  This environment variable tells the WebApp host which Juno environment to target (e.g. juno-dev01, juno-prod01).  Typically, you will target the development
  environment. Be very cautious if targeting the production environment. Avoid this if at all possible.

*(Example: defining the variables at the user or system/global-level)*
![](..\Juno.Documentation\Images\AspNetCore_Hosting_Environment_Variables.PNG)

*(Example: defining the variables at the individual project/process-level. Note that the Profile should be set to the project and not to IISExpress etc...)*
![](..\Juno.Documentation\Images\AspNetCore_Hosting_Debug_Settings.PNG)

#### Install Required Certificates
All API services and Azure resources (e.g. Key Vault) in the Juno system use Azure Active Directory (AAD) service principals for identity and
role-based access control. When you are running a host (e.g. API, Execution Service, Scheduler Service) locally, you will need to have the required
certificates installed. Each API or Service uses its own individual AAD service principal for identity and that identity is associated with a
unique certificate (1-to-1 mapping).

All certificates can be found in a Key Vault within the environment (e.g. junodev01vault, junoprod01vault). The certificates for each API or Service
can be downloaded and installed from the Key Vault (note that you will need to install the PFX certificate that contains the private key). The AAD services
principals used for each API or Service in the environment(s) and links to the Key Vaults can be found at the following Wiki location:

https://msazure.visualstudio.com/One/_wiki/wikis/One.wiki/48593/Juno-Environments

As an example, if you are going to run the Juno Execution Service locally targeting the juno-dev01 environment, you would need to install the
certificate for the 'juno-dev01-executionsvc-principal'.

## Hosting in Azure Web App
Azure Web App enables the developer to host a Web Job in the Azure cloud without the overhead of having to manage an
entire VM. The following documentation provides details pertinent to understanding the requirements.

#### Overview
When publishing the webjob from visual studio to azure app service, the pubisher will create a batch file with run.cmd, the content of run.cmd is just 
a call to launch the webjob.dll
Example:
* dotnet Juno.Execution.Management.WebJobHost.dll 

#### Host/Project Configuration
In order to configure the host for running in webjob, there are a few things that have to be configured
in the project itself. The host is the runtime container for functions that listens for triggers and calls functions. 
The following steps create a host that implements IHost, which is the Generic Host in ASP.NET Core.

<div style="color:#1569C7">
<div style="font-weight:600">A Note on type of webjobs</div>
There are two type of webjobs continuous and triggered. Currently we are doing continues webjob where our function invoked when the host start 
and we have infinite loop. If we exit by any chance Azure app service will try to restrart the job after 60 seconds. 

<table>
<tr> 
<td>Continuous </td> 
<td>Triggered</td> 
</tr>
<tr>
<td>Starts immediately when the WebJob is created. To keep the job from ending, the program or script typically does its work inside an endless loop. If the job does end, you can restart it.</td>
<td>Starts only when triggered manually or on a schedule.</td>
</tr>
<tr>
<td>Runs on all instances that the web app runs on. You can optionally restrict the WebJob to a single instance.</td>
<td>Runs on a single instance that Azure selects for load balancing.</td>
</tr>
</table>

</div>

* The main method in the Program.cs will contain host configuration like below
``` csharp
        var builder = new HostBuilder();
        builder.ConfigureWebJobs(b =>
        {
            b.AddAzureStorageCoreServices();
        });

        builder.ConfigureLogging((context, b) =>
        {
            b.AddConsole();
            b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = settings.AppInsightsInstrumentationKeyForTraces);
        });

        var telemetryLogger = StartupFactory.CreateLogger(settings.AppInsightsInstrumentationKeyForTelemetry, Constants.ExecutionManagementHost);

        //// Depedency injection registration.
        builder.ConfigureServices(s => s.AddSingleton<ExecutionManagementSettings>(settings));
        builder.ConfigureServices(s => s.AddSingleton<ILogger>(telemetryLogger));

        //// IConfiguration depedency injection is avaiable by default
        ////builder.ConfigureServices(s => s.AddSingleton<IConfiguration>(configuration));

        using (IHost host = builder.Build())
        {
            var jobHost = host.Services.GetService(typeof(IJobHost)) as JobHost;
            host.StartAsync().Wait();

            //// Start the job when the host start for continues running job. This is also called manual triggered.
            jobHost.CallAsync("ExecuteAsync", tokenSource.Token).Wait(tokenSource.Token);
            host.StopAsync().Wait();
        }
```

#### Azure Web App Configuration
Once the host project is configured as shown above, there are a few things that have to be configured on the Azure
Web App resource as well.

  <div style="color:#AA0000">
  <div style="font-weight:600">Important</div>
  All of the steps below should be completed before publishing any Juno webjob to the Azure Web App. Without these
  steps, the application will NOT startup and run correctly.
  </div>

* **Set Required General Settings**  
  Juno webjob runtime services are built as .NET Core 2.2 applications and built specifically for x64 architecture. The default
  settings for the Azure app service do not align to this necessarily. When initially creating the Azure app service, you will
  be able to define the target .NET Core framework.  However, to be safe, ensure the following settings match:

  * Stack = .NET Core
  * Platform = 64 Bit
  * Managed pipeline version = Integrated
  * Always on = On
  ![](..\Juno.Documentation\Images\AzureWebApp_GeneralSettings.PNG)

* **Set Required Application Settings**
  In order for the host to run correctly, the following application settings must be set on the Azure Web App:

  * APPINSIGHTS_INSTRUMENTATIONKEY  
    This setting provides a reference to the target Application Insights endpoint where general request and diagnostic
    traces will be written for the entire hosting environment. Juno API services use 2 different Application Insights
    stores for telemetry and tracing data. This is considered tracing data and is useful for diagnosing any issues related
    to the host environment separate from structured telemetry data used to drive Juno experiment live site and insights.
    Set this to the instrumentation key for the 'tracing' endpoint (e.g. Application Insights -> junodev01tracing).
    
  * ASPNETCORE_ENVIRONMENT  
    This setting/environment variable indicates to the API host which environment it should interact with (e.g. juno-dev01).
    In short, the host will load the settings it needs from the appropriate *.environmentsettings.json file for this environment.

  * WEBSITE_LOAD_CERTIFICATES  
    This setting enables the API host to access certificates imported/uploaded to the Azure Web App (i.e. the local certificate store).

  ![](..\Juno.Documentation\Images\AzureWebApp_ApplicationSettings.PNG)

* **Upload Certificate Required for Host to Authenticate with execution api**  
  The certificate to access the execution api is in the Key Vault.  Azure Web App will enable you to upload the
  the certificate directly from the Key Vault.

  ![](..\Juno.Documentation\Images\AzureWebApp_Certificates_KeyVault.PNG)

* **Enable Application Insights**  
  Turn on Application Insights to ensure tracing telemetry for the environment is captured.

  ![](..\Juno.Documentation\Images\AzureWebApp_AppInsights.PNG)

* **Deploying from visual studio**  

  <div style="color:#AA0000">
  <div style="font-weight:600">Important</div>
  Visual studio 2019 is required. Visual studio 2017 seems to have a bug when authenticating with nuget feed.
  </div>

* Configure the visual sudio publish setting as below example 
   ![](..\Juno.Documentation\Images\WebJobPublishFromVisualStudio.PNG)

#### Azure Web App Deployment Slots
The following section covers the creation of **Deployment Slots** for the Web App/Job.  Deployment Slots are used to enable safe deployments to the Web Job
that do not impact call traffic (i.e. zero-downtime deployments).

* **Create a Deployment Slot**
Select 'Deployment slots' in the Azure portal for the Web App and add a new slot. Choose the option to clone settings from the production/existing slot.

  ![](..\Juno.Documentation\Images\AzureWebApp_Add_DeploymentSlot.PNG)

  ![](..\Juno.Documentation\Images\AzureWebApp_DeploymentSlot.PNG)

* **Add Required Application Settings**  
The following settings are required to ensure the Web Job can distinguish when it is running in the 'staging' slot. During that time, it is expected to
be running idle and NOT processing any experiments.

  * DEPLOYMENT_STAGING_SLOT  
  Application setting is used to tell the Web Job that it is running in a 'staging' slot and should run idle.  This setting MUST be marked as a 'Deployent slot' setting
  when it is created.  Note that this is a custom setting used by Juno execution hosts. In order for this to function correctly, this setting must be
  referenced in the host and the expectations of running idle handled.
