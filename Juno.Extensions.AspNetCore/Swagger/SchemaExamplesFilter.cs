namespace Juno.Extensions.AspNetCore.Swagger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.OpenApi.Any;
    using Microsoft.OpenApi.Models;
    using Newtonsoft.Json;
    using Swashbuckle.AspNetCore.SwaggerGen;

    /// <summary>
    /// Provides a OpenAPI/Swagger schema filter for data contract/model objects
    /// used in Juno ASP.NET Core REST API services.
    /// </summary>
    /// <remarks>
    /// Swagger Configuration:
    /// https://github.com/domaindrivendev/Swashbuckle.AspNetCore
    /// </remarks>
    public class SchemaExamplesFilter : ISchemaFilter
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            // Format: 2012-03-21T05:40:12.340Z
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,

            // We tried using PreserveReferenceHandling.All and Object, but ran into issues
            // when deserializing string arrays and read only dictionaries
            ReferenceLoopHandling = ReferenceLoopHandling.Error,

            // This is the default setting, but to avoid remote code execution bugs do NOT change
            // this to any other setting.
            TypeNameHandling = TypeNameHandling.None
        };

        /// <summary>
        /// Adds schema examples to the OpenAPI request context. These examples are used
        /// when rendering the Swagger UI definitions for individual API methods. The OpenAPI
        /// request is passed to each filter before being rendered to enable the developer to 
        /// change the output to the UI.
        /// </summary>
        /// <param name="schema">The request schema definition.</param>
        /// <param name="context">The request/API method context.</param>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            schema.ThrowIfNull(nameof(schema));
            context.ThrowIfNull(nameof(context));

            string objectType = context.Type.Name;
            switch (objectType)
            {
                case nameof(Experiment):
                    schema.Example = new OpenApiString(SchemaExamples.Experiment.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;
                        
                case nameof(ExperimentInstance):
                    schema.Example = new OpenApiString(SchemaExamples.ExperimentInstance.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(ExperimentMetadata):
                    schema.Example = new OpenApiString(SchemaExamples.ExperimentContext.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(ExperimentMetadataInstance):
                    schema.Example = new OpenApiString(SchemaExamples.ExperimentContextInstance.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(ExperimentStepInstance):
                    schema.Example = new OpenApiString(SchemaExamples.ExperimentStepInstance.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(ProblemDetails):
                    schema.Example = new OpenApiString(SchemaExamples.ProblemDetails.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(AgentIdentification):
                    schema.Example = new OpenApiString(SchemaExamples.AgentIdentification.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(AgentHeartbeat):
                    schema.Example = new OpenApiString(SchemaExamples.AgentHeartbeat.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(AgentHeartbeatInstance):
                    schema.Example = new OpenApiString(SchemaExamples.AgentHeartbeatInstance.Value
                        .ToJson(SchemaExamplesFilter.SerializerSettings));
                    break;

                case nameof(ExecutionStatus):
                    schema.Enum = new List<IOpenApiAny>(Enum.GetNames(typeof(ExecutionStatus)).Select(e => new OpenApiString(e)));
                    break;
            }
        }
    }
}