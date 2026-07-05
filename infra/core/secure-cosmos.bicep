metadata description = 'Shared secure Azure Cosmos DB for NoSQL account used by the design-pattern samples. Serverless, local (key) authentication disabled, with pre-created databases/containers and data-plane RBAC for the supplied principals. Plain Bicep (no external modules) so it stays small and easy to read.'

@description('Required. Name of the Cosmos DB account.')
param name string

@description('Optional. Location for the account.')
param location string = resourceGroup().location

@description('Optional. Tags to apply to the account.')
param tags object = {}

@description('Optional. SQL databases to pre-create. Shape: [{ name: string }].')
param databases array = []

@description('Optional. Containers to pre-create. Shape: [{ databaseName: string, name: string, partitionKey: string, defaultTtl: int? }]. Pre-creating them means the app identity only needs data-plane item access, not permission to create databases/containers. Set defaultTtl (seconds, or -1 for no expiry) to enable time-to-live on the container.')
param containers array = []

@description('Optional. Principal IDs (managed identities and/or users) to grant the built-in Cosmos DB Data Contributor data-plane role.')
param dataContributorPrincipalIds array = []

// Cosmos DB built-in "Data Contributor" data-plane role definition (fixed, well-known id).
var dataContributorRoleId = '00000000-0000-0000-0000-000000000002'

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: name
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    // Force Microsoft Entra ID (RBAC) authentication; the account has no usable keys.
    disableLocalAuth: true
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    // Serverless keeps the samples cheap (pay per request, no provisioned throughput).
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
}

resource sqlDatabases 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = [
  for db in databases: {
    parent: account
    name: db.name
    properties: {
      resource: {
        id: db.name
      }
    }
  }
]

resource containersResource 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = [
  for container in containers: {
    name: '${account.name}/${container.databaseName}/${container.name}'
    properties: {
      // Merge in defaultTtl only when supplied, so containers without a TTL don't
      // emit an explicit null (which Cosmos rejects). defaultTtl is in seconds,
      // or -1 for enabled with no default expiry.
      resource: union(
        {
          id: container.name
          partitionKey: {
            paths: [
              container.partitionKey
            ]
            kind: 'Hash'
          }
        },
        container.?defaultTtl != null ? { defaultTtl: container.defaultTtl } : {}
      )
    }
    dependsOn: [
      sqlDatabases
    ]
  }
]

resource dataContributorAssignments 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = [
  for principalId in dataContributorPrincipalIds: {
    parent: account
    name: guid(account.id, principalId, dataContributorRoleId)
    properties: {
      roleDefinitionId: '${account.id}/sqlRoleDefinitions/${dataContributorRoleId}'
      principalId: principalId
      scope: account.id
    }
  }
]

@description('The document endpoint of the Cosmos DB account.')
output endpoint string = account.properties.documentEndpoint

@description('The name of the Cosmos DB account.')
output name string = account.name

@description('The resource ID of the Cosmos DB account.')
output resourceId string = account.id
