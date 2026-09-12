variable "resource_group_name" {
  description = "Name of the resource group that hosts the Minibank deployment."
  type        = string
  default     = "rg-roy-ztdemo-minibank"
}

variable "location" {
  description = "Azure region for the Minibank resources."
  type        = string
  default     = "westeurope"
}

variable "app_suffix" {
  description = "Suffix applied to resource names to keep them unique within the subscription."
  type        = string
  default     = "minibank"
}

variable "tags" {
  description = "Default resource tags applied throughout the environment."
  type        = map(string)
  default = {
    Application = "Zero-Trust Minibank Payments"
    Environment = "DEMO"
    Owner       = "Roy Cornelissen"
    Purpose     = "Conference demo"
  }
}

variable "payments_client_secret" {
  description = "Client secret used by the payments API."
  type        = string
  sensitive   = true
}

variable "accounts_client_secret" {
  description = "Client secret used by the accounts API."
  type        = string
  sensitive   = true
}

variable "current_user_object_id" {
  description = "Object ID of the user deploying the stack; used for Key Vault access and ownership assignment."
  type        = string
  default     = ""
}

variable "allowed_urls" {
  description = "FQDNs that the firewall allows outbound HTTPS access to."
  type        = list(string)
  default = [
    "open.er-api.com",
    "minibank.azurecr.io",
    "login.microsoftonline.com",
    "login.windows.net",
    "sts.windows.net",
    "mcr.microsoft.com"
  ]
}

variable "acr_name" {
  description = "Name of the existing Azure Container Registry that hosts the Minibank images."
  type        = string
  default     = "royztdemo"
}

variable "acr_resource_group_name" {
  description = "Resource group that contains the existing Azure Container Registry."
  type        = string
  default     = "rg-roy-ztdemo-tf"
}
