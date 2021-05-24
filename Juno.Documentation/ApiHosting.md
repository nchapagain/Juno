# Juno API Service Hosting
The following documentation describes important details necessary to understand when hosting Juno API services in various
environments (e.g. Azure Web App, Local Machine). The information provided is the result of the learning experience through
which the team went while developing Juno API services.

#### References
* Azure Hosting Recommendations  
  https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/azure-hosting-recommendations-for-asp-net-web-apps  

* ASP.NET Core Web App Hosting Model  
  https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-3.1
  https://docs.microsoft.com/en-us/dotnet/core/deploying/index

* Azure Web App  
  https://docs.microsoft.com/en-us/azure/app-service/overview  
  https://docs.microsoft.com/en-us/azure/app-service/app-service-web-get-started-dotnet  
  https://docs.microsoft.com/en-us/azure/app-service/overview-authentication-authorization
  https://docs.microsoft.com/en-us/azure/app-service/deploy-staging-slots

## Hosting/Debugging on the Local Machine
The following documentation describes how to run the Juno Experiment Execution API host on your local system. The hosting executable uses local environment
variables to determine the specifics of hosting.  Use the following steps to run the API hosted on your local machine.

#### Define Environment Variables
Define the following environment variables on your local system (System or User-level):

* **ASPNETCORE_ENVIRONMENT**  
  This environment variable tells the WebApp host which Juno environment to target (e.g. juno-dev01, juno-prod01).  Typically, you will target the development
  environment. Be very cautious if targeting the production environment. Avoid this if at all possible.

