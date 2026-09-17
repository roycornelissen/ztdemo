output "acr_id" {
  description = "ID of the existing ACR used by the container apps."
  value       = data.azurerm_container_registry.this.id
}

output "login_server" {
  description = "Login server (FQDN) of the existing ACR used by the container apps."
  value       = data.azurerm_container_registry.this.login_server
}
