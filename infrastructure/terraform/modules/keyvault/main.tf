data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                          = "kv-${var.app_suffix}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  purge_protection_enabled      = false
  rbac_authorization_enabled    = true
  public_network_access_enabled = true
  tags                          = var.tags
}

resource "azurerm_role_assignment" "current_user" {
  count = var.current_user_object_id == "" ? 0 : 1

  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.current_user_object_id
}

resource "azurerm_role_assignment" "identity_readers" {
  for_each = var.identity_principal_ids

  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value
}

resource "azurerm_private_dns_zone" "this" {
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "this" {
  name                  = "link-keyvault"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.this.name
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
}

resource "azurerm_private_endpoint" "this" {
  name                = "pep-${azurerm_key_vault.this.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.pep_subnet_id

  private_service_connection {
    name                           = "${azurerm_key_vault.this.name}-connection"
    private_connection_resource_id = azurerm_key_vault.this.id
    is_manual_connection           = false
    subresource_names              = ["vault"]
  }

  private_dns_zone_group {
    name                 = "keyvault-dnszonegroup"
    private_dns_zone_ids = [azurerm_private_dns_zone.this.id]
  }
}
