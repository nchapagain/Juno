namespace Juno.EnvironmentSelection.Api.WebAppHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;
    using Juno.Contracts.Configuration;
    using Juno.EnvironmentSelection.Service;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.AspNetCore.Swagger;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerUI;

    /// <summary>
    /// Defines the startup settings and requirements for the Juno Environments
    /// API useing ASP.NET Core.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The configuration for the API service(s).</param>
        public Startup(IConfiguration configuration)
        {
            configuration.ThrowIfNull(nameof(configuration));
            this.Configuration = configuration;
        }

        /// <summary>
        /// Gets the configuration settings.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// ASP.NET Core startup method for configuring dependency services used by the 
        /// Juno Environments API service.
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.Configuration);
            services.AddApplicationInsightsTelemetry(settings.AppInsightsSettings.Get(Setting.Tracing).InstrumentationKey);
            AadPrincipalSettings executionApiPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.EnvironmentsApi); 
            ILogger logger = Program.Logger;
            IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(executionApiPrincipal, settings.KeyVaultSettings.Get(Setting.Default));

            string assemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Startup)).Location);
            IEnumerable<IAccountable> accountableFilters = EnvironmentSelectionProviderFactory.CreateAccountableInstances(assemblyPath, services, this.Configuration, logger);
            services.AddSingleton<ILogger>(logger);
            services.AddSingleton<IAzureKeyVault>(keyVaultClient);
            services.AddSingleton<IEnumerable<IAccountable>>(accountableFilters);

            IEnvironmentSelectionService environmentSelectionService = new EnvironmentSelectionService(services, this.Configuration, logger);
            services.AddSingleton<IEnvironmentSelectionService>(environmentSelectionService);

            // Add OpenAPI/Swagger definition.
            // https://docs.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-3.1&tabs=visual-studio
            // https://github.com/domaindrivendev/Swashbuckle.AspNetCore
            // https://idratherbewriting.com/learnapidoc
            services.AddSwaggerGen(doc =>
            {
                doc.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Juno Environment Selection API",
                    Description = "Juno Environment Selection REST API/service.",
                    Contact = new OpenApiContact()
                    {
                        Name = "Cloud Readiness Criteria Team",
                        Email = "crcair@microsoft.com",
                        Url = new Uri("https://msazure.visualstudio.com/One/_wiki/wikis/One.wiki/31949/Cloud-Readiness-Criteria-(CRC)")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "API License",
                        Url = new Uri("https://msazure.visualstudio.com/One/_git/CSI-CRC-AIR?path=%2FLICENSE")
                    },
                });

                // All JSON input and output objects are expected to be in camel-case.
                doc.DescribeAllParametersInCamelCase();
                doc.SchemaFilter<SchemaExamplesFilter>();

                // Set the comments path for the Swagger JSON and UI.
                string xmlFile = $"{Assembly.GetAssembly(typeof(EnvironmentSelectionController)).GetName().Name}.xml"; // Change this
                doc.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
            });

            // Add ASP.NET Core MVC Dependency Injection Middleware
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1
            services.AddMvc(options =>
            {
                // Certain API methods have multiple usage scenarios and allow HTTP body parameters to be null.
                options.AllowEmptyInputInBodyModelBinding = true;

                options.EnableEndpointRouting = false;

                // ASP.NET Core  3.0 introduced a change to the way it handles 'Async' suffixes when using
                // CreatedAtAction() and CreatedAtRoute() methods.
                // https://github.com/microsoft/aspnet-api-versioning/issues/558
                options.SuppressAsyncSuffixInActionNames = false;
            })
            .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
            .AddNewtonsoftJson();
        }

        /// <summary>
        /// ASP.NET Core startup method for configuring the application hosting environment for the
        /// Juno Environment API service.
        /// </summary>
        /// <param name="applicationBuilder">Provides context required to configure the application.</param>
        /// <param name="hostingEnvironment">Provides information about the hosting environment.</param>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Required signature for ASP.NET Core startup class definitions.")]
        [SuppressMessage("Microsoft.Usage", "CA1801: Review unused parameters", Justification = "Required signature for ASP.NET Core startup class definitions.")]
        public void Configure(IApplicationBuilder applicationBuilder, IWebHostEnvironment hostingEnvironment)
        {
            // Enable middleware to serve generated Swagger as a JSON endpoint.
            applicationBuilder.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            applicationBuilder.UseSwaggerUI(doc =>
            {
                doc.SwaggerEndpoint("/swagger/v1/swagger.json", "Juno Experiment Execution API (v1)");

                // "Try it Out" only for GET methods.
                doc.SupportedSubmitMethods(SubmitMethod.Get);
                doc.RoutePrefix = string.Empty;
                doc.ConfigObject.DefaultModelRendering = ModelRendering.Example;
            });

            applicationBuilder.UseMiddleware<ApiExceptionMiddleware>();
            applicationBuilder.UseMvc();
        }
    }
}
