metadata description = 'Resources for the distributed-lock sample: the interactive web front end on App Service (keyless, via managed identity) over a serverless, keyless Azure Cosmos DB account. Reuses the shared web-app and secure-cosmos modules. The Locks container is TTL-enabled so an abandoned lock is released automatically; the app sets each lock record\'s own TTL. The account uses the shared module default (Session consistency); lock safety comes from optimistic concurrency (a unique-id insert plus ETag-checked renewal), not the consistency level.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs (e.g. the console app).')
param principalId string = ''

// Interactive web front end + user-assigned identity (shared module). It only touches Cosmos
// while a browser is connected, so it does not need "Always On". The Cosmos endpoint is derived
// from the account name to avoid a dependency cycle (the Cosmos account is granted access to
// this app's identity).
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
    ]
  }
}

// Serverless, keyless Cosmos DB with the sample's database/container pre-created and data-plane
// access granted to the web app's identity (and the deploying user, who can run the console app).
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
        defaultTtl: -1
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
