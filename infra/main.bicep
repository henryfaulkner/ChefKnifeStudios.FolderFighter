@description('Location for all resources')
param location string = resourceGroup().location

@description('Storage account name (must be globally unique, 3-24 lowercase letters/numbers)')
param storageAccountName string = 'folderfighterstorage'

@description('Queue name for game events')
param queueName string = 'folder-fighter-events'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

// Queue Service
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// Game Events Queue
resource gameEventsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: queueName
}

// Outputs
@description('Storage account name')
output storageAccountName string = storageAccount.name

@description('Queue name')
output queueName string = gameEventsQueue.name

@description('Storage account connection string')
output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
