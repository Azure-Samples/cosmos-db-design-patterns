metadata description = 'Resources for the distributed-lock console sample: a serverless, keyless Azure Cosmos DB account with the sample database/container pre-created and data-plane access granted to the deploying user. The Locks container is created with a 60-second TTL so leases auto-expire, matching the app. The account uses the shared module default (Session consistency); lock safety comes from optimistic concurrency (ETag + unique-id conditional writes), not the consistency level.'

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
        name: 'LockDB'
      }
    ]
    containers: [
      {
        databaseName: 'LockDB'
        name: 'Locks'
        partitionKey: '/id'
        defaultTtl: 60
      }
    ]
    dataContributorPrincipalIds: empty(principalId) ? [] : [ principalId ]
  }
}

@description('The Cosmos DB document endpoint.')
output cosmosEndpoint string = cosmos.outputs.endpoint
