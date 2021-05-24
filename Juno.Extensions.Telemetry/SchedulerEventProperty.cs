namespace Juno.Extensions.Telemetry
{
    /// <summary>
    /// Constants that define names of common telemetry events in scheduler context properties.
    /// </summary>
    public static class SchedulerEventProperty
    {
        /// <summary>
        /// cronExpression
        /// </summary>
        public const string CronExpression = "cronExpression";

        /// <summary>
        /// junoOfrs
        /// </summary>
        public const string JunoOfrs = "junoOfrs";

        /// <summary>
        /// nodeList
        /// </summary>
        public const string NodeList = "nodeList";

        /// <summary>
        /// offendingTipSessions
        /// </summary>
        public const string OffendingTipSessions = "offendingTipSessions";

        /// <summary>
        /// scheduleActionResult
        /// </summary>
        public const string ScheduleActionResult = "scheduleActionResult";

        /// <summary>
        /// scheduleContext
        /// </summary>
        public const string ScheduleContext = "scheduleContext";

        /// <summary>
        /// preconditionResult
        /// </summary>
        public const string PreconditionResult = "preconditionResult";

        /// <summary>
        /// targetGoal
        /// </summary>
        public const string TargetGoal = "targetGoal";

        /// <summary>
        /// executionGoal
        /// </summary>
        public const string ExecutionGoal = "executionGoal";

        /// <summary>
        /// targetFailureRate
        /// </summary>
        public const string TargetFailureRate = "targetFailureRate";

        /// <summary>
        /// failureRate
        /// </summary>
        public const string FailureRate = "failureRate";

        /// <summary>
        /// conditionSatisfied
        /// </summary>
        public const string ConditionSatisfied = "conditionSatisfied";

        /// <summary>
        /// threshold
        /// </summary>
        public const string Threshold = "threshold";

        /// <summary>
        /// targetExperiments
        /// </summary>
        public const string TargetExperiments = "targetExperiments";

        /// <summary>
        /// successfulExperimentsCount
        /// </summary>
        public const string SuccessfulExperimentsCount = "successfulExperimentsCount";

        /// <summary>
        /// inProgressExperimentsCount
        /// </summary>
        public const string InProgressExperimentsCount = "inProgressExperimentsCount";

        /// <summary>
        /// nextOccurence
        /// </summary>
        public const string NextOccurence = "nextOccurence";

        /// <summary>
        /// nextOccurence
        /// </summary>
        public const string Candidates = "candidates";

        /// <summary>
        /// List of elligble vm skus returned from ESS.
        /// </summary>
        public const string VmSkuList = "vmSkuList";

        /// <summary>
        /// parameters
        /// </summary>
        public const string Parameters = "parameters";

        /// <summary>
        /// experimentParameters
        /// </summary>
        public const string ExperimentParameters = "experimentParameters";

        /// <summary>
        /// responseContent
        /// </summary>
        public const string ResponseContent = "responseContent";

        /// <summary>
        /// experimentClientResponse
        /// </summary>
        public const string ExperimentClientResponse = "experimentClientResponse";

        /// <summary>
        /// kustoQuery
        /// </summary>
        public const string KustoQuery = "kustoQuery";
    }
}