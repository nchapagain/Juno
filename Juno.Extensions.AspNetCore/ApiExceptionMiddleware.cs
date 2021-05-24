namespace Juno.Extensions.AspNetCore
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides generic exception response handling functionality for Juno API
    /// services.
    /// </summary>
    public class ApiExceptionMiddleware
    {
        private static IDictionary<Type, int> statusCodeMappings = new Dictionary<Type, int>
        {
            [typeof(ArgumentException)] = StatusCodes.Status400BadRequest
        };

        private RequestDelegate nextMiddlewareComponent;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        public ApiExceptionMiddleware(RequestDelegate next)
        {
            this.nextMiddlewareComponent = next;
        }

        /// <summary>
        /// Middleware pipeline invocation entry point.
        /// </summary>
        /// <param name="httpContext">Provides the context of the HTTP request and response.</param>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                if (this.nextMiddlewareComponent != null)
                {
                    await this.nextMiddlewareComponent.Invoke(httpContext).ConfigureDefaults();
                }
            }
            catch (Exception exc)
            {
                await ApiExceptionMiddleware.HandleExceptionAsync(httpContext, exc).ConfigureDefaults();
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            Type exceptionType = exception.GetType();
            context.Response.ContentType = "application/json";

            int statusCode;
            if (!ApiExceptionMiddleware.statusCodeMappings.TryGetValue(exceptionType, out statusCode))
            {
                statusCode = StatusCodes.Status500InternalServerError;
            }

            return context.Response.WriteAsync(new ProblemDetails
            {
                Detail = exception.ToString(withCallStack: false, withErrorTypes: false),
                Instance = context.Request != null
                    ? $"{context.Request.Method} {context.Request.Path.Value}"
                    : null,
                Status = statusCode,
                Title = exceptionType.Name,
                Type = exceptionType.FullName
            }.ToJson());
        }
    }
}
