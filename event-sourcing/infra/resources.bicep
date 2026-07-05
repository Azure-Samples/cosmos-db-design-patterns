metadata description = 'Resources for the event-sourcing sample: a Flex Consumption Function App (system-assigned identity, keyless) that writes to a serverless, keyless Azure Cosmos DB account. Uses managed identity for both Cosmos and storage so no keys or connection strings are stored.'

@description('Location for all resources.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Short unique token used to name resources.')
param resourceToken string

@description('Optional. Principal ID of the user running the deployment, granted Cosmos data access (local runs) and blob access (to publish the app package).')
param principalId string = ''

// Well-known built-in role definition IDs.
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

var deploymentContainerName = 'deployments'

// Storage account required by the Functions runtime. Shared-key auth is disabled;
// access is via managed identity only.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
  }

  resource blobServices 'blobServices' = {
    name: 'default'

    resource deploymentContainer 'containers' = {
      name: deploymentContainerName
    }
  }
}

// Flex Consumption plan: scales to zero, pay-per-execution (cheap).
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${resourceToken}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-${resourceToken}'
  location: location
  // azd uses this tag to deploy the matching service's code to this resource.
  tags: union(tags, { 'azd-service-name': 'eventsourcing' })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      appSettings: [
        // Identity-based storage for the Functions runtime (no keys).
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
        }
        // Keyless Cosmos DB access via the app's system-assigned managed identity.
        // The endpoint is derived from the account name to avoid a dependency cycle
        // (the Cosmos account is granted access to this app's identity).
        {
          name: 'CosmosDBConnection__accountEndpoint'
          value: 'https://cosmos-${resourceToken}.documents.azure.com:443/'
        }
        {
          name: 'CosmosDBConnection__credential'
          value: 'managedidentity'
        }
      ]
    }
  }
}

// The Function App's identity needs blob data access for the runtime and deployment container.
resource functionStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, functionApp.id, storageBlobDataOwnerRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// The deploying user needs blob access to upload the application package (shared key is disabled).
resource userStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  scope: storage
  name: guid(storage.id, principalId, storageBlobDataContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: principalId
    principalType: 'User'
  }
}

// Serverless, keyless Cosmos DB with the sample's database/container pre-created and
// data-plane access granted to the function's identity (and the deploying user).
module cosmos '../../infra/core/secure-cosmos.bicep' = {
  name: 'secure-cosmos'
  params: {
    name: 'cosmos-${resourceToken}'
    location: location
    tags: tags
    databases: [
      {
        name: 'EventSourcingDB'
      }
    ]
    containers: [
      {
        databaseName: 'EventSourcingDB'
        name: 'CartEvents'
        partitionKey: '/CartId'
      }
    ]
    dataContributorPrincipalIds: empty(principalId)
      ? [ functionApp.identity.principalId ]
      : [ functionApp.identity.principalId, principalId ]
  }
}

@description('The Cosmos DB document endpoint.')
output cosmosEndpoint string = cosmos.outputs.endpoint

@description('The deployed Function App name.')
output functionAppName string = functionApp.name
