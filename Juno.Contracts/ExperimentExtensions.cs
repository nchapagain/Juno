namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Extension methods for <see cref="Experiment"/> instances.
    /// </summary>
    public static class ExperimentExtensions
    {
        internal const string ParameterReference = "$.parameters";

        /// <summary>
        /// Extension adds the set of sub-components (steps) to the parent component.
        /// </summary>
        /// <param name="component">The parent component to which to add the child components.</param>
        /// <param name="childComponents">The set of child components to add to the parent component.</param>
        public static ExperimentComponent AddOrReplaceChildSteps(this ExperimentComponent component, params ExperimentComponent[] childComponents)
        {
            component.ThrowIfNull(nameof(component));
            childComponents.ThrowIfNullOrEmpty(nameof(childComponents));

            component.Extensions[ContractExtension.Steps] = JToken.FromObject(new List<ExperimentComponent>(childComponents));

            return component;
        }

        /// <summary>
        /// Extension returns the set of sub-components (steps) for the component provided
        /// (if they exist).
        /// </summary>
        /// <param name="component">The component containing sub-components.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentComponent"/> instances that are defined
        /// as sub-components/steps.
        /// </returns>
        public static IEnumerable<ExperimentComponent> GetChildSteps(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            IEnumerable<ExperimentComponent> subComponents = null;
            if (component.HasExtension(ContractExtension.Steps))
            {
                try
                {
                    subComponents = component.Extensions[ContractExtension.Steps].ToObject<IEnumerable<ExperimentComponent>>();
                }
                catch (JsonException exc)
                {
                    throw new SchemaException(
                        $"Invalid child component definition for component '{component.Name}'. Child components must be an array of valid components themselves.",
                        exc);
                }
            }

            return subComponents;
        }

        /// <summary>
        /// Returns the set of distinct group names defined in the experiment.
        /// </summary>
        /// <param name="experiment"></param>
        /// <returns></returns>
        public static IEnumerable<string> GroupNames(this Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));

            return experiment.Workflow
                .Where(component => component.Group != ExperimentComponent.AllGroups)
                ?.Select(component => component.Group)
                ?.Distinct();
        }

        /// <summary>
        /// Extension returns true/false whether the component has a matching extension (by name).
        /// </summary>
        /// <param name="component">The component containing extension.</param>
        /// <param name="extensionName">The name/key of the extension.</param>
        /// <returns>
        /// True if the component itself has the extension defined, false if not.
        /// </returns>
        public static bool HasExtension(this ExperimentComponent component, string extensionName)
        {
            component.ThrowIfNull(nameof(component));
            return component.Extensions.ContainsKey(extensionName);
        }

        /// <summary>
        /// Extension returns true/false whether experiment auto-triage diagnostics is enabled.
        /// </summary>
        /// <param name="experiment">The experiment to verify.</param>
        /// <returns>
        /// True if diagnostics is enabled on the experiment. To enable diagnostics add the following entry to the 
        /// metadata at the experiment level:  enableDiagnostics = true.
        /// </returns>
        public static bool IsDiagnosticsEnabled(this Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));

            return experiment.Metadata.GetValue<bool>(Experiment.EnableDiagnostics, false);
        }

        /// <summary>
        /// Extension returns true/false whether experiment instance has auto-triage diagnostics enabled.
        /// </summary>
        /// <param name="instance">The experiment instance to verify.</param>
        /// <returns>
        /// True if diagnostics is enabled on the experiment. To enable diagnostics add the following entry to the 
        /// metadata at the experiment level:  enableDiagnostics = true.
        /// </returns>
        public static bool IsDiagnosticsEnabled(this ExperimentInstance instance)
        {
            instance.ThrowIfNull(nameof(instance));

            return instance.Definition.IsDiagnosticsEnabled();
        }

        /// <summary>
        /// Extension will create a new <see cref="Experiment"/> with all components
        /// and parameters inlined.
        /// </summary>
        /// <param name="experiment">The experiment to inline.</param>
        /// <returns>
        /// An <see cref="Experiment"/> having all components and parameters inlined.
        /// </returns>
        public static Experiment Inlined(this Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));

            // Inline the parameter references.
            if (experiment.Parameters?.Any() == true)
            {
                Experiment inlinedExperiment = ExperimentExtensions.ApplyIdentifierReferences(experiment.Parameters, experiment);
                ExperimentExtensions.ApplyParameterReferences(inlinedExperiment.Parameters, inlinedExperiment.Workflow);
                ExperimentExtensions.ApplyParameterReferences(inlinedExperiment.Parameters, inlinedExperiment.Metadata);
                inlinedExperiment.Parameters.Clear();
                return inlinedExperiment;
            }

            return experiment;
        }

        /// <summary>
        /// Returns true/false whether the experiment is inlined. An inlined experiment
        /// has all payloads and workloads moved into the workflow and has all reference parameters
        /// updated.
        /// </summary>
        /// <param name="experiment">The experiment to validate as inlined.</param>
        /// <returns>
        /// True if the experiment is inlined, false if not.
        /// </returns>
        public static bool IsInlined(this Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));

            string parameterName;
            return !experiment.Workflow.Any(c => !c.IsInlined())
                && !experiment.Metadata.Any(entry => entry.TryGetParameterReference(out parameterName));
        }

        /// <summary>
        /// Returns true if the component is a 'Parallel Execution' definition which contains child
        /// steps that should be executed in-parallel.
        /// </summary>
        /// <param name="component">The experiment component definition.</param>
        /// <returns>
        /// True if the component definition is a 'Parallel Execution' component having child
        /// steps that should be executed in parallel.
        /// </returns>
        public static bool IsParallelExecution(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            // Example Schema Format:
            // {
            //    "type": "ParallelExecution",
            //    "name": "Parallel Workload Steps",
            //    "steps": [
            //        {
            //            "type": "Juno.Execution.Providers.Workloads.WorkloadProvider1",
            //            "name": "Apply workload A",
            //            "description": "Runs stress on the VM CPU",
            //            "group": "Group A"
            //        },
            //        {
            //            "type": "Juno.Execution.Providers.Workloads.WorkloadProvider2",
            //            "name": "Apply workload B",
            //            "description": "Runs stress on the VM IO",
            //            "group": "Group B"
            //        },
            //    ]
            // }

            return string.Equals(
                component.ComponentType,
                ExperimentComponent.ParallelExecutionType,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true/false whether the parameter value represents a parameter
        /// reference (ex:  $.parameters.Cluster).
        /// </summary>
        /// <param name="parameter">The parameter to validate as a reference.</param>
        /// <param name="parameterName">
        /// The name of the shared parameter that is referenced (e.g. $.parameters.Cluster -> Cluster).
        /// </param>
        /// <returns>
        /// True if the value is a parameter reference, false if not.
        /// </returns>
        public static bool TryGetParameterReference(this KeyValuePair<string, IConvertible> parameter, out string parameterName)
        {
            parameter.ThrowIfNull(nameof(parameter));
            parameterName = null;

            bool isParameterReference = false;

            if (parameter.Value != null
                && parameter.Value.ToString().StartsWith(ExperimentExtensions.ParameterReference, StringComparison.OrdinalIgnoreCase))
            {
                isParameterReference = true;
                parameterName = parameter.Value.ToString().Substring(ExperimentExtensions.ParameterReference.Length + 1);
            }

            return isParameterReference;
        }

        /// <summary>
        /// Returns the experiment flow definition if a flow extension exists.
        /// </summary>
        /// <param name="component">The component containing the flow definition.</param>
        /// <returns></returns>
        public static ExperimentFlow Flow(this ExperimentComponent component)
        {
            ExperimentFlow flow = null;
            if (component.HasExtension(ContractExtension.Flow))
            {
                flow = component.Extensions[ContractExtension.Flow].ToObject<ExperimentFlow>();
            }

            return flow;
        }

        /// <summary>
        /// Appends a string representation of each component to the builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="component">The component to append in string form to the builder.</param>
        internal static StringBuilder AppendComponent(this StringBuilder builder, ExperimentComponent component)
        {
            builder.ThrowIfNull(nameof(builder));
            if (component != null)
            {
                builder.Append(component.GetHashCode());
            }

            return builder;
        }

        /// <summary>
        /// Appends a string representation of each component to the builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="components">The set of components to append in string form to the builder.</param>
        internal static StringBuilder AppendComponents(this StringBuilder builder, IEnumerable<ExperimentComponent> components)
        {
            builder.ThrowIfNull(nameof(builder));

            if (components?.Any() == true)
            {
                builder.Append($"{string.Join(",", components.Select(entry => $"{entry?.GetHashCode()}"))}");
            }

            return builder;
        }

        /// <summary>
        /// Appends a string representation of the extensions/entries to the builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="extensions">The set of extensions to append to the builder.</param>
        internal static StringBuilder AppendExtensions(this StringBuilder builder, IDictionary<string, JToken> extensions)
        {
            builder.ThrowIfNull(nameof(builder));
            if (extensions?.Any() == true)
            {
                builder.Append(string.Join(",", extensions.Select(entry => $"{entry.Key}={entry.Value.ToString()}")));
            }

            return builder;
        }

        /// <summary>
        /// Appends a string representation of the set of parameters to the builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="parameters">The set of parameters to append to the builder.</param>
        internal static StringBuilder AppendParameters(this StringBuilder builder, IDictionary<string, IConvertible> parameters)
        {
            builder.ThrowIfNull(nameof(builder));
            if (parameters?.Any() == true)
            {
                builder.Append(string.Join(",", parameters.Select(entry => $"{entry.Key}={entry.Value}")));
            }

            return builder;
        }

        /// <summary>
        /// Appends a string representation of the properties to the builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="properties">The set of properties to append to the builder.</param>
        internal static StringBuilder AppendProperties(this StringBuilder builder, params IConvertible[] properties)
        {
            builder.ThrowIfNull(nameof(builder));

            if (properties?.Any() == true)
            {
                builder.Append(string.Join(",", properties.Select(p => p?.ToString())));
            }

            return builder;
        }

        private static Experiment ApplyIdentifierReferences(IDictionary<string, IConvertible> identifiers, Experiment experiment)
        {
            return new Experiment(
                experiment.Name.ReplaceIdentifierReference(identifiers),
                experiment.Description.ReplaceIdentifierReference(identifiers),
                experiment.ContentVersion.ReplaceIdentifierReference(identifiers),
                experiment.Metadata,
                experiment.Parameters,
                experiment.Workflow,
                schema: experiment.Schema);
        }

        private static string ReplaceIdentifierReference(this string identifier, IDictionary<string, IConvertible> identifiers)
        {
            if (identifier.TryGetIdentifierReference(out string identifierName))
            {
                if (!identifiers.ContainsKey(identifierName))
                {
                    throw new SchemaException(
                        $"Invalid identifier reference '{identifierName}' in experiment identifiers. " +
                        $"A matching shared parameter is not defined in the experiment.");
                }

                return identifiers.GetValue<string>(identifierName);
            }

            return identifier;
        }

        private static bool TryGetIdentifierReference(this string identifier, out string identifierName)
        {
            identifier.ThrowIfNull(nameof(identifier));

            identifierName = null;

            if (identifier.StartsWith(ExperimentExtensions.ParameterReference, StringComparison.OrdinalIgnoreCase))
            {
                identifierName = identifier.Substring(ExperimentExtensions.ParameterReference.Length + 1);
            }
            
            return identifierName != null;
        }

        private static void ApplyParameterReferences(IDictionary<string, IConvertible> sharedParameters, IDictionary<string, IConvertible> metadata)
        {
            if (metadata?.Any() == true)
            {
                // Inline Parameters for the component itself.
                IDictionary<string, IConvertible> newParameterValues = new Dictionary<string, IConvertible>();

                foreach (KeyValuePair<string, IConvertible> entry in metadata)
                {
                    string parameterName;
                    if (entry.TryGetParameterReference(out parameterName))
                    {
                        if (!sharedParameters.ContainsKey(parameterName))
                        {
                            throw new SchemaException(
                                $"Invalid parameter reference '{parameterName}' in experiment metadata. " +
                                $"A matching shared parameter is not defined in the experiment.");
                        }

                        newParameterValues.Add(entry.Key, sharedParameters[parameterName]);
                    }
                }

                metadata.AddRange(newParameterValues, withReplace: true);
            }
        }

        private static void ApplyParameterReferences(IDictionary<string, IConvertible> sharedParameters, IEnumerable<ExperimentComponent> components)
        {
            if (components?.Any() == true)
            {
                foreach (ExperimentComponent component in components)
                {
                    // Inline Parameters for the component itself.
                    if (component.Parameters?.Any() == true)
                    {
                        IDictionary<string, IConvertible> newParameterValues = new Dictionary<string, IConvertible>();

                        newParameterValues.Clear();
                        foreach (KeyValuePair<string, IConvertible> entry in component.Parameters)
                        {
                            string parameterName;
                            if (entry.TryGetParameterReference(out parameterName))
                            {
                                if (!sharedParameters.ContainsKey(parameterName))
                                {
                                    throw new SchemaException(
                                        $"Invalid parameter reference '{parameterName}' in experiment component/step '{component.Name}'. " +
                                        $"A matching shared parameter is not defined in the experiment.");
                                }

                                newParameterValues.Add(entry.Key, sharedParameters[parameterName]);
                            }
                        }

                        component.Parameters.AddRange(newParameterValues, withReplace: true);
                    }

                    // Inline parameters for any dependencies of the component.
                    if (component.Dependencies?.Any() == true)
                    {
                        ExperimentExtensions.ApplyParameterReferences(sharedParameters, component.Dependencies);
                    }

                    // Inline parameters for any child 'steps' of the component.
                    if (component.HasExtension(ContractExtension.Steps))
                    {
                        IEnumerable<ExperimentComponent> childSteps = component.GetChildSteps();
                        if (childSteps?.Any() == true)
                        {
                            ExperimentExtensions.ApplyParameterReferences(sharedParameters, childSteps);
                            component.Extensions[ContractExtension.Steps] = JToken.FromObject(childSteps);
                        }
                    }
                }
            }
        }

        private static bool IsInlined(this ExperimentComponent component)
        {
            bool isInlined = true;
            string parameterName;
            if (component.Parameters?.Any(p => p.TryGetParameterReference(out parameterName)) == true)
            {
                isInlined = false;
            }

            if (isInlined && component.Dependencies?.Any() == true)
            {
                foreach (ExperimentComponent dependency in component.Dependencies)
                {
                    if (!dependency.IsInlined())
                    {
                        isInlined = false;
                        break;
                    }
                }
            }

            if (isInlined && component.HasExtension(ContractExtension.Steps))
            {
                IEnumerable<ExperimentComponent> childSteps = component.GetChildSteps();
                if (childSteps?.Any() == true)
                {
                    foreach (ExperimentComponent childStep in childSteps)
                    {
                        if (!childStep.IsInlined())
                        {
                            isInlined = false;
                            break;
                        }
                    }
                }
            }

            return isInlined;
        }
    }
}
