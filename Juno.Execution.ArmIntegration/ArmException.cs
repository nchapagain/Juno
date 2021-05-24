namespace Juno.Execution.ArmIntegration
{
    using System;

    /// <summary>
    /// Arm deployment related exception(template and non-template version)
    /// </summary>
    public class ArmException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArmException"/> class.
        /// </summary>
        public ArmException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmException"/> class.
        /// </summary>
        /// <param name="message"></param>
        public ArmException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmException"/> class.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ArmException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="errorResponse">The resource management error response.</param>
        public ArmException(string message, ErrorResponse errorResponse)
            : base(message)
        {
            this.ErrorResponse = errorResponse;
        }

        /// <summary>
        /// Arm template error response <see cref="ErrorResponse"/>
        /// </summary>
        public ErrorResponse ErrorResponse { get; }
    }
}
