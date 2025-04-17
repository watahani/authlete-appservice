function Set-OrAddProperty {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        $Value
    )

    if ($Object.PSObject.Properties[$PropertyName]) {
        $Object.$PropertyName = $Value
    }
    else {
        $Object | Add-Member -MemberType NoteProperty -Name $PropertyName -Value $Value
    }
}

Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  Start updating Authlete Service Settings with deplpyed endpoints...";
Write-Host -ForegroundColor Green "==========================================================================";

$CLIENT_URL = azd env get-value CLIENT_URL;
$AUTHZ_SERVER_URL = azd env get-value AUTHZ_SERVER_URL;

$redirectUri = "$CLIENT_URL/.auth/login/authlete/callback";

$authleteBaseUrl = & azd env get-value AUTHLETE_BASE_URL 2>$null;
$apiKey = & azd env get-value AUTHLETE_SERVICE_API_KEY 2>$null;
$orgToken = & azd env get-value AUTHLETE_ORGANIZATION_ACCESS_TOKEN 2>$null;
$clientId = & azd env get-value AUTHLETE_CLIENT_ID 2>$null;

Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  Updating Authlete Service Settings using with URL deployed to Azure...";
Write-Host -ForegroundColor Green "==========================================================================";

try {
    $headers = @{Authorization = "Bearer $orgToken" };
    $service = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/service/get" -Method Get -Headers $headers -ErrorAction Stop;
 
    Set-OrAddProperty -Object $service -PropertyName "issuer" -Value $AUTHZ_SERVER_URL;
    Set-OrAddProperty -Object $service -PropertyName "authorizationEndpoint" -Value "$AUTHZ_SERVER_URL/api/authorization";
    Set-OrAddProperty -Object $service -PropertyName "tokenEndpoint" -Value "$AUTHZ_SERVER_URL/api/token";
    Set-OrAddProperty -Object $service -PropertyName "jwksUri" -Value "$AUTHZ_SERVER_URL/api/jwks";
    Set-OrAddProperty -Object $service -PropertyName "revocationEndpoint" -Value "$AUTHZ_SERVER_URL/api/revocation";
    Set-OrAddProperty -Object $service -PropertyName "introspectionEndpoint" -Value "$AUTHZ_SERVER_URL/api/introspection";
    Set-OrAddProperty -Object $service -PropertyName "userInfoEndpoint" -Value "$AUTHZ_SERVER_URL/api/userinfo";
    
    $service = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/service/update" -ContentType "application/json" -Method POST -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes(($service | ConvertTo-Json -Depth 10))) -ErrorAction Stop
}
catch {
    Write-Host "Error updating service settings";
    throw
}

try {
    Write-Host -ForegroundColor Green "==========================================================================";
    Write-Host -ForegroundColor Green "  Updating redirect URI for Authlete client...";
    Write-Host -ForegroundColor Green "==========================================================================";

    $demoClient = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/client/get/${clientId}" -Method Get -Headers $headers -ErrorAction Stop;
    Set-OrAddProperty -Object $demoClient -PropertyName "redirectUris" -Value @($redirectUri);
    $demoClient
    $demoClient = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/client/update/${clientId}" -ContentType "application/json" -Method POST `
        -Headers $headers `
        -Body ([System.Text.Encoding]::UTF8.GetBytes(($demoClient | ConvertTo-Json -Depth 10)))`
        -MaximumRedirection 0 -AllowInsecureRedirect -ErrorAction Stop;

}
catch {
    Write-Host "${authleteBaseUrl}/api/${apiKey}/client/update/$($demoClient.clientId)"
    Write-Host "Error updating client settings";
    throw;
}

Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  removing src/java-oauth-server/src/main/resources/authlete.properties...";
Write-Host -ForegroundColor Green "==========================================================================";

if (Test-Path "src/java-oauth-server/src/main/resources/authlete.properties") {
    Remove-Item -Path "src/java-oauth-server/src/main/resources/authlete.properties" -Force;
}
