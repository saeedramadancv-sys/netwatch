// NetWatch on Azure: App Service for the container, Azure Cache for Redis for the
// distributed cache, Azure SQL for persistence.
//
// The application reads all three through configuration, so this template is the only
// place the topology is described. Nothing here is referenced from application code.

@description('Base name for every resource. Must be globally unique for the web app.')
@minLength(3)
@maxLength(24)
param appName string

@description('Deployment region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Administrator login for the SQL logical server.')
param sqlAdminLogin string

@description('Administrator password for the SQL logical server.')
@secure()
param sqlAdminPassword string

@description('Container image to run, including tag.')
param containerImage string = 'ghcr.io/saeedramadancv-sys/netwatch:latest'

@description('JWT signing key. Startup fails outside Development when this is missing.')
@secure()
param jwtSigningKey string

var planName = '${appName}-plan'
var redisName = '${appName}-redis'
var sqlServerName = '${appName}-sql'
var databaseName = 'netwatch'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  // Linux containers require reserved: true; the portal calls the same flag "Linux".
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    // B1 is the cheapest tier that still allows always-on, which the monitoring
    // BackgroundService needs: on a tier that idles the worker out, checks stop
    // running whenever nobody has the dashboard open.
    name: 'B1'
  }
}

resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: redisName
  location: location
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    redisConfiguration: {
      // The cache holds only rebuildable query results, so evicting the coldest key
      // under pressure is strictly better than refusing writes.
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }

  resource database 'databases' = {
    name: databaseName
    location: location
    sku: {
      name: 'Basic'
      tier: 'Basic'
    }
  }

  // Azure-internal traffic only. The 0.0.0.0 pseudo-rule is the documented way to allow
  // Azure services without naming the outbound addresses App Service happens to use
  // today; it does not open the server to the public internet.
  resource allowAzure 'firewallRules' = {
    name: 'AllowAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      // Keeps the worker resident so the monitoring loop keeps ticking with no traffic.
      alwaysOn: true
      healthCheckPath: '/health'
      webSocketsEnabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Database__Provider'
          value: 'SqlServer'
        }
        {
          name: 'Cache__Enabled'
          value: 'true'
        }
        {
          // Double underscore is the configuration-provider separator for nested keys,
          // so this binds to ConnectionStrings:Redis.
          name: 'ConnectionStrings__Redis'
          value: '${redis.properties.hostName}:${redis.properties.sslPort},password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
        }
        {
          name: 'ConnectionStrings__Default'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'Jwt__SigningKey'
          value: jwtSigningKey
        }
      ]
    }
  }
}

output webAppName string = web.name
output webAppUrl string = 'https://${web.properties.defaultHostName}'
output redisHostName string = redis.properties.hostName
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
