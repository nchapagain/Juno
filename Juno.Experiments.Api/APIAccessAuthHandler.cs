namespace Juno.Experiments.Api
{
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Net.Http.Headers;

    /// <summary>
    /// 
    /// </summary>
    public class APIAccessAuthHandler : AuthorizationHandler<APIAccessAuthRequirement>
    {
        /// <summary>
        /// This method handles the AuthZ validation for API call
        /// </summary>
        /// <param name="context">Authorization Handler Context</param>
        /// <param name="requirement">APIAccess Authorization Requirement</param>
        /// <returns></returns>
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, APIAccessAuthRequirement requirement)
        {
            if (context is null)
            {
                return Task.CompletedTask;
            }

            var authFilterCtx = (Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext)context.Resource;
            var stream = authFilterCtx.HttpContext.Request.Headers[HeaderNames.Authorization].ToString().Split(' ')[1];
            var handler = new JwtSecurityTokenHandler();
            var tokenS = handler.ReadToken(stream) as JwtSecurityToken;

            var claim = tokenS.Claims.FirstOrDefault(c => c.Type == requirement.ClaimName && c.Value.Contains(requirement.ClaimValue, StringComparison.OrdinalIgnoreCase));
            if (claim != null)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
