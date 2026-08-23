output "vnet_id" {
  description = "Resource ID of the Minibank VNet."
  value       = azurerm_virtual_network.this.id
}

output "vnet_name" {
  description = "Name of the Minibank VNet."
  value       = azurerm_virtual_network.this.name
}

output "subnet_ids" {
  description = "IDs for the key network subnets."
  value = {
    apps     = azurerm_subnet.apps.id
    appgw    = azurerm_subnet.appgw.id
    firewall = azurerm_subnet.firewall.id
    pep      = azurerm_subnet.pep.id
  }
}

output "subnet_address_prefixes" {
  description = "CIDR blocks used by each subnet."
  value = {
    apps     = azurerm_subnet.apps.address_prefixes[0]
    appgw    = azurerm_subnet.appgw.address_prefixes[0]
    firewall = azurerm_subnet.firewall.address_prefixes[0]
    pep      = azurerm_subnet.pep.address_prefixes[0]
  }
}
