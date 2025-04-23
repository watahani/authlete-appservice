targetScope = 'resourceGroup'
var appServicePlanSku = 'B1'
var appServicePlanCapacity = 1

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string = 'japaneast'

@description('Client ID for client server')
param clientId string

@description('Client secret for client server')
param clientSecret string

param linuxFxVersion string = 'DOTNETCORE|9.0'

// Tags that should be applied to all resources.
//
// Note that 'azd-service-name' tags should be applied separately to service host resources.
// Example usage:
//   tags: union(tags, { 'azd-service-name': <service name in azure.yaml> })
var tags = {
  'azd-env-name': environmentName
}

var uniqueId = uniqueString(subscription().id, resourceGroup().id, environmentName)

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: 'aspd-${uniqueId}'
  location: location
  sku: {
    name: appServicePlanSku
    capacity: appServicePlanCapacity
  }
  properties: {
    zoneRedundant: false
    reserved: true
  }
  kind: 'linux'
  tags: tags
}

resource authzServer 'Microsoft.Web/sites@2023-01-01' = {
  name: 'authz-${uniqueId}'
  location: location
  properties: {
    httpsOnly: true
    serverFarmId: appServicePlan.id
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: 'TOMCAT|9.0-java21'
      minTlsVersion: '1.2'
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      appSettings: [
      ]
    }
  }
  tags: union(tags, {
    'azd-service-name': 'authz'
  })
}

resource client 'Microsoft.Web/sites@2023-01-01' = {
  name: 'client-${uniqueId}'
  location: location
  properties: {
    httpsOnly: true
    serverFarmId: appServicePlan.id
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      appSettings: [
        {
          name: 'AUTHLETE_AUTHENTICATION_SECRET'
          value: clientSecret
        }
        {
          name: 'RESOURCE_IDENTIFIER'
          value: 'https://${api.properties.defaultHostName}'
        }
      ]
    }
  }
  tags: union(tags, {
    'azd-service-name': 'client'
  })
  dependsOn: [
    authzServer
  ]
}

resource clientAuthConfig 'Microsoft.Web/sites/config@2022-09-01' = {
  name: 'authsettingsV2'
  parent: client
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'AllowAnonymous'
      redirectToProvider: 'authlete'
    }
    identityProviders: {
      customOpenIdConnectProviders: {
        authlete: {
          registration: {
            clientCredential: {
              clientSecretSettingName: 'AUTHLETE_AUTHENTICATION_SECRET'
            }
            clientId: clientId
            openIdConnectConfiguration: {
              wellKnownOpenIdConfiguration: 'https://${authzServer.properties.defaultHostName}/.well-known/openid-configuration'
            }
          }
          login: {}
        }
      }
    }
    login: {
      routes: {}
      tokenStore: {
        enabled: true
        tokenRefreshExtensionHours: json('72.0')
        fileSystem: {}
        azureBlobStorage: {}
      }
      preserveUrlFragmentsForLogins: false
      cookieExpiration: {
        convention: 'FixedTime'
        timeToExpiration: '08:00:00'
      }
      nonce: {
        validateNonce: true
        nonceExpirationInterval: '00:05:00'
      }
    }
    httpSettings: {
      requireHttps: true
      routes: {
        apiPrefix: '/.auth'
      }
      forwardProxy: {
        convention: 'NoProxy'
      }
    }
  }
}

resource api 'Microsoft.Web/sites@2023-01-01' = {
  name: 'api-${uniqueId}'
  location: location
  properties: {
    httpsOnly: true
    serverFarmId: appServicePlan.id
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      appSettings: [
        {
          name: 'AUTHLETE_AUTHENTICATION_SECRET'
          value: 'api-does-not-need-client-secret-this-is-dummy'
        }
      ]
    }
  }
  tags: union(tags, {
    'azd-service-name': 'api'
  })
  dependsOn: [
    authzServer
  ]
}

resource apiAuthConfig 'Microsoft.Web/sites/config@2022-09-01' = {
  name: 'authsettingsV2'
  parent: api
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'Return401'
    }
    identityProviders: {
      customOpenIdConnectProviders: {
        authlete: {
          registration: {
            clientCredential: {
              clientSecretSettingName: 'AUTHLETE_AUTHENTICATION_SECRET'
            }
            clientId: 'https://${api.properties.defaultHostName}'
            openIdConnectConfiguration: {
              wellKnownOpenIdConfiguration: 'https://${authzServer.properties.defaultHostName}/.well-known/openid-configuration'
            }
          }
          login: {}
        }
      }
    }
    login: {
      routes: {}
      tokenStore: {
        enabled: true
        tokenRefreshExtensionHours: json('72.0')
        fileSystem: {}
        azureBlobStorage: {}
      }
      preserveUrlFragmentsForLogins: false
      cookieExpiration: {
        convention: 'FixedTime'
        timeToExpiration: '08:00:00'
      }
      nonce: {
        validateNonce: true
        nonceExpirationInterval: '00:05:00'
      }
    }
    httpSettings: {
      requireHttps: true
      routes: {
        apiPrefix: '/.auth'
      }
      forwardProxy: {
        convention: 'NoProxy'
      }
    }
  }
}

output AUTHZ_SERVER_URL string = 'https://${authzServer.properties.defaultHostName}'
output CLIENT_URL string = 'https://${client.properties.defaultHostName}'
output API_URL string = 'https://${api.properties.defaultHostName}'
