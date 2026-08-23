output "container_app_environment_name" {
  description = "Name of the Container Apps environment."
  value       = azurerm_container_app_environment.this.name
}

output "container_app_fqdns" {
  description = "FQDNs for the deployed Container Apps."
  value = {
    for key, app in azurerm_container_app.this : key => app.latest_revision_fqdn
  }
}
