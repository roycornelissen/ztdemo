output "public_ip_address" {
  description = "Public IP address of the Application Gateway."
  value       = azurerm_public_ip.this.ip_address
}

output "gateway_id" {
  description = "Resource ID of the Application Gateway."
  value       = azurerm_application_gateway.this.id
}
