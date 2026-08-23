variable "accounts_fqdn" {
  description = "FQDN of the accounts Container App."
  type        = string
}

variable "app_gateway_subnet_id" {
  description = "ID of the subnet used by the Application Gateway."
  type        = string
}

variable "app_suffix" {
  description = "Suffix applied to the Application Gateway name."
  type        = string
}

variable "location" {
  description = "Azure region for the Application Gateway."
  type        = string
}

variable "payment_fqdn" {
  description = "FQDN of the payments Container App."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group that hosts the Application Gateway."
  type        = string
}

variable "tags" {
  description = "Tags applied to the Application Gateway resources."
  type        = map(string)
}
