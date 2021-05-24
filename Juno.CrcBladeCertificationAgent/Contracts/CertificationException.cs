namespace Juno.CRCTipBladeCertification.Contracts
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represent the base exception for certification exceptions/errors.
    /// </summary>
    [Serializable]
    public class CertificationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CertificationException"/> class.
        /// </summary>
        public CertificationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificationException"/> class with
        /// the provided message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public CertificationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificationException"/> class with
        ///  the provided message and inner exception.
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception.</param>
        public CertificationException(string message, Exception innerException)
             : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificationException"/> class with
        /// the provided serialization and context information.
        /// </summary>
        /// <param name="info">The serialization information.</param>
        /// <param name="context">The streaming context</param>
        protected CertificationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}