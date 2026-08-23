output "resource_group_name" {
  description = "Name of the Minibank resource group."
  value       = azurerm_resource_group.this.name
}

output "vnet_name" {
  description = "Name of the VNet created for the deployment."
  value       = module.networking.vnet_name
}

output "container_app_fqdns" {
  description = "Latest FQDNs for each deployed Container App."
  value       = module.container_platform.container_app_fqdns
}
