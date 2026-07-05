metadata description = 'Shared App Service web app for the design-pattern samples: a Linux App Service with a user-assigned managed identity used (deterministically, via AZURE_CLIENT_ID) for keyless access to Azure Cosmos DB. Plain Bicep, intentionally small and reusable across the web samples.'

@description('Required. Name of the web app.')
param name string

@description('Optional. Location for all resources.')
param location string = resourceGroup().location

@description('Optional. Tags to apply. Include an "azd-service-name" tag so azd deploys the app code here.')
param tags object = {}

@description('Optional. Linux runtime stack.')
param linuxFxVersion string = 'DOTNETCORE|10.0'

@description('Optional. Keep the app always running. Required when the app hosts an in-app background service such as a Change Feed processor.')
param alwaysOn bool = true

@description('Optional. App settings (name/value pairs). AZURE_CLIENT_ID (for the managed identity) is added automatically.')
param appSettings array = []

// User-assigned identity the app uses for keyless Cosmos DB access. Created here so the
// same identity can be granted the Cosmos data-plane role and selected via AZURE_CLIENT_ID.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${name}'
  location: location
  tags: tags
}

// Basic (B1) plan — inexpensive and supports "Always On".
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${name}'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      alwaysOn: alwaysOn
      appSettings: concat(appSettings, [
        {
          name: 'AZURE_CLIENT_ID'
          value: identity.properties.clientId
        }
      ])
    }
  }
}

@description('Principal ID of the managed identity (grant it the Cosmos data-plane role).')
output identityPrincipalId string = identity.properties.principalId

@description('Client ID of the managed identity.')
output identityClientId string = identity.properties.clientId

@description('The web app name.')
output name string = site.name

@description('The web app URL.')
output url string = 'https://${site.properties.defaultHostName}'
