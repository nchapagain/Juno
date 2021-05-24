namespace Juno
{
    using System;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A mock/test logger that can be used to validate logging behaviors in tests.
    /// </summary>
    public class TestLogger : ILogger
    {
        /// <summary>
        /// Action/delegate to invoke when the 'BeginScope' method is executed.
        /// </summary>
        public Action<object> OnBeginScope { get; set; }

        /// <summary>
        /// Action/delegate to invoke when the 'IsEnabled' method is executed.
        /// </summary>
        public Func<LogLevel, bool> OnIsEnabled { get; set; }

        /// <summary>
        /// Action/delegate to invoke when the 'Log' method is executed.
        /// </summary>
        public Action<LogLevel, EventId, object, Exception> OnLog { get; set; }

        /// <summary>
        /// Allows the validation of the logging behavior to begin scope.
        /// </summary>
        /// <typeparam name="TState">The data type for the state object.</typeparam>
        /// <param name="state">A state object to pass to the logic within the logging scope.</param>
        public IDisposable BeginScope<TState>(TState state)
        {
            this.OnBeginScope?.Invoke(state);
            return new NoOpDisposable();
        }

        /// <summary>
        /// Allows the validation of the logging behavior when checking for enablement.
        /// </summary>
        /// <param name="logLevel">The log level to verify as enabled.</param>
        public bool IsEnabled(LogLevel logLevel)
        {
            return this.OnIsEnabled != null
                ? this.OnIsEnabled.Invoke(logLevel)
                : true;
        }

        /// <summary>
        /// Allows the validation of the logging behavior itself.
        /// </summary>
        /// <typeparam name="TState">The data type of the log message/object.</typeparam>
        /// <param name="logLevel">The log severity level for the message.</param>
        /// <param name="eventId">The event ID for the message.</param>
        /// <param name="state">The log message/object.</param>
        /// <param name="exception">An exception to log.</param>
        /// <param name="formatter">A formatter to structure the message given an exception is supplied.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            this.OnLog?.Invoke(logLevel, eventId, state, exception);
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
