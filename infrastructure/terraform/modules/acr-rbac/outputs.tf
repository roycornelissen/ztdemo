output "acr_id" {
  description = "ID of the existing ACR used by the container apps."
  value       = data.azurerm_container_registry.this.id
}
