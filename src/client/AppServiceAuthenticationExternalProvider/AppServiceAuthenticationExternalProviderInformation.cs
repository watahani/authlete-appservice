using System.Security.Claims;
using System.Text.Json;

namespace Authlete.AppService.Demo
{
    
    public static class AppServiceAuthenticationExternalProviderInformation{
        internal static string AppServiceIdPHeader = "X-MS-CLIENT-PRINCIPAL-IDP";
        internal static string AppServiceClamsHeader = "X-MS-CLIENT-PRINCIPAL";
        public static ClaimsPrincipal? GetUser(IHeaderDictionary headers, string? nameType, string? roleType)
        {
            // {"auth_typ":"authlete","claims":[{"typ":"iss","val":"https:\/\/authz-mccpzlr747kro.azurewebsites.net"},{"typ":"http:\/\/schemas.xmlsoap.org\/ws\/2005\/05\/identity\/claims\/nameidentifier","val":"1003"},{"typ":"aud","val":"1973559295"},{"typ":"exp","val":"1744543444"},{"typ":"iat","val":"1744457044"},{"typ":"http:\/\/schemas.microsoft.com\/ws\/2008\/06\/identity\/claims\/authenticationinstant","val":"1744457042"},{"typ":"nonce","val":"14e401139b0547fcabba9faab208abed_20250412112857"},{"typ":"s_hash","val":"P--AZkfNLiZJeZoGJxLGlA"}],"name_typ":"http:\/\/schemas.xmlsoap.org\/ws\/2005\/05\/identity\/claims\/name","role_typ":"http:\/\/schemas.microsoft.com\/ws\/2008\/06\/identity\/claims\/role"}
            string idp;
            string claimsHeader;

            if(headers.TryGetValue(AppServiceIdPHeader, out var idpHeaderValue))
            {
                //decode base64 from UTF8 string
                idp = idpHeaderValue.ToString();
            }
            else
            {
                // unauthorized
                return null;
            }

            if (headers.TryGetValue(AppServiceClamsHeader, out var claimsHeaderValue))
            {
                //decode base64 from UTF8 string
                claimsHeader = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(claimsHeaderValue.ToString()));
            }
            else
            {
                return null;
            }
            //convert json to jsonObject
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(claimsHeader);
            
            JsonElement claimsElement;
            if (jsonObject.TryGetProperty("claims", out claimsElement))
            {
                var claims = new List<Claim>();
                foreach (var claim in claimsElement.EnumerateArray())
                {
                    var type = claim.GetProperty("typ").GetString();
                    var value = claim.GetProperty("val").GetString();
                    if (type == null || value == null)
                    {
                        continue;
                    }
                    claims.Add(new Claim(type, value));
                }
                

                
                var identity = new ClaimsIdentity(
                    claims, 
                    AppServiceAuthenticationExternalProviderDefaults.AuthenticationScheme,
                    // App service authentication transforms the subject claim to http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier
                    nameType ?? "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    roleType ?? ClaimsIdentity.DefaultRoleClaimType
                );
                return new ClaimsPrincipal(identity);
            }
            else
            {
                return null;
            }
        }
    }
}