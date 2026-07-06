metadata description = 'Resources for the loop-safe-change-feed sample: an App Service web front end (keyless, via managed identity) that hosts an in-process Change Feed processor over a serverless, keyless Azure Cosmos DB account. Reuses the shared web-app and secure-cosmos modules. The account pre-creates the Documents container (source + in-place enrichment target) and a leases container for the processor.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs (e.g. the console processor).')
param principalId string = ''

// Interactive web front end + user-assigned identity (shared module). It hosts the Change Feed
// processor in-process, so it needs "Always On". The Cosmos endpoint is derived from the account
// name to avoid a dependency cycle (the Cosmos account grants access to this app's identity).
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

// Serverless, keyless Cosmos DB with the sample's database and containers pre-created and
// data-plane access granted to the web app's identity (and the deploying user, who can run the
// console processor). The leases container is required by the Change Feed processor.
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'EnrichDB'
      }
    ]
    containers: [
      {
        databaseName: 'EnrichDB'
        name: 'Documents'
        partitionKey: '/id'
      }
      {
        databaseName: 'EnrichDB'
        name: 'leases'
        partitionKey: '/id'
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
