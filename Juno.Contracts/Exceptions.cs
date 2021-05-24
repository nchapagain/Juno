namespace Juno.Contracts
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represent the base exception for all experiment exceptions/errors.
    /// </summary>
    [Serializable]
    public class ExperimentException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class.
        /// </summary>
        public ExperimentException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public ExperimentException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class.
        /// </summary>
        /// <param name="reason">The reason for the error.</param>
        public ExperimentException(ErrorReason reason)
            : base()
        {
            this.Reason = reason;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="reason">The reason for the error.</param>
        public ExperimentException(string message, ErrorReason reason)
            : base(message)
        {
            this.Reason = reason;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ExperimentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="reason">The reason for the error.</param>
        public ExperimentException(string message, ErrorReason reason, Exception innerException)
            : base(message, innerException)
        {
            this.Reason = reason;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentException"/> class with
        /// the provided serialization and context information.
        /// </summary>
        /// <param name="info">The serialization information.</param>
        /// <param name="context">The streaming context.</param>
        protected ExperimentException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info != null)
            {
                try
                {
                    ErrorReason reason;
                    if (Enum.TryParse<ErrorReason>(info.GetString(nameof(this.Reason)), out reason))
                    {
                        this.Reason = reason;
                    }
                }
                catch
                {
                    // If the properties were not added to the serialization info,
                    // we handle the error and continue.
                }
            }
        }

        /// <summary>
        /// Gets the reason/category of the error.
        /// </summary>
        public ErrorReason Reason { get; }

        /// <summary>
        /// Reads the exception properties from the serialization context.
        /// </summary>
        /// <param name="info">Contains the exception properties.</param>
        /// <param name="context">The serialization streaming context information.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (info != null)
            {
                info.AddValue(nameof(this.Reason), this.Reason.ToString());
            }
        }
    }

    /// <summary>
    /// Represent an error that occurs in the execution of Juno experiments.
    /// </summary>
    [Serializable]
    public class ExecutionException : ExperimentException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class.
        /// </summary>
        public ExecutionException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public ExecutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class.
        /// </summary>
        /// <param name="reason">The reason for the error.</param>
        public ExecutionException(ErrorReason reason)
            : base(reason)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="reason">The reason for the error.</param>
        public ExecutionException(string message, ErrorReason reason)
            : base(message, reason)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="reason">The reason for the error.</param>
        public ExecutionException(string message, ErrorReason reason, Exception innerException)
            : base(message, reason, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionException"/> class with
        /// the provided serialization and context information.
        /// </summary>
        /// <param name="info">The serialization information.</param>
        /// <param name="context">The streaming context.</param>
        protected ExecutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Represent an error that occurred in the context of an experiment provider.
    /// </summary>
    [Serializable]
    public class ProviderException : ExperimentException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class.
        /// </summary>
        public ProviderException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public ProviderException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class.
        /// </summary>
        /// <param name="reason">The reason for the error.</param>
        public ProviderException(ErrorReason reason)
            : base(reason)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="reason">The reason for the error.</param>
        public ProviderException(string message, ErrorReason reason)
            : base(message, reason)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="reason">The reason for the error.</param>
        public ProviderException(string message, ErrorReason reason, Exception innerException)
            : base(message, reason, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderException"/> class with
        /// the provided serialization and context information.
        /// </summary>
        /// <param name="info">The serialization information.</param>
        /// <param name="context">The streaming context.</param>
        protected ProviderException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    ///     Represent an error that occurred in the context of the scheduler service
    /// </summary>
    [Serializable]
    public class SchedulerException : ExperimentException
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SchedulerException" /> class.
        /// </summary>
        public SchedulerException()
            : base()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SchedulerException" /> class with
        ///     the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public SchedulerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchedulerException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="reason">The reason for the error.</param>
        public SchedulerException(string message, ErrorReason reason) 
            : base(message, reason)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SchedulerException" /> class with
        ///     the provided serialization and context information.
        /// </summary>
        /// <param name="info">The serialization information.</param>
        /// <param name="context">The streaming context.</param>
        protected SchedulerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        protected SchedulerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Represents an error that occured in the context of the EnvironmentSelection  Service
    /// </summary>
    [Serializable]
    public class EnvironmentSelectionException : ExperimentException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentSelectionException"/>
        /// </summary>
        public EnvironmentSelectionException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentSelectionException"/>
        /// </summary>
        /// <param name="message">The exception message</param>
        public EnvironmentSelectionException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentSelectionException"/> class.
        /// </summary>
        /// <param name="reason">The reason for the error.</param>
        public EnvironmentSelectionException(ErrorReason reason)
            : base(reason)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentSelectionException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="reason">The reason for the error.</param>
        public EnvironmentSelectionException(string message, ErrorReason reason)
            : base(message, reason)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentSelectionException"/>
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public EnvironmentSelectionException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentSelectionException"/> class with
        /// the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="reason">The reason for the error.</param>
        public EnvironmentSelectionException(string message, ErrorReason reason, Exception innerException)
            : base(message, reason, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentSelectionException"/>
        /// </summary>
        /// <param name="info">The serialization information.</param>
        /// <param name="context">The streaming context.</param>
        protected EnvironmentSelectionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
