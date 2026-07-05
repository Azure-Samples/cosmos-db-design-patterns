metadata description = 'Resources for the preallocation console sample: a serverless, keyless Azure Cosmos DB account with the sample database and both comparison containers pre-created, and data-plane access granted to the deploying user. Pre-creating the containers means the app identity only needs data-plane item access, not permission to create databases/containers.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs.')
param principalId string = ''

module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'PreallocationDB'
      }
    ]
    containers: [
      {
        databaseName: 'PreallocationDB'
        name: 'WithPreallocation'
        partitionKey: '/hotelId'
      }
      {
        databaseName: 'PreallocationDB'
        name: 'WithoutPreallocation'
        partitionKey: '/hotelId'
      }
    ]
    dataContributorPrincipalIds: empty(principalId) ? [] : [ principalId ]
  }
}

@description('The Cosmos DB document endpoint.')
output cosmosEndpoint string = cosmos.outputs.endpoint
