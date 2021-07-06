namespace Juno.Providers
{
    /// <summary>
    /// Supported step parameter name constants.
    /// </summary>
    public static class StepParameters
    {
        /// <summary>
        /// Parameter = 'applicationInsightsInstrumentationKey'.
        /// </summary>
        public const string ApplicationInsightsInstrumentationKey = nameof(StepParameters.ApplicationInsightsInstrumentationKey);

        /// <summary>
        /// Parameter = 'command'.
        /// </summary>
        public const string Command = nameof(StepParameters.Command);

        /// <summary>
        /// Parameter = 'commandArguments'.
        /// </summary>
        public const string CommandArguments = nameof(StepParameters.CommandArguments);

        /// <summary>
        /// Parameter = 'duration' (e.g. 00:30:00).
        /// </summary>
        public const string Duration = nameof(StepParameters.Duration);

        /// <summary>
        /// Parameter = 'enableDiagnostics'
        /// </summary>
        public const string EnableDiagnostics = nameof(StepParameters.EnableDiagnostics);

        /// <summary>
        /// Parameter = 'eventHubConnectionString'
        /// </summary>
        public const string EventHubConnectionString = nameof(StepParameters.EventHubConnectionString);

        /// <summary>
        /// Parameter = 'featureFlag' (e.g. IndividualStepStateSupport).
        /// </summary>
        public const string FeatureFlag = nameof(StepParameters.FeatureFlag);

        /// <summary>
        /// Parameter = 'isAmberNodeRequest'.
        /// </summary>
        public const string IsAmberNodeRequest = nameof(StepParameters.IsAmberNodeRequest);

        /// <summary>
        /// Parameter = 'includeSpecifications'.
        /// </summary>
        public const string IncludeSpecifications = nameof(StepParameters.IncludeSpecifications);

        /// <summary>
        /// Parameter = 'NodeAffinity'.
        /// </summary>
        public const string NodeAffinity = nameof(StepParameters.NodeAffinity);

        /// <summary>
        /// Parameter = 'NodeState'.
        /// </summary>
        public const string NodeState = nameof(StepParameters.NodeState);

        /// <summary>
        /// Parameter = 'packageVersion'
        /// </summary>
        public const string PackageVersion = nameof(StepParameters.PackageVersion);

        /// <summary>
        /// Parameter = 'pilotFishServiceName'.
        /// </summary>
        public const string PilotfishServiceName = nameof(StepParameters.PilotfishServiceName);

        /// <summary>
        /// Parameter = 'pilotFishServicePath'.
        /// </summary>
        public const string PilotfishServicePath = nameof(StepParameters.PilotfishServicePath);

        /// <summary>
        /// Parameter = 'isSocService'.
        /// </summary>
        public const string IsSocService = nameof(StepParameters.IsSocService);

        /// <summary>
        /// Parameter = 'platform'
        /// </summary>
        public const string Platform = nameof(StepParameters.Platform);

        /// <summary>
        /// Parameter = 'runAsAdministrator'.
        /// </summary>
        public const string RunAsAdministrator = nameof(StepParameters.RunAsAdministrator);

        /// <summary>
        /// Parameter = 'nodeTag'.
        /// </summary>
        public const string NodeTag = nameof(StepParameters.NodeTag);

        /// <summary>
        /// Parameter = 'Count'.
        /// </summary>
        public const string Count = nameof(StepParameters.Count);

        /// <summary>
        /// Parameter = 'timeout' (e.g. 00:30:00).
        /// </summary>
        public const string Timeout = nameof(StepParameters.Timeout);

        /// <summary>
        /// Parameter = 'timeoutMinStepsSucceeded'.
        /// </summary>
        public const string TimeoutMinStepsSucceeded = nameof(StepParameters.TimeoutMinStepsSucceeded);
    }
}