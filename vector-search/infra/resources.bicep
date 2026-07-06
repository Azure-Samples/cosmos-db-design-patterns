metadata description = 'Resources for the vector-search sample: an App Service web front end (keyless, via managed identity) over a serverless, keyless Azure Cosmos DB account whose Movies container is created with a vector embedding policy and a DiskANN vector index. Reuses the shared web-app and secure-cosmos modules.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs (e.g. the console app).')
param principalId string = ''

// Interactive web front end + user-assigned identity (shared module). It loads the embedding
// model and seeds the catalog at startup, so it runs with "Always On". The Cosmos endpoint is
// derived from the account name to avoid a dependency cycle.
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

// Serverless, keyless Cosmos DB. The Movies container is pre-created WITH the vector embedding
// policy + DiskANN vector index (these can only be set at creation time), so the app identity only
// needs data-plane access to read/write items and run vector queries.
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'MoviesDB'
      }
    ]
    containers: [
      {
        databaseName: 'MoviesDB'
        name: 'Movies'
        partitionKey: '/id'
        vectorEmbeddingPolicy: {
          vectorEmbeddings: [
            {
              path: '/embedding'
              dataType: 'float32'
              distanceFunction: 'cosine'
              dimensions: 384
            }
          ]
        }
        indexingPolicy: {
          indexingMode: 'consistent'
          automatic: true
          includedPaths: [
            {
              path: '/*'
            }
          ]
          excludedPaths: [
            {
              path: '/embedding/*'
            }
          ]
          vectorIndexes: [
            {
              path: '/embedding'
              type: 'diskANN'
            }
          ]
        }
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
