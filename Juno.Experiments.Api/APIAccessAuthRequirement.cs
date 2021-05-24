namespace Juno.Experiments.Api
{
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// 
    /// </summary>
    public class APIAccessAuthRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Sets up the name and value of the claim to be validated
        /// </summary>
        public APIAccessAuthRequirement(string name, string value)
        {
            this.ClaimName = name;
            this.ClaimValue = value;
        }

        /// <summary>
        /// Name of the Claim as appears in Access Token and viewable by tools
        /// like jwt.ms
        /// </summary>
        public string ClaimName { get; set; }

        /// <summary>
        /// Value of the Claim as appears in Access Token and viewable by tools
        /// like jwt.ms
        /// </summary>
        public string ClaimValue { get; set; }
    }
}
