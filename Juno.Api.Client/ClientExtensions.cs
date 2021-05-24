namespace Juno.Api.Client
{
    using System;
    using System.Net;
    using System.Net.Http;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;

    /// <summary>
    /// Extension methods for Juno API client operations.
    /// </summary>
    public static class ClientExtensions
    {
        /// <summary>
        /// Extension throws an exception of the type specified if the response status code is unsuccessful.
        /// Note that the contents of the response is expected to be 
        /// </summary>
        /// <typeparam name="TException"></typeparam>
        /// <param name="response"></param>
        /// <returns></returns>
        public static void ThrowOnError<TException>(this HttpResponseMessage response)
            where TException : Exception
        {
            response.ThrowIfNull(nameof(response));

            if (!response.IsSuccessStatusCode)
            {
                TException exception = null;
                try
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // Note that this makes a reasonable assumption that the TException exception implementation
                        // includes a constructor that takes in a single 'message' parameter. Whereas this is a defacto
                        // standard with exception classes, it is not an absolute requirement.
                        exception = (TException)Activator.CreateInstance(
                            typeof(TException),
                            $"API Request Error (status code = {response.StatusCode}): {response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}",
                            ErrorReason.Unauthorized);
                    }
                    else if (!response.IsJsonContent())
                    {
                        exception = (TException)Activator.CreateInstance(
                            typeof(TException),
                            $"API Request Error (status code = {response.StatusCode}): {response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}");
                    }
                    else
                    {
                        ProblemDetails errorDetails = response.Content.ReadAsJsonAsync<ProblemDetails>()
                            .GetAwaiter().GetResult();

                        string errorMessage = $"{errorDetails.Detail} (title={errorDetails.Title}, type={errorDetails.Type}, instance={errorDetails.Instance})";

                        ErrorReason errorReason = ErrorReason.Undefined;
                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.Conflict:
                                errorReason = ErrorReason.DataAlreadyExists;
                                break;

                            case HttpStatusCode.NotFound:
                                errorReason = ErrorReason.DataNotFound;
                                break;

                            case HttpStatusCode.PreconditionFailed:
                                errorReason = ErrorReason.DataETagMismatch;
                                break;
                        }

                        // Note that this makes a reasonable assumption that the TException exception implementation
                        // includes a constructor that takes in a single 'message' parameter. Whereas this is a defacto
                        // standard with exception classes, it is not an absolute requirement.
                        exception = (TException)Activator.CreateInstance(typeof(TException), errorMessage, errorReason);
                    }
                }
                catch (Exception exc)
                {
                    exception = new ExperimentException($"API Request Error (status code = {response.StatusCode})", exc) as TException;
                }

                throw exception;
            }
        }
    }
}
