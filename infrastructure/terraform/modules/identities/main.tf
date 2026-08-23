resource "azurerm_user_assigned_identity" "accounts_api" {
  name                = "id-accounts-api"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_user_assigned_identity" "payments_api" {
  name                = "id-payments-api"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_user_assigned_identity" "processing" {
  name                = "id-processing"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_user_assigned_identity" "container_user" {
  name                = "id-containeruser-${var.app_suffix}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
