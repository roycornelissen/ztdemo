resource "azurerm_storage_account" "this" {
  name                            = "st${var.app_suffix}"
  resource_group_name             = var.resource_group_name
  location                        = var.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  public_network_access_enabled   = true
  tags                            = var.tags
}

resource "azurerm_storage_table" "accounts" {
  name                 = "accounts"
  storage_account_name = azurerm_storage_account.this.name
}

resource "azurerm_storage_table" "transactions" {
  name                 = "transactions"
  storage_account_name = azurerm_storage_account.this.name
}

resource "azurerm_storage_queue" "payments" {
  name                 = "payments"
  storage_account_name = azurerm_storage_account.this.name
}

resource "azurerm_role_assignment" "payments_sender" {
  scope                = azurerm_storage_account.this.id
  role_definition_name = "Storage Queue Data Message Sender"
  principal_id         = var.identity_principal_ids["payments_api"]
}

resource "azurerm_role_assignment" "processing_queue" {
  scope                = azurerm_storage_account.this.id
  role_definition_name = "Storage Queue Data Message Processor"
  principal_id         = var.identity_principal_ids["processing"]
}

resource "azurerm_role_assignment" "processing_table" {
  scope                = azurerm_storage_account.this.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = var.identity_principal_ids["processing"]
}

resource "azurerm_role_assignment" "accounts_reader" {
  scope                = azurerm_storage_account.this.id
  role_definition_name = "Storage Table Data Reader"
  principal_id         = var.identity_principal_ids["accounts_api"]
}

resource "azurerm_private_dns_zone" "table" {
  name                = "privatelink.table.core.windows.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone" "queue" {
  name                = "privatelink.queue.core.windows.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "table" {
  name                  = "link-table"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.table.name
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
}

resource "azurerm_private_dns_zone_virtual_network_link" "queue" {
  name                  = "link-queue"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.queue.name
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
}

resource "azurerm_private_endpoint" "table" {
  name                = "pep-storage-table"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.pep_subnet_id

  private_service_connection {
    name                           = "storage-table-connection"
    private_connection_resource_id = azurerm_storage_account.this.id
    is_manual_connection           = false
    subresource_names              = ["table"]
  }

  private_dns_zone_group {
    name                 = "table-dnszonegroup"
    private_dns_zone_ids = [azurerm_private_dns_zone.table.id]
  }
}

resource "azurerm_private_endpoint" "queue" {
  name                = "pep-storage-queue"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.pep_subnet_id

  private_service_connection {
    name                           = "storage-queue-connection"
    private_connection_resource_id = azurerm_storage_account.this.id
    is_manual_connection           = false
    subresource_names              = ["queue"]
  }

  private_dns_zone_group {
    name                 = "queue-dnszonegroup"
    private_dns_zone_ids = [azurerm_private_dns_zone.queue.id]
  }
}
