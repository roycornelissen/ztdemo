@description('The name of the App Gateway that will be deployed')
param appGatewayName string

@description('The name of the IP address that will be deployed')
param ipAddressName string

@description('The subnet ID that will be used for the App Gateway configuration')
param subnetId string

@description('The FQDN of for Pool 1')
param pool1_fqdn string

@description('The Path of for Pool 1')
param pool1_path string

@description('The FQDN of for Pool 2')
param pool2_fqdn string

@description('The Path of for Pool 2')
param pool2_path string

@description('The location where the App Gateway will be deployed')
param location string

@description('The tags that will be applied to the App Gateway')
param tags object

resource appGateway 'Microsoft.Network/applicationGateways@2024-07-01' = {
  name: appGatewayName
  location: location
  tags: tags
  zones: [
    '1'
  ]
  properties: {
    sku: {
      tier: 'Standard_v2'
      capacity: 1
      name: 'Standard_v2'
    }
    gatewayIPConfigurations: [
      { 
        name: 'appgateway-subnet'
        properties: {
          subnet: {
            id: subnetId
          }
        }
      }
    ]
    frontendIPConfigurations: [
      { 
        name: 'my-frontend'
        properties: {
          publicIPAddress: {
            id: publicIp.id
          }
        }
      }
    ]
    privateLinkConfigurations: [
      { 
        name: 'my-agw-private-link'

        properties: {
          ipConfigurations: [
            { 
              name: 'privateLinkIpConfig'
              properties: {
                primary: true
                privateIPAllocationMethod: 'Dynamic'
                subnet: {
                  id: subnetId
                }
              }
            }
          ]
        }
      }
    ]
    frontendPorts: [
      { 
        name: 'port_80'
        properties: {
          port: 80
        }
      }
    ]
    probes: [
      {
        name: 'pool1-health-probe'
        properties: {
          protocol: 'Https'
          host: pool1_fqdn
          path: '/healthz'
          interval: 30
          timeout: 30
          unhealthyThreshold: 3
          match: {
            statusCodes: [
              '200-399'
            ]
          }
          pickHostNameFromBackendHttpSettings: false
        }
      }
      {
        name: 'pool2-health-probe'
        properties: {
          protocol: 'Https'
          host: pool2_fqdn
          path: '/healthz'
          interval: 30
          timeout: 30
          unhealthyThreshold: 3
          match: {
            statusCodes: [
              '200-399'
            ]
          }
          pickHostNameFromBackendHttpSettings: false
        }
      }
    ]
    backendAddressPools: [
      { 
        name: 'root-pool'
        properties: {
          backendAddresses: [
            { 
              fqdn: 'hmpg.net'
            }
          ]
        }
      }
      {
        name: 'pool_1'
        properties: {
          backendAddresses: [
            { 
              fqdn: pool1_fqdn
            }
          ]
        }
      }
      {
        name: 'pool_2'
        properties: {
          backendAddresses: [
            { 
              fqdn: pool2_fqdn
            }
          ]
        }
      }
    ]
    backendHttpSettingsCollection: [
      { 
        name: 'pool1-backend-setting'
        properties: {
          protocol: 'Https'
          port: 443
          cookieBasedAffinity: 'Disabled'
          requestTimeout: 20
          pickHostNameFromBackendAddress: false
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', appGatewayName, 'pool1-health-probe')
          }
        }
      }
      { 
        name: 'pool2-backend-setting'
        properties: {
          protocol: 'Https'
          port: 443
          cookieBasedAffinity: 'Disabled'
          requestTimeout: 20
          pickHostNameFromBackendAddress: false
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', appGatewayName, 'pool2-health-probe')
          }
        }
      }
    ]
    httpListeners: [
      { 
        name: 'my-agw-listener'
        properties: {
          frontendIPConfiguration: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendIPConfigurations', appGatewayName, 'my-frontend')
          }
          frontendPort: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', appGatewayName, 'port_80')
          }
          protocol: 'Http'
        }
      }
    ]
    urlPathMaps: [
      { 
        name: 'agw-url-path-map'
        properties: {
          defaultBackendAddressPool: {
            id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', appGatewayName, 'root-pool')
          }
          defaultBackendHttpSettings: {
            id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', appGatewayName, 'pool1-backend-setting')
          }
          pathRules: [
            { 
              name: 'rule1'
              properties: {
                paths: ['${pool1_path}/*']
                backendAddressPool: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', appGatewayName, 'pool_1')
                }
                backendHttpSettings: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', appGatewayName, 'pool1-backend-setting')
                }
              }
            }
            { 
              name: 'rule2'
              properties: {
                paths: ['${pool2_path}/*']
                backendAddressPool: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', appGatewayName, 'pool_2')
                }
                backendHttpSettings: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', appGatewayName, 'pool2-backend-setting')
                }
              }
            }
          ]
        }
      }
    ]
    requestRoutingRules: [
      { 
        name: 'my-agw-routing-rule'
        properties: {
          priority: 1
          ruleType: 'PathBasedRouting'
          httpListener: {
            id: resourceId('Microsoft.Network/applicationGateways/httpListeners', appGatewayName, 'my-agw-listener')
          }
          urlPathMap: {
            id: resourceId('Microsoft.Network/applicationGateways/urlPathMaps', appGatewayName, 'agw-url-path-map')
          }
        }
      }
    ]
    enableHttp2: true
  }
}

resource publicIp 'Microsoft.Network/publicIPAddresses@2024-07-01' = {
  name: ipAddressName
  location: location
  sku: {
    name: 'Standard'
  }
  zones: [
    '1'
  ]
  properties: {
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Static'
  }
}
