output "ids" {
  description = "Resource IDs for the managed identities."
  value = {
    accounts_api   = azurerm_user_assigned_identity.accounts_api.id
    payments_api   = azurerm_user_assigned_identity.payments_api.id
    processing     = azurerm_user_assigned_identity.processing.id
    container_user = azurerm_user_assigned_identity.container_user.id
  }
}

output "principal_ids" {
  description = "Principal IDs for the managed identities."
  value = {
    accounts_api   = azurerm_user_assigned_identity.accounts_api.principal_id
    payments_api   = azurerm_user_assigned_identity.payments_api.principal_id
    processing     = azurerm_user_assigned_identity.processing.principal_id
    container_user = azurerm_user_assigned_identity.container_user.principal_id
  }
}
