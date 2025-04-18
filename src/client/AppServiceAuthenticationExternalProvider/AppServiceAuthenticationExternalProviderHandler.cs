using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Authlete.AppService.Demo
{
    public class AppServiceAuthenticationExternalProviderHandler : AuthenticationHandler<AppServiceAuthenticationOptions>
    {
        public AppServiceAuthenticationExternalProviderHandler(
            IOptionsMonitor<AppServiceAuthenticationOptions> options,
            ILoggerFactory logger,
#if NET8_0_OR_GREATER
            UrlEncoder encoder) : base(options, logger, encoder)
#else
            UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock)
#endif
        {

        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {

            ClaimsPrincipal? claimsPrincipal = AAppServiceAuthenticationExternalProviderInformation.GetUser(Context.Request.Headers, Options.nameType, Options.roleType);
            if (claimsPrincipal != null)
            {
                AuthenticationTicket ticket = new AuthenticationTicket(claimsPrincipal, AppServiceAuthenticationExternalProviderDefaults.AuthenticationScheme);
                AuthenticateResult success = AuthenticateResult.Success(ticket);
                return Task.FromResult(success);
            }

            // Try another handler
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            // redirect to toppage
            if (Options.responseStatusCode == StatusCodes.Status302Found)
            {
                var path = Uri.EscapeDataString(Context.Request.Path.ToString());
                Context.Response.Redirect($"/.auth/login/authlete?state={path}&{Options.authorizationOpetionQueries}");
            }
            else
            {
                Context.Response.StatusCode = Options.responseStatusCode;
            }
            return Task.CompletedTask;
        }
    }

    public class AppServiceAuthenticationOptions : AuthenticationSchemeOptions
    {
        public int responseStatusCode { get; set; } = StatusCodes.Status302Found;
        public string authorizationOpetionQueries { get; set; } = "";
        public string? nameType { get; set; } = null;
        public string? roleType { get; set; } = null;
    }
}