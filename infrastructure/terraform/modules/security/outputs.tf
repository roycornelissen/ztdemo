output "firewall_private_ip" {
  description = "Private IP assigned to the firewall."
  value       = azurerm_firewall.this.ip_configuration[0].private_ip_address
}

output "firewall_policy_id" {
  description = "Resource ID of the firewall policy."
  value       = azurerm_firewall_policy.this.id
}
