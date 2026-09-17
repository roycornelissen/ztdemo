variable "app_suffix" {
  description = "Suffix used to build the Container Apps Environment name."
  type        = string
}

variable "app_subnet_id" {
  description = "Subnet used for VNet-injected Container Apps."
  type        = string
}

variable "container_apps" {
  description = "Map of Container Apps to create, keyed by logical name."
  type = map(object({
    name             = string
    image            = string
    target_port      = number
    identity_id      = string
    external_enabled = optional(bool, true)
  }))
}

variable "container_user_id" {
  description = "Resource ID used to configure ACR pull for the apps."
  type        = string
}

variable "registry_server" {
  description = "Login server (FQDN) of the ACR that hosts the container images."
  type        = string
}

variable "location" {
  description = "Azure region for the Container App environment."
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace used by the Container Apps environment."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Container Apps deployment."
  type        = string
}

variable "tags" {
  description = "Tags applied to the Container Apps resources."
  type        = map(string)
}
