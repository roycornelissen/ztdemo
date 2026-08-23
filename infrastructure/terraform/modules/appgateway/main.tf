resource "azurerm_public_ip" "this" {
  name                = "pip-${var.app_suffix}-gateway"
  location            = var.location
  resource_group_name = var.resource_group_name
  allocation_method   = "Static"
  sku                 = "Standard"
  zones               = ["1"]
  tags                = var.tags
}

resource "azurerm_application_gateway" "this" {
  name                = "agw-${var.app_suffix}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags

  sku {
    name     = "Standard_v2"
    tier     = "Standard_v2"
    capacity = 2
  }

  gateway_ip_configuration {
    name      = "appGatewayIpConfig"
    subnet_id = var.app_gateway_subnet_id
  }

  frontend_ip_configuration {
    name                 = "public"
    public_ip_address_id = azurerm_public_ip.this.id
  }

  frontend_port {
    name = "http"
    port = 80
  }

  backend_address_pool {
    name  = "payments-pool"
    fqdns = [var.payment_fqdn]
  }

  backend_address_pool {
    name  = "accounts-pool"
    fqdns = [var.accounts_fqdn]
  }

  backend_http_settings {
    name                  = "payment-http"
    cookie_based_affinity = "Disabled"
    port                  = 80
    protocol              = "Http"
    request_timeout       = 60
  }

  http_listener {
    name                           = "gateway-listener"
    frontend_ip_configuration_name = "public"
    frontend_port_name             = "http"
    protocol                       = "Http"
  }

  url_path_map {
    name                               = "app-map"
    default_backend_address_pool_name  = "payments-pool"
    default_backend_http_settings_name = "payment-http"

    path_rule {
      name                       = "payment-rule"
      paths                      = ["/payment"]
      backend_address_pool_name  = "payments-pool"
      backend_http_settings_name = "payment-http"
    }

    path_rule {
      name                       = "accounts-rule"
      paths                      = ["/accounts"]
      backend_address_pool_name  = "accounts-pool"
      backend_http_settings_name = "payment-http"
    }
  }

  request_routing_rule {
    name                       = "gateway-route"
    rule_type                  = "PathBasedRouting"
    http_listener_name         = "gateway-listener"
    url_path_map_name          = "app-map"
    priority                   = 100
    backend_http_settings_name = "payment-http"
  }
}
