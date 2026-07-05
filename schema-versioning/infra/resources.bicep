metadata description = 'Resources for the schema-versioning sample: an App Service web app (keyless, via managed identity) over a serverless, keyless Azure Cosmos DB account. Reuses the shared web-app and secure-cosmos modules.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access for local runs.')
param principalId string = ''

// App Service web app + user-assigned identity (shared module). Plain CRUD app, so it
// does not need "Always On". The Cosmos endpoint is derived from the account name to
// avoid a dependency cycle (the Cosmos account is granted access to this app's identity).
module web '../../infra/core/web-app.bicep' = {
  name: 'web-app'
  params: {
    name: 'web-${resourceToken}'
    location: location
    tags: union(tags, { 'azd-service-name': 'web' })
    alwaysOn: false
    appSettings: [
      {
        name: 'CosmosDb__CosmosUri'
        value: 'https://cosmos-${resourceToken}.documents.azure.com:443/'
      }
    ]
  }
}

// Serverless, keyless Cosmos DB with the sample's database/container pre-created and
// data-plane access granted to the web app's identity (and the deploying user).
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'SchemaVersionDB'
      }
    ]
    containers: [
      {
        databaseName: 'SchemaVersionDB'
        name: 'ShoppingCart'
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

@description('The deployed web app URL.')
output webUrl string = web.outputs.url