* **ASPNETCORE_ENVIRONMENT_HOST_LOCAL**
  This environment variable instructs the WebApp host to configure HTTP listeners for local hosting.  This enables the developer to access the API using
  'localhost' addresses and simple HTTP protocol without certificate requirements (e.g. http://localhost/api/experiments ).

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

As an example, if you were going to run the Juno Experiments API locally, you would need to install the certificate for the 'juno-dev01-experiments-api-principal'.

## Hosting in Azure Web App
Azure Web App enables the developer to host an API service in the Azure cloud without the overhead of having to manage an
entire VM. The following documentation provides details pertinent to understanding the requirements.

#### Overview
Azure Web App uses Microsoft Internet Information Services (IIS) to host services. When hosting an API service in Azure API App
the API .dll will be hosted alongside IIS. The Azure Web App framework will actually call your 'Program.Main' to run the logic for
your hosted API.

#### Preferred Hosting Model
Juno API services use the 'InProcess' hosting model. This means that Juno API services are hosted in the same process in
which IIS runs (i.e. w3wp.exe) when hosted in an Azure Web App (see references above). The process hosting model is defined 
in the project file for the host using the 'AspNetCoreHostingModel' property:

``` xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net5.0</TargetFramework>
        <AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>
    </PropertyGroup>

    <PropertyGroup>
        <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
        <AssemblyName>Juno.Execution.Api.WebAppHost</AssemblyName>
        <RootNamespace>Juno.Execution.Api.WebAppHost</RootNamespace>
        <PackageId>Juno.Execution.Api.WebAppHost</PackageId>
        <Authors>crcteam@microsoft.com</Authors>
        <AssemblyVersion>0.0.1</AssemblyVersion>
        <Description>Juno experiment execution REST API/service Azure WebApp host executable.</Description>
    </PropertyGroup>

</Project>
```

#### Host/Project Configuration
In order to configure the host for running in-process alongside IIS, there are a few things that have to be configured
in the project itself.  Following the recommended pattern for ASP.NET Core MVC applications, we use a 'Startup' class that
provides the location of all configurations for the hosting environment. This startup/configuration class should be in the
same .dll as the controller/API classes.

<div style="color:#1569C7">
<div style="font-weight:600">A Note on Ports</div>
Azure Web App does not support just any port. In fact it supports only 80 (HTTP) and 443 (HTTPS) TCP ports. Hence when
configuring the host to run in IIS, these are the ports that will be used.  There is nothing much you need to do to
configure this behavior...the ASP.NET Core MVC framework handles it.
</div>

``` csharp
// Using the startup class for the Execution API as an example.
public class ExecutionApiStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Dependencies used by the controller/API classes are added to the 'services'
        // collection here. This enables the ASP.NET Core dependency injection capabilities.
        ExecutionApiSettings settings = new ExecutionApiSettings(this.Configuration);
        services.AddApplicationInsightsTelemetry(settings.AppInsightsInstrumentationKeyForTraces);

        // Add Controller Dependencies.
        ILogger logger = ExecutionApiStartup.CreateLogger(settings);
        IAzureKeyVault keyVaultClient = ExecutionApiStartup.CreateKeyVaultClient(settings);
        IExperimentDataManager dataManager = ExecutionApiStartup.CreateExperimentDataManager(settings, keyVaultClient);

        services.AddSingleton(logger);
        services.AddSingleton(keyVaultClient);
        services.AddSingleton(dataManager);

        // Add ASP.NET Core MVC Dependency Injection Middleware
        services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
    }

    public void Configure(IApplicationBuilder applicationBuilder, IHostingEnvironment hostingEnvironment)
    {
        // Host/hosting configurations are set here. These affect how the host runs.

        // Use ASP.NET Core MVC request routing. Our controllers are MVC controllers, so we need to use
        // the routing semantics expected.
        applicationBuilder.UseMvc();
    }
}
```

To ensure the host integrates with the IIS pipeline, we create a IWebHost instance and enable IIS middleware. The IWebHost
is the culmination of all of the configuration settings into a single host object.

``` csharp
IWebHostBuilder webHostBuilder = new WebHostBuilder();
webHostBuilder.UseConfiguration(configuration);

// When hosting in Azure Web App, we use IIS. The API runs in 'InProcess' mode 
// which means that it runs in the same process as IIS (w3wp.exe). This is the recommended
// hosting model with Azure Web App for optimal performance.
webHostBuilder.UseIIS();
webHostBuilder.UseStartup<ExecutionApiStartup>();

IWebHost apiHost = webHostBuilder.Build();
apiHost.RunAsync();

// or...
// This functionality is wrapped up in the ApiHostFactory in Juno source for
// convenience and reusability.
IWebHost apiHost = ApiHostFactory.CreateWebAppHost<ExecutionApiStartup>(configuration);
apiHost.RunAsync();
```

#### Azure Web App Configuration
Once the host project is configured as shown above, there are a few things that have to be configured on the Azure
Web App resource as well.

  <div style="color:#AA0000">
  <div style="font-weight:600">Important</div>
  All of the steps below should be completed before publishing any Juno API to the Azure Web App. Without these
  steps, the application will NOT startup and run correctly.
  </div>

* **Set Required General Settings**  
  Juno API services are built as .NET Core 2.2 applications and built specifically for x64 architecture. The default
  settings for the Azure API App do not align to this necessarily. When initially creating the Azure API App, you will
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

  * WEBSITE_SWAP_WARMUP_PING_PATH  
    This setting provides a relative path to a page that can be used to warm up the application before the swap happens. The Swagger API UI page
    can be used for this purpose (e.g. /index.html).

  * WEBSITE_SWAP_WARMUP_PING_STATUSES  
    This setting defines a comma-delimited list of HTTP status codes to expect from the page defined above that indicate the site is operational
    and ready for the swap.  The swap will not occur if the ping does not return one of these status codes.  The Swagger API UI page returns a status
    code of 200, so use this as the expected ping status.

  ![](..\Juno.Documentation\Images\AzureWebApp_ApplicationSettings.PNG)

* **Upload Certificate Required for Host to Authenticate with Key Vault**  
  The certificate to access the Key Vault is in the Key Vault itself.  Azure Web App will enable you to upload the
  the certificate directly from the Key Vault.

  ![](..\Juno.Documentation\Images\AzureWebApp_Certificates_KeyVault.PNG)

* **Enable Application Insights**  
  Turn on Application Insights to ensure tracing telemetry for the environment is captured.

  ![](..\Juno.Documentation\Images\AzureWebApp_AppInsights.PNG)

* **Enable AAD Authentication**  
  Juno services require AAD-provided JSON web tokens (JWT) for authentication. The token can be provided for a user from a browser interface by enabling AAD login on the Web App or by
  a piece of client automation by using an ```IRestClient``` that uses the authentication provider that accepts the token. In the REST client scenario, the application will
  need to have authenticated with AAD directly to start with having received a valid JSON web token (JWT). Regardless of which option a user or client application uses to authenticate
  with the API Web App, the Web App is configured the same way.

  * Enable 'App Service Authentication'
  * Set 'Action to take when request not authenticated' to 'Log in with Azure Active Directory'
  * Configure 'Azure Active Directory' Authentication Provider to use the AAD App/Service Principal assigned to the API.
  
    *Note that each API will have a single/specific Service Principal used for authentication with the API in client automated
    programmatic scenarios (e.g. client PowerShell tools).  Use the following URIs for the Issuer Url:*

    Microsoft Tenant:  
    https://sts.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/
  
    AME Tenant:  
    https://sts.windows.net/33e01921-4d64-4f8c-a055-5bdaffd5e33d/

  ![](..\Juno.Documentation\Images\AzureWebApp_AuthenticationSettings.PNG)
  
  *In the image below, the 'Client ID' is the ID of the AAD App/Service Principal.  The 'Issuer Url' is the URL to the Azure AAD*
  *endpoint for the Azure tenant (e.g. Microsoft, AME tenants)*
  ![](..\Juno.Documentation\Images\AzureWebApp_AADSettings.PNG)

  ![](..\Juno.Documentation\Images\AzureWebApp_AADPrincipal.PNG)

  *You can get the AAD endpoint Guid in the 'Endpoints' defined for the AAD App/Service Principal. The URI itself may not match*
  *exactly. Use the URI for the tenant as noted above*
  ![](..\Juno.Documentation\Images\AzureWebApp_AADEndpoint.PNG)

* **Set TLS/SSL Settings**  
  Enable HTTPS only and TLS version 1.2 on the Azure Web App.

  ![](..\Juno.Documentation\Images\AzureWebApp_SSLSettings.PNG)

* **Configure Web App to Use Virtual Network**  
To better secure the Cosmos DB data stores in the Juno system, the data stores are configured to allow connections ONLY from trusted
virtual networks. Thus, the Web Apps must be configured to integrate with any the virtual networks used to access the Cosmos DB. There
is typically a single virtual network per-environment.

  <div style="color:#FF0000">
  <div style="font-weight:600">Warning:</div>
  When you change the Cosmos DB to allow network connections from a virtual network, this will eliminate your ability to access the Data Explorer
  from a browser on your local system.
  </div>

  * Confirm Virtual Network(s) with Access to Cosmos
    
   ![](..\Juno.Documentation\Images\Cosmos_Virtual_Networks.PNG)

  * Configure Web App as Part of the Virtual Network
    
   ![](..\Juno.Documentation\Images\AzureWebApp_Virtual_Network_1.PNG)

   ![](..\Juno.Documentation\Images\AzureWebApp_Virtual_Network_2.PNG)

#### Azure Web App Deployment Slots
The following section covers the creation of **Deployment Slots** for the Web App.  Deployment Slots are used to enable safe deployments to the Web App
that do not impact call traffic (i.e. zero-downtime deployments).

* **Create a Deployment Slot**
Select 'Deployment slots' in the Azure portal for the Web App and add a new slot. Choose the option to clone settings from the production/existing slot.

  ![](..\Juno.Documentation\Images\AzureWebApp_Add_DeploymentSlot.PNG)

  ![](..\Juno.Documentation\Images\AzureWebApp_DeploymentSlot.PNG)