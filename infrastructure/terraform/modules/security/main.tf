resource "azurerm_network_security_group" "container_apps" {
  name                = "nsg-containerapps"
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_network_security_rule" "appgw_ingress" {
  name                        = "AllowAppGwIngress"
  priority                    = 100
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "AzureLoadBalancer"
  source_port_range           = "*"
  destination_address_prefix  = "*"
  destination_port_range      = "443"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.container_apps.name
}

resource "azurerm_network_security_rule" "apps_to_pex_443" {
  name                        = "AllowToPrivateEndpoints443"
  priority                    = 101
  direction                   = "Outbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = var.app_subnet_address_space
  source_port_range           = "*"
  destination_address_prefix  = var.pep_subnet_address_space
  destination_port_range      = "443"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.container_apps.name
}

resource "azurerm_network_security_group" "private_endpoints" {
  name                = "nsg-privateendpoints"
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_network_security_rule" "app_subnet_to_pep" {
  name                        = "AllowFromAppSubnet443"
  priority                    = 100
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = var.app_subnet_address_space
  source_port_range           = "*"
  destination_address_prefix  = var.pep_subnet_address_space
  destination_port_range      = "443"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.private_endpoints.name
}

resource "azurerm_subnet_network_security_group_association" "apps" {
  subnet_id                 = var.app_subnet_id
  network_security_group_id = azurerm_network_security_group.container_apps.id
}

resource "azurerm_subnet_network_security_group_association" "private_endpoints" {
  subnet_id                 = var.pep_subnet_id
  network_security_group_id = azurerm_network_security_group.private_endpoints.id
}

resource "azurerm_public_ip" "firewall" {
  name                = "pip-minibank-firewall"
  location            = var.location
  resource_group_name = var.resource_group_name
  allocation_method   = "Static"
  sku                 = "Standard"
  zones               = ["1"]
}

resource "azurerm_firewall_policy" "this" {
  name                = "afwp-minibank"
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_firewall" "this" {
  name                = "afw-minibank"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku_name            = "AZFW_VNet"
  sku_tier            = "Standard"
  firewall_policy_id  = azurerm_firewall_policy.this.id

  ip_configuration {
    name                 = "afw-ip-config"
    subnet_id            = var.firewall_subnet_id
    public_ip_address_id = azurerm_public_ip.firewall.id
  }
}

resource "azurerm_firewall_policy_rule_collection_group" "this" {
  name               = "NetworkRuleCollectionGroup"
  firewall_policy_id = azurerm_firewall_policy.this.id
  priority           = 100

  application_rule_collection {
    name     = "allowed_urls"
    priority = 100
    action   = "Allow"

    rule {
      name              = "allowed_url_external"
      description       = "Allow outbound access for required external endpoints"
      source_addresses  = [var.app_subnet_address_space]
      destination_fqdns = concat(var.allowed_urls, ["open.er-api.com", "minibank.azurecr.io", "login.microsoftonline.com", "login.windows.net", "sts.windows.net", "mcr.microsoft.com"])

      protocols {
        type = "Https"
        port = 443
      }
    }
  }
}

resource "azurerm_route_table" "firewall" {
  name                = "rt-firewall"
  location            = var.location
  resource_group_name = var.resource_group_name

  route {
    name                   = "internet-via-fw"
    address_prefix         = "0.0.0.0/0"
    next_hop_type          = "VirtualAppliance"
    next_hop_in_ip_address = azurerm_firewall.this.ip_configuration[0].private_ip_address
  }
}

resource "azurerm_subnet_route_table_association" "apps" {
  subnet_id      = var.app_subnet_id
  route_table_id = azurerm_route_table.firewall.id
}
