namespace Juno.Hosting.Common
{
    using System;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Provides a factory for the creation of hosts used by Juno ASP.NET Core 
    /// API services.
    /// </summary>
    public static class HostFactory
    {
        /// <summary>
        /// Creates a <see cref="IWebHost"/> instance to host the Juno API services referenced
        /// by the startup instance on the local machine. The ASP.NET Kestrel server is used.
        /// </summary>
        /// <param name="configuration">Provides configuration settings to the host.</param>
        /// <param name="port">The port for which HTTP listeners will be setup.</param>
        public static IWebHost CreateLocalHost<TStartup>(IConfiguration configuration, int port = 5000)
            where TStartup : class
        { 
            IWebHostBuilder webHostBuilder = new WebHostBuilder();
            webHostBuilder.UseConfiguration(configuration);

            // When hosting locally, we use the Kestrel HTTP server.
            webHostBuilder.UseKestrel(options =>
            {
                options.ListenLocalhost(port);
            });

            webHostBuilder.UseStartup<TStartup>();

            return webHostBuilder.Build();
        }

        /// <summary>
        /// Creates a <see cref="IWebHost"/> instance to host the Juno API services referenced
        /// by the startup instance within an Azure Web App. The IIS server is used. Note that the
        /// API is expected to be configured for the 'InProcess' hosting model.
        /// </summary>
        /// <param name="configuration">Provides configuration settings to the host.</param>
        public static IWebHost CreateWebAppHost<TStartup>(IConfiguration configuration)
            where TStartup : class
        {
            IWebHostBuilder webHostBuilder = new WebHostBuilder();
            webHostBuilder.UseConfiguration(configuration);

            // When hosting in Azure Web App, we use IIS. The API runs in 'InProcess' mode 
            // which means that it runs in the same process as IIS (w3wp.exe). This is the recommended
            // hosting model with Azure Web App for optimal performance.
            webHostBuilder.UseIIS();
            webHostBuilder.UseStartup<TStartup>();

            return webHostBuilder.Build();
        }

        /// <summary>
        /// Returns the name of the deployment slot in which the Web App is running (e.g. production, staging).
        /// </summary>
        public static string GetDeploymentSlot()
        {
            // The staging slot has a specific environment variable that is used to denote
            // that the service is in the 'staging' slot.  When running in the 'staging' slot, the
            // service is expected to run idle and not to process any queued experiments.
            return Environment.GetEnvironmentVariable("DEPLOYMENT_SLOT", EnvironmentVariableTarget.Process);
        }
    }
}
