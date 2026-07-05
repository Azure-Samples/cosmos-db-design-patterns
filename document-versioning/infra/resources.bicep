metadata description = 'Resources for the document-versioning sample: an App Service web app that reads/writes and runs a Change Feed processor against a serverless, keyless Azure Cosmos DB account. The app authenticates to Cosmos with a user-assigned managed identity (used deterministically via AZURE_CLIENT_ID).'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs.')
param principalId string = ''

// User-assigned managed identity that the web app uses to talk to Cosmos DB.
// Created first so its principal is known up front (no dependency cycle) and so the
// app can select it unambiguously via AZURE_CLIENT_ID.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${resourceToken}'
  location: location
  tags: tags
}

// Basic (B1) plan so the web app can enable "Always On" (required for the in-app
// Change Feed processor background service). Still an inexpensive tier.
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${resourceToken}'
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

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: 'web-${resourceToken}'
  location: location
  // azd uses this tag to deploy the matching service's code to this resource.
  tags: union(tags, { 'azd-service-name': 'web' })
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
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      // Keep the app warm so the Change Feed background service keeps running.
      alwaysOn: true
      appSettings: [
        // Keyless Cosmos DB access. CosmosKey is left unset, so the app uses
        // DefaultAzureCredential; AZURE_CLIENT_ID makes it use the user-assigned identity.
        {
          name: 'AZURE_CLIENT_ID'
          value: identity.properties.clientId
        }
        {
          name: 'CosmosDb__CosmosUri'
          value: cosmos.outputs.endpoint
        }
      ]
    }
  }
}

// Serverless, keyless Cosmos DB with the sample's database/containers pre-created and
// data-plane access granted to the app's user-assigned identity (and the deploying user).
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'DocumentVersionDB'
      }
    ]
    containers: [
      {
        databaseName: 'DocumentVersionDB'
        name: 'CurrentOrderStatus'
        partitionKey: '/CustomerId'
      }
      {
        databaseName: 'DocumentVersionDB'
        name: 'HistoricalOrderStatus'
        partitionKey: '/CustomerId'
      }
      {
        databaseName: 'DocumentVersionDB'
        name: 'leases'
        partitionKey: '/id'
      }
    ]
    dataContributorPrincipalIds: empty(principalId)
      ? [ identity.properties.principalId ]
      : [ identity.properties.principalId, principalId ]
  }
}

@description('The Cosmos DB document endpoint.')
output cosmosEndpoint string = cosmos.outputs.endpoint

@description('The deployed web app URL.')
output webUrl string = 'https://${web.properties.defaultHostName}'
