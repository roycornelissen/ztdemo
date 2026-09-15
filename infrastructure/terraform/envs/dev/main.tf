resource "azurerm_resource_group" "this" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

module "identities" {
  source = "../../modules/identities"

  app_suffix          = var.app_suffix
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  tags                = var.tags
}

module "networking" {
  source = "../../modules/networking"

  app_suffix          = var.app_suffix
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  tags                = var.tags
}

module "monitoring" {
  source = "../../modules/monitoring"

  app_suffix          = var.app_suffix
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  tags                = var.tags
}

# Leave firewall behind for now to avoid high costs
# module "security" {
#   source = "../../modules/security"

#   allowed_urls             = var.allowed_urls
#   app_subnet_address_space = module.networking.subnet_address_prefixes["apps"]
#   app_subnet_id            = module.networking.subnet_ids["apps"]
#   app_suffix               = var.app_suffix
#   firewall_subnet_id       = module.networking.subnet_ids["firewall"]
#   location                 = var.location
#   pep_subnet_address_space = module.networking.subnet_address_prefixes["pep"]
#   pep_subnet_id            = module.networking.subnet_ids["pep"]
#   resource_group_name      = azurerm_resource_group.this.name
#   vnet_name                = module.networking.vnet_name
# }

module "storage" {
  source = "../../modules/storage"

  app_suffix             = var.app_suffix
  identity_principal_ids = module.identities.principal_ids
  location               = var.location
  pep_subnet_id          = module.networking.subnet_ids["pep"]
  resource_group_name    = azurerm_resource_group.this.name
  tags                   = var.tags
  vnet_id                = module.networking.vnet_id
}

module "keyvault" {
  source = "../../modules/keyvault"

  accounts_client_secret = var.accounts_client_secret
  app_suffix             = var.app_suffix
  current_user_object_id = var.current_user_object_id
  identity_principal_ids = module.identities.principal_ids
  location               = var.location
  payments_client_secret = var.payments_client_secret
  pep_subnet_id          = module.networking.subnet_ids["pep"]
  resource_group_name    = azurerm_resource_group.this.name
  tags                   = var.tags
  vnet_id                = module.networking.vnet_id
}

module "acr_rbac" {
  source = "../../modules/acr-rbac"

  acr_name                    = var.acr_name
  acr_resource_group_name     = var.acr_resource_group_name
  container_user_principal_id = module.identities.principal_ids["container_user"]
}

module "container_platform" {
  source = "../../modules/container-platform"

  app_suffix    = var.app_suffix
  app_subnet_id = module.networking.subnet_ids["apps"]
  container_apps = {
    accounts_api = {
      name        = "ca-accountapi-${var.app_suffix}"
      image       = "minibank.azurecr.io/minibank/accounts:latest"
      target_port = 8080
      identity_id = module.identities.ids["accounts_api"]
    }
    payments_api = {
      name        = "ca-payments"
      image       = "minibank.azurecr.io/minibank/payments:latest"
      target_port = 8080
      identity_id = module.identities.ids["payments_api"]
    }
    processing = {
      name        = "ca-processing"
      image       = "minibank.azurecr.io/minibank/processing:latest"
      target_port = 8080
      identity_id = module.identities.ids["processing"]
    }
  }
  container_user_id          = module.identities.ids["container_user"]
  location                   = var.location
  log_analytics_workspace_id = module.monitoring.workspace_id
  resource_group_name        = azurerm_resource_group.this.name
  tags                       = var.tags
}

# Leave app gateway behind for now to avoid high costs
# module "appgateway" {
#   source = "../../modules/appgateway"

#   accounts_fqdn         = module.container_platform.container_app_fqdns["accounts_api"]
#   app_gateway_subnet_id = module.networking.subnet_ids["appgw"]
#   app_suffix            = var.app_suffix
#   location              = var.location
#   payment_fqdn          = module.container_platform.container_app_fqdns["payments_api"]
#   resource_group_name   = azurerm_resource_group.this.name
#   tags                  = var.tags
# }
