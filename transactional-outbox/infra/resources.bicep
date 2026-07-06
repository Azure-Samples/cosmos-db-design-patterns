metadata description = 'Resources for the transactional-outbox sample: an App Service web front end (keyless, via managed identity) that hosts the change feed relay over a serverless, keyless Azure Cosmos DB account. Reuses the shared web-app and secure-cosmos modules. OrderStore holds both orders and outbox events; leases tracks the change feed processor.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs (e.g. the console app).')
param principalId string = ''

// Web front end hosts the change feed relay in-process, so it needs "Always On".
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

module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'OutboxDB'
      }
    ]
    containers: [
      {
        databaseName: 'OutboxDB'
        name: 'OrderStore'
        partitionKey: '/orderId'
      }
      {
        databaseName: 'OutboxDB'
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
