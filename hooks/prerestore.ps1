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

Write-Host -ForegroundColor Green "👋 Welcome to Authlete App Service Sample! 👋`n"
Write-Host "`nTo run this sample, you need to sign up for Authlete 3.0 and enter the following parameters`n"
Write-Host "  - base URL (eg: https://jp.authlete.com)  - organization ID (eg: 664427308880310) `n  - organization access token (eg: -vA8h21Vuwf6X513gxTVFh5edi5zUuIutsITM8ezWe8)`n  - service api key (eg: 2531176892)`n`nIf you don’t have an account, please register at: https://console.authlete.com/register"

Write-Host -ForegroundColor Red "=========================================================================="
Write-Host -ForegroundColor Red "⚠️  This script will overwrite the existing Authlete Service Settings. "
Write-Host -ForegroundColor Red "⚠️  If you want to keep the existing settings, please create new service. "
Write-Host -ForegroundColor Red "==========================================================================`n"

Write-Host -ForegroundColor Red "Enter Y to continue"

$y = Read-Host "(Y/N)"

if ($y.ToLower() -ne "y") {
    Write-Host "Aborted.";
    exit 1;
}

git submodule update --init --recursive 

$orgToken = & azd env get-value AUTHLETE_ORGANIZATION_ACCESS_TOKEN 2>$null;
if ($LASTEXITCODE -eq 1) {
    $orgToken = Read-Host -MaskInput "Enter Authlete Organization Access Token";
}

$apiKey = & azd env get-value AUTHLETE_SERVICE_API_KEY 2>$null;
if ($LASTEXITCODE -eq 1) {
    $apiKey = Read-Host "Enter Authlete Service ID";
}

$apiToken = & azd env get-value AUTHLETE_SERVICE_API_ACCESS_TOKEN 2>$null;
if ($LASTEXITCODE -eq 1) {
    $apiToken = Read-Host -MaskInput "Enter Authlete Service Access Token";
}

$authleteBaseUrl = & azd env get-value AUTHLETE_BASE_URL 2>$null;
if ($LASTEXITCODE -eq 1) {
    $authleteBaseUrl = Read-Host "Enter Authlete Base URL (example: https://jp.authlete.com)";
    if (-not $authleteBaseUrl -match "^https://(jp|us|eu|br)\.authlete\.com$") {
        Write-Host "Invalid Authlete Base URL. allowed format: https://(jp|us|eu|br).authlete.com";
        exit 1;
    }
}

try {
    $headers = @{Authorization = "Bearer $apiToken" }
    $clients = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/client/get/list?start=0&end=10" -Method Get -Headers $headers -ErrorAction Stop;
}
catch {
    Write-Host "Failed to get authlete service. Please check your Service Access Token and Base URL.";
    throw;
}

try {
    $headers = @{Authorization = "Bearer $orgToken" };
    $service = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/service/get" -Method Get -Headers $headers -ErrorAction Stop;
}
catch {
    Write-Host "Failed to get authlete service. Please check your Organization Token and Base URL. ";
    throw;
}

Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  Saving Authlete Service Settings using azd env set command...";
Write-Host -ForegroundColor Green "==========================================================================";

# [TODO] you should use azd env set-secret to store secrets for production environments
azd env set AUTHLETE_ORGANIZATION_ACCESS_TOKEN $orgToken;
azd env set AUTHLETE_SERVICE_API_KEY $apiKey;
azd env set AUTHLETE_SERVICE_API_ACCESS_TOKEN $apiToken;
azd env set AUTHLETE_BASE_URL $authleteBaseUrl;

Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  Exporting Authlete Service Settings to src/java-oauth-server/src/main/resources/authlete.properties...";
Write-Host -ForegroundColor Green "==========================================================================";

$authleteProperties = @"
api_version = V3
base_url=$authleteBaseUrl
service.api_key=$apiKey
service.access_token=$apiToken
"@;

$authleteProperties | Out-File -FilePath "src/java-oauth-server/src/main/resources/authlete.properties" -Encoding utf8 -Force;

if (-not $service.jwks) {
    Write-Host -ForegroundColor Green "==========================================================================";
    Write-Host -ForegroundColor Green "  Service JWKS is not set. Creating a new jwks and update the service...";
    Write-Host -ForegroundColor Green "==========================================================================";

    $mkJwks = Invoke-RestMethod -UseBasicParsing -Uri "https://mkjwk.org/jwk/ec?alg=ES256&use=sig&gen=sha256&crv=P-256";
    $kid = $mkJwks.jwk.kid;
    $jwks = $mkJwks.jwks | ConvertTo-Json -Depth 10 -Compress;

    Set-OrAddProperty -Object $service -PropertyName "jwks" -Value $jwks;
    Set-OrAddProperty -Object $service -PropertyName "idTokenSignatureKeyId" -Value $kid;
    Set-OrAddProperty -Object $service -PropertyName "accessTokenSignAlg" -Value "ES256";
    
    Write-Host "Updating service jwks with kid: $kid...";

    $service = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/service/update" -ContentType "application/json" -Method POST -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes(($service | ConvertTo-Json -Depth 10))) -ErrorAction Stop
}

Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  Updating service settings...";
Write-Host -ForegroundColor Green "==========================================================================";

