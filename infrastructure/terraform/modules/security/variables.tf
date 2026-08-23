variable "allowed_urls" {
  description = "List of FQDNs allowed through the Azure Firewall policy."
  type        = list(string)
}

variable "app_subnet_address_space" {
  description = "CIDR of the app subnet."
  type        = string
}

variable "app_subnet_id" {
  description = "ID of the app subnet."
  type        = string
}

variable "app_suffix" {
  description = "Suffix applied to resource names."
  type        = string
}

variable "firewall_subnet_id" {
  description = "ID of the Azure Firewall subnet."
  type        = string
}

variable "location" {
  description = "Azure region for the security resources."
  type        = string
}

variable "pep_subnet_address_space" {
  description = "CIDR of the private-endpoint subnet."
  type        = string
}

variable "pep_subnet_id" {
  description = "ID of the private-endpoint subnet."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group of the Minibank environment."
  type        = string
}

variable "vnet_name" {
  description = "Name of the VNet for the deployment."
  type        = string
}
