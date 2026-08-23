resource "azurerm_container_app_environment" "this" {
  name                           = "env-${var.app_suffix}-1"
  location                       = var.location
  resource_group_name            = var.resource_group_name
  log_analytics_workspace_id     = var.log_analytics_workspace_id
  infrastructure_subnet_id       = var.app_subnet_id
  internal_load_balancer_enabled = true
  tags                           = var.tags
}

resource "azurerm_container_app" "this" {
  for_each = var.container_apps

  name                         = each.value.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [each.value.identity_id]
  }

  registry {
    server   = "minibank.azurecr.io"
    identity = var.container_user_id
  }

  ingress {
    external_enabled = true
    target_port      = each.value.target_port
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    container {
      name   = each.value.name
      image  = each.value.image
      cpu    = 0.25
      memory = "0.5Gi"

      liveness_probe {
        transport = "TCP"
        port      = each.value.target_port
      }
    }

    min_replicas = 1
    max_replicas = 2
  }
}
