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
    identity_ids = [each.value.identity_id, var.container_user_id]
  }

  # Dapr is intentionally not configured: omitting the `dapr` block keeps the
  # Dapr sidecar (and its additional attack surface / inter-app API) disabled.

  registry {
    server   = var.registry_server
    identity = var.container_user_id
  }

  ingress {
    external_enabled           = each.value.external_enabled
    target_port                = each.value.target_port
    transport                  = "auto"
    allow_insecure_connections = false

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

      # Reduce runtime attack surface: disable in-process diagnostics/debugging endpoints
      env {
        name  = "DOTNET_EnableDiagnostics"
        value = "0"
      }
      env {
        name  = "DOTNET_EnableEventPipe"
        value = "0"
      }

      dynamic "env" {
        for_each = each.value.environment_variables
        content {
          name  = env.key
          value = env.value
        }
      }

      liveness_probe {
        transport = "TCP"
        port      = each.value.target_port
      }

      readiness_probe {
        transport = "TCP"
        port      = each.value.target_port
      }
    }

    min_replicas = 1
    max_replicas = 2
  }
}

# AzureRM does not expose identitySettings. Keep the app and registry in AzureRM
# and manage only the identity lifecycle settings through AzAPI.
resource "azapi_update_resource" "identity_settings" {
  for_each = var.container_apps

  type        = "Microsoft.App/containerApps@2025-07-01"
  resource_id = azurerm_container_app.this[each.key].id

  list_unique_id_property = {
    "properties.configuration.identitySettings" = "identity"
  }

  body = {
    properties = {
      configuration = {
        identitySettings = [
          {
            identity  = var.container_user_id
            lifecycle = "None"
          },
          {
            identity  = each.value.identity_id
            lifecycle = "Main"
          },
        ]
      }
    }
  }
}
