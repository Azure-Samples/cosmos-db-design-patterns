targetScope = 'subscription'

metadata description = 'Deploys the event-sourcing sample: a serverless, keyless Azure Cosmos DB account and a Consumption Function App that writes to it via managed identity.'

@minLength(1)
@description('Name of the azd environment; used to name the resource group.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Optional. Principal ID of the deploying user (provided automatically by azd), granted Cosmos data access for local runs.')
param principalId string = ''

var tags = {
  'azd-env-name': environmentName
}
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: resourceGroup
  name: 'resources'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    principalId: principalId
  }
}

output AZURE_COSMOS_ENDPOINT string = resources.outputs.cosmosEndpoint
output SERVICE_EVENTSOURCING_NAME string = resources.outputs.functionAppName
