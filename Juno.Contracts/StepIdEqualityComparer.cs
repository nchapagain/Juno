namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Used for comparing <see cref="ExperimentStepInstance"/> based on Id
    /// </summary>
    public class StepIdEqualityComparer : IEqualityComparer<ExperimentStepInstance>
    {
        private StepIdEqualityComparer()
        {
        }

        /// <summary>
        /// Instance of <see cref="StepIdEqualityComparer"/>
        /// </summary>
        public static StepIdEqualityComparer Instance { get; } = new StepIdEqualityComparer();

        /// <inheritdoc/>
        public bool Equals(ExperimentStepInstance x, ExperimentStepInstance y)
        {
            x.ThrowIfNull(nameof(x));
            y.ThrowIfNull(nameof(y));
            return x.Id == y.Id;
        }

        /// <inheritdoc/>
        public int GetHashCode(ExperimentStepInstance obj)
        {
            obj.ThrowIfNull(nameof(obj));
            return obj.Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}