Write-Host '$service.supportedGrantTypes = @("AUTHORIZATION_CODE", "REFRESH_TOKEN");
$service.supportedResponseTypes = @("CODE");
$service.supportedAuthorizationDetailsTypes = @("client-api", "service-api");'

Set-OrAddProperty -Object $service -PropertyName "supportedGrantTypes" -Value @("AUTHORIZATION_CODE", "REFRESH_TOKEN");
Set-OrAddProperty -Object $service -PropertyName "supportedResponseTypes" -Value @("CODE");
Set-OrAddProperty -Object $service -PropertyName "supportedAuthorizationDetailsTypes" -Value  @("client-api", "service-api");
Set-OrAddProperty -Object $service -PropertyName "claimShortcutRestrictive" -Value $false;

$service = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/service/update" -ContentType "application/json" -Method POST -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes(($service | ConvertTo-Json -Depth 10))) -ErrorAction Stop

$demoClient = $clients.clients | Where-Object { $_.clientName -eq "Azure Demo Client" }; 

if (-not $demoClient) {
    Write-Host -ForegroundColor Green "==========================================================================";
    Write-Host -ForegroundColor Green "  Creating new Authlete client...";
    Write-Host -ForegroundColor Green "==========================================================================";
    $body = @"
{
  "clientName": "Azure Demo Client",
  "clientType": "CONFIDENTIAL",
  "applicationType": "WEB",
  "grantTypes": [
    "AUTHORIZATION_CODE",
    "REFRESH_TOKEN"
  ],
  "responseTypes": [
    "CODE",
    "ID_TOKEN",
    "CODE_ID_TOKEN"
  ],
  "redirectUris": [],
  "tokenAuthMethod": "CLIENT_SECRET_POST",
  "attributes": [],
  "idTokenSignAlg": "ES256",
  "authorizationDetailsTypes": [
    "client-api",
    "service-api"
  ]
}
"@
    Write-Host "body prameter: $body"
    $demoClient = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/client/create" -ContentType "application/json" -Method POST `
        -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($body))`
        -MaximumRedirection 0 -AllowInsecureRedirect -ErrorAction Stop;
}
else {
    Write-Host -ForegroundColor Green "==========================================================================";
    Write-Host -ForegroundColor Green "  Updating Authlete client settings...";
    Write-Host -ForegroundColor Green "==========================================================================";
    Write-Host '$demoClient.clientType = "CONFIDENTIAL";
$demoClient.applicationType = "WEB";
$demoClient.grantTypes = @("AUTHORIZATION_CODE", "REFRESH_TOKEN");
$demoClient.responseTypes = @("CODE", "ID_TOKEN", "CODE_ID_TOKEN");
$demoClient.tokenAuthMethod = "CLIENT_SECRET_POST";
$demoClient.idTokenSignAlg = "ES256";'
$demoClient.authorizationDetailsTypes = @("client-api", "service-api");

    Set-OrAddProperty -Object $demoClient -PropertyName "clientType" -Value "CONFIDENTIAL";
    Set-OrAddProperty -Object $demoClient -PropertyName "applicationType" -Value "WEB";
    Set-OrAddProperty -Object $demoClient -PropertyName "grantTypes" -Value @("AUTHORIZATION_CODE", "REFRESH_TOKEN");
    Set-OrAddProperty -Object $demoClient -PropertyName "responseTypes" -Value @("CODE", "ID_TOKEN", "CODE_ID_TOKEN");
    Set-OrAddProperty -Object $demoClient -PropertyName "tokenAuthMethod" -Value "CLIENT_SECRET_POST";
    Set-OrAddProperty -Object $demoClient -PropertyName "idTokenSignAlg" -Value "ES256";
    Set-OrAddProperty -Object $demoClient -PropertyName "authorizationDetailsTypes" -Value @("client-api", "service-api");

    $demoClient = Invoke-RestMethod -Uri "${authleteBaseUrl}/api/${apiKey}/client/update/$($demoClient.clientId)" -ContentType "application/json" -Method POST `
        -Headers $headers `
        -Body ([System.Text.Encoding]::UTF8.GetBytes(($demoClient | ConvertTo-Json -Depth 10)))`
        -MaximumRedirection 0 -AllowInsecureRedirect -ErrorAction Stop;
}
Write-Host -ForegroundColor Green "==========================================================================";
Write-Host -ForegroundColor Green "  Saving Client ID and secret to azd env file...";
Write-Host -ForegroundColor Green "==========================================================================";
$clientSecret = $demoClient.clientSecret;
azd env set AUTHLETE_CLIENT_ID $demoClient.clientId;
azd env set AUTHLETE_CLIENT_SECRET $clientSecret;

$azureOpenAIEndPoint = & azd env get-value AZURE_OPENAI_ENDPOINT 2>$null;
if ($LASTEXITCODE -eq 1) {
    $azureOpenAIEndPoint = Read-Host "Enter Azure OpenAI Endpoint";
}

$azureOpenAIKey = & azd env get-value AZURE_OPENAI_KEY 2>$null;
if ($LASTEXITCODE -eq 1) {
    $azureOpenAIKey = Read-Host -MaskInput "Enter Azure OpenAI Key";
}

azd env set AZURE_OPENAI_ENDPOINT $azureOpenAIEndPoint;
azd env set AZURE_OPENAI_KEY $azureOpenAIKey;



