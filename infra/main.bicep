// Qirsh on Azure: Container Apps (consumption) + Postgres Flexible Server + Service Bus.
// Optional and manual: `az deployment group create -g <rg> -f infra/main.bicep -p postgresAdminPassword=...`
targetScope = 'resourceGroup'

@description('Prefix for resource names.')
param name string = 'qirsh'

param location string = resourceGroup().location

@description('Registry and tag of the images built by `docker compose build` and pushed by you.')
param imageRegistry string = 'ghcr.io/example'
param imageTag string = 'latest'

param postgresAdminLogin string = 'qirshadmin'

@secure()
param postgresAdminPassword string

module env 'modules/environment.bicep' = {
  name: 'environment'
  params: {
    name: name
    location: location
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: '${name}-pg-${uniqueString(resourceGroup().id)}'
    location: location
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    name: '${name}-sb-${uniqueString(resourceGroup().id)}'
    location: location
  }
}

var postgresConnection = 'Host=${postgres.outputs.fqdn};Database=qirsh;Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require'

var services = [
  { name: 'ingestion', port: 5101, external: false }
  { name: 'ledger', port: 5201, external: false }
  { name: 'budgets', port: 5301, external: false }
]

module apps 'modules/containerapp.bicep' = [for s in services: {
  name: 'app-${s.name}'
  params: {
    name: '${name}-${s.name}'
    location: location
    environmentId: env.outputs.environmentId
    image: '${imageRegistry}/qirsh-${s.name}:${imageTag}'
    targetPort: s.port
    external: s.external
    secrets: [
      { name: 'postgres', value: postgresConnection }
      { name: 'servicebus', value: serviceBus.outputs.connectionString }
    ]
    env: [
      { name: 'ConnectionStrings__Postgres', secretRef: 'postgres' }
      { name: 'ConnectionStrings__ServiceBus', secretRef: 'servicebus' }
      { name: 'Messaging__Transport', value: 'AzureServiceBus' }
      { name: 'Services__LedgerGrpc', value: 'http://${name}-ledger:5202' }
    ]
  }
}]

module web 'modules/containerapp.bicep' = {
  name: 'app-web'
  params: {
    name: '${name}-web'
    location: location
    environmentId: env.outputs.environmentId
    image: '${imageRegistry}/qirsh-web:${imageTag}'
    targetPort: 8080
    external: true
    secrets: []
    env: []
  }
}

output webUrl string = web.outputs.url
