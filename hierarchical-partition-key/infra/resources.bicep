metadata description = 'Resources for the hierarchical-partition-key sample: an App Service web front end (keyless, via managed identity) over a serverless, keyless Azure Cosmos DB account. The Events container uses a hierarchical partition key (/tenantId then /userId); EventsByUser uses a single /userId key for the before/after comparison. Reuses the shared web-app and secure-cosmos modules.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs (e.g. the console app).')
param principalId string = ''

module web '../../infra/core/web-app.bicep' = {
  name: 'web-app'
  params: {
    name: 'web-${resourceToken}'
    location: location
    tags: union(tags, { 'azd-service-name': 'web' })
    alwaysOn: true
    appSettings: [
      {
        name: 'CosmosUri'
        value: 'https://cosmos-${resourceToken}.documents.azure.com:443/'
      }
    ]
  }
}

// Serverless, keyless Cosmos DB. Containers are pre-created (the partition key can only be set at
// creation time), so the app identity only needs data-plane access.
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'ActivityDB'
      }
    ]
    containers: [
      {
        databaseName: 'ActivityDB'
        name: 'Events'
        // Hierarchical partition key: /tenantId then /userId.
        partitionKeyPaths: [
          '/tenantId'
          '/userId'
        ]
        partitionKeyKind: 'MultiHash'
      }
      {
        databaseName: 'ActivityDB'
        name: 'EventsByUser'
        // Single-key "before" container for the comparison.
        partitionKey: '/userId'
      }
    ]
    dataContributorPrincipalIds: empty(principalId)
      ? [ web.outputs.identityPrincipalId ]
      : [ web.outputs.identityPrincipalId, principalId ]
  }
}

@description('The Cosmos DB document endpoint.')
output cosmosEndpoint string = cosmos.outputs.endpoint

@description('The deployed web front end URL.')
output webUrl string = web.outputs.url
