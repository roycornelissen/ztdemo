provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "example" {
  name     = "gohttpbin-rg"
  location = "westeurope"
}

resource "azurerm_virtual_network" "example" {
  name                = "gohttpbin-vnet"
  location            = azurerm_resource_group.example.location
  resource_group_name = azurerm_resource_group.example.name
  address_space       = ["10.0.0.0/16"]
}

resource "azurerm_subnet" "app" {
  name                 = "app-subnet"
  resource_group_name  = azurerm_resource_group.example.name
  virtual_network_name = azurerm_virtual_network.example.name
  address_prefixes     = [ "10.0.1.0/24" ]
  delegation {
    name = "Microsoft.App/environments"
    service_delegation {
      name = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

resource "azurerm_subnet" "gateway" {
  name                 = "gateway-subnet"
  resource_group_name  = azurerm_resource_group.example.name
  virtual_network_name = azurerm_virtual_network.example.name
  address_prefixes     = [ "10.0.2.0/24" ]
}

resource "azurerm_subnet" "firewall" {
  name                 = "AzureFirewallSubnet"
  resource_group_name  = azurerm_resource_group.example.name
  virtual_network_name = azurerm_virtual_network.example.name
  address_prefixes     = [ "10.0.3.0/24" ]
}

resource "azurerm_public_ip" "firewall" {
  name                = "gohttpbin-fw-ip"
  location            = azurerm_resource_group.example.location
  resource_group_name = azurerm_resource_group.example.name
  sku                 = "Standard"
  allocation_method   = "Static"
}

resource "azurerm_firewall" "example" {
  name                = "gohttpbin-fw"
  location            = azurerm_resource_group.example.location
  resource_group_name = azurerm_resource_group.example.name
  sku_tier            = "Basic"
  sku_name            = "AZFW_VNet"

  ip_configuration {
    name                 = "fw-config"
    public_ip_address_id = azurerm_public_ip.firewall.id
    subnet_id            = azurerm_subnet.firewall.id
  }
}

resource "azurerm_firewall_policy" "example" {
  name                = "gohttpbin-fw-policy"
  location            = azurerm_resource_group.example.location
  resource_group_name = azurerm_resource_group.example.name
}

resource "azurerm_firewall_policy_rule_collection_group" "example" {
  name                = "NetworkRuleCollectionGroup"
  firewall_policy_id  = azurerm_firewall_policy.example.id
  priority            = 100

  application_rule_collection {
    name     = "allowed_urls"
    priority = 100
    action   = "Allow"

    rule {
      name                  = "allowed_urls"
      source_addresses      = ["10.0.0.0/16"]
      destination_fqdns     = ["httpbin.org"]
    protocols {
        type = "Http"
        port = 80
    }
    protocols {
        type = "Https"
        port = 443
    }
  }
}
}

resource "azurerm_container_group" "example" {
  name                = "gohttpbin"
  location            = azurerm_resource_group.example.location
  resource_group_name = azurerm_resource_group.example.name
  os_type             = "Linux"

  container {
    name   = "gohttpbin"
    image  = "kennethreitz/httpbin"
    cpu    = "0.5"
    memory = "1"
    ports {
      port     = 80
      protocol = "TCP"
    }
  }

  ip_address_type = "Public"
  dns_name_label  = "gohttpbin"
}
