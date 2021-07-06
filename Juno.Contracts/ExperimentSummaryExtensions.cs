namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Extension methods for determining summaries of experiments.
    /// </summary>
    public static class ExperimentSummaryExtensions
    {
        /// <summary>
        /// Derives a collection of summaries for experiments from the business signals and progresses provided.
        /// </summary>
        /// <param name="businessSignals">A collection of Business Signals for Juno experiments.</param>
        /// <param name="progresses">A collection of progress indicators for Juno experiments.</param>
        /// <returns>
        /// A collection of <see cref="ExperimentSummary"/> summaries for Juno experiments.
        /// </returns>
        public static IEnumerable<ExperimentSummary> DeriveExperimentSummary(IEnumerable<BusinessSignal> businessSignals, IEnumerable<ExperimentProgress> progresses)
        {
            // We take experiments in business signals as our source of truth and determine progress for them.
            if (businessSignals is null || !businessSignals.Any())
            {
                throw new ArgumentException("Business Signals cannot be empty.", nameof(businessSignals));
            }

            var disintctExperiments = businessSignals
                // Subsequent releases will have TenantId added to the identifiers.
                .GroupBy(businessSignal => new { businessSignal.ExperimentName, businessSignal.Revision });

            // Variable distinctExperiments holds the collection of following "Key" - "Object" pairs.
            // Ex: [
            //                       "Key"                    "Object"
            //         < { experimentNameX, revisionX }, businenssSignalX1 >
            //         <              "                , businenssSignalX2 >
            //         <              "                , businenssSignalX3 >
            //         <              "                , businenssSignalX4 >
            //         <              "                , businenssSignalX5 >
            //
            //         < { experimentNameY, revisionY }, businenssSignalY1 >
            //         <              "                , businenssSignalY2 >
            //         <              "                , businenssSignalY3 >
            //         <              "                , businenssSignalY4 >
            //         <              "                , businenssSignalY5 >
            //
            //                        ...
            //                        ...
            //     ]
            //
            // We are converting the above into the following colection.
            // 5 individual business signals are converted into 1 experiment summary with 5 Semaphores/BusinessSignalKPIs
            // [
            //        Latest experiment for which we have computed business signals will be on the top.
            //        {
            //            "experimentName": "experimentNameX",
            //            "revision": "revisionX",
            //            "progress": Progress inferred from progresses,
            //            "experimentDateUtc": Earliest start date of the businessSignals for the given experiment. Ex: "07-Jun-21".
            //            "semaphores": [
            //                 businenssSignalKPIX1,
            //                 businenssSignalKPIX2,
            //                 ...
            //            ]
            //        }
            //        ...
            // ]

            IEnumerable<ExperimentSummary> summaries = disintctExperiments
                // Select collection of anonymous objects with each object having experimentName, revision, progress, experimentDateUtc and businessSignalKPIs
                .Select(disintctExperiment =>
                    new
                    {
                        experimentName = disintctExperiment.Key.ExperimentName,

                        revision = disintctExperiment.Key.Revision,

                        // In case we fail to determine progress for a given experiment, we mark is as 0.
                        progress = ExperimentSummaryExtensions.GetProgress(
                            progresses, disintctExperiment.Key.ExperimentName, disintctExperiment.Key.Revision),

                        // Earliest start date among the business signals for a given experiment.
                        experimentDateUtc = disintctExperiment.Min(businessSignal => businessSignal.ExperimentDateUtc),

                        // Generating Semaphores/BusinessSignalKPIs/Light-weight Business Signals for a given experiment.
                        businessSignalKPIs = disintctExperiment.Select(businessSignal => new BusinessSignalKPI(businessSignal))
                    })
                // Construct a collection of experiment Summaries using the collection of anonymous objects.
                .Select(obj => new ExperimentSummary(obj.experimentName, obj.revision, obj.progress, obj.experimentDateUtc, obj.businessSignalKPIs))

                // Placing summaries of latest experiments on the top.
                .OrderByDescending(summary => DateTime.Parse(summary.ExperimentDateUtc));

            return summaries;
        }

        /// <summary>
        /// Returns progress of a given experiment.
        /// </summary>
        /// <param name="progresses">A collection of progress indicators for Juno experiments.</param>
        /// <param name="experimentName">Name of the experiment.</param>
        /// <param name="revision">Revision of the experiment.</param>
        /// <returns>
        /// Integer within 0 - 100 depicting the progress of the experiment.
        /// </returns>
        private static int GetProgress(IEnumerable<ExperimentProgress> progresses, string experimentName, string revision)
        {
            return progresses.Where(progress =>
                        progress.ExperimentName.Equals(experimentName, StringComparison.OrdinalIgnoreCase) &&
                        progress.Revision.Equals(revision, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault()?.Progress ?? 0;
        }
    }
}