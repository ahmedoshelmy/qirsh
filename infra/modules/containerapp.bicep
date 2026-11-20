param name string
param location string
param environmentId string
param image string
param targetPort int
param external bool

param secrets array
param env array

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: external
        targetPort: targetPort
        transport: 'auto'
      }
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: name
          image: image
          env: env
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 1 }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
