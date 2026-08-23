variable "accounts_client_secret" {
  description = "Shared secret used by the accounts identity."
  type        = string
  sensitive   = true
}

variable "app_suffix" {
  description = "Suffix applied to the Key Vault name."
  type        = string
}

variable "current_user_object_id" {
  description = "Current Azure AD object ID to assign Key Vault access."
  type        = string
  default     = ""
}

variable "identity_principal_ids" {
  description = "Principal IDs of the managed identities that should read Key Vault secrets."
  type        = map(string)
}

variable "location" {
  description = "Azure region for the Key Vault resources."
  type        = string
}

variable "payments_client_secret" {
  description = "Shared secret used by the payments identity."
  type        = string
  sensitive   = true
}

variable "pep_subnet_id" {
  description = "Subnet used for the private endpoint."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group that will contain the Key Vault resources."
  type        = string
}

variable "tags" {
  description = "Resource tags for the Key Vault resources."
  type        = map(string)
}

variable "vnet_id" {
  description = "ID of the VNet that will link the private DNS zone."
  type        = string
}
