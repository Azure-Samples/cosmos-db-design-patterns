metadata description = 'Resources for the distributed-counter sample: the Visualizer App Service web app (keyless, via managed identity) over a serverless, keyless Azure Cosmos DB account. Reuses the shared web-app and secure-cosmos modules. The ConsumerApp console driver runs locally against the same account.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs (e.g. the ConsumerApp).')
param principalId string = ''

// Visualizer web dashboard + user-assigned identity (shared module). It polls Cosmos
// on demand, so it does not need "Always On". The Cosmos endpoint is derived from the
// account name to avoid a dependency cycle (the Cosmos account is granted access to this
// app's identity).
module web '../../infra/core/web-app.bicep' = {
  name: 'web-app'
  params: {
    name: 'web-${resourceToken}'
    location: location
    tags: union(tags, { 'azd-service-name': 'web' })
    alwaysOn: false
    appSettings: [
      {
        name: 'CosmosUri'
        value: 'https://cosmos-${resourceToken}.documents.azure.com:443/'
      }
      {
        name: 'CosmosDatabase'
        value: 'CounterDB'
      }
      {
        name: 'CosmosContainer'
        value: 'Counters'
      }
    ]
  }
}

// Serverless, keyless Cosmos DB with the sample's database/container pre-created and
// data-plane access granted to the web app's identity (and the deploying user, who runs
// the ConsumerApp locally).
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'CounterDB'
      }
    ]
    containers: [
      {
        databaseName: 'CounterDB'
        name: 'Counters'
        partitionKey: '/pk'
      }
    ]
    dataContributorPrincipalIds: empty(principalId)
      ? [ web.outputs.identityPrincipalId ]
      : [ web.outputs.identityPrincipalId, principalId ]
  }
}

@description('The Cosmos DB document endpoint.')
output cosmosEndpoint string = cosmos.outputs.endpoint

@description('The deployed Visualizer web app URL.')
output webUrl string = web.outputs.url
