data "azurerm_container_registry" "this" {
  name                = var.acr_name
  resource_group_name = var.acr_resource_group_name
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = data.azurerm_container_registry.this.id
  role_definition_name = "AcrPull"
  principal_id         = var.container_user_principal_id
}
