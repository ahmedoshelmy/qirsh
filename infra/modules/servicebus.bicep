param name string
param location string

// Standard tier: MassTransit publishes to topics, which Basic does not support.
resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: name
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource appRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' = {
  parent: namespace
  name: 'qirsh-apps'
  properties: {
    rights: ['Listen', 'Send', 'Manage']
  }
}

#disable-next-line outputs-should-not-contain-secrets
output connectionString string = appRule.listKeys().primaryConnectionString
