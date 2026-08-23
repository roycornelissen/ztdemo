variable "app_suffix" {
  description = "Name suffix applied to the storage account."
  type        = string
}

variable "identity_principal_ids" {
  description = "Principal IDs for the workload identities with storage access."
  type        = map(string)
}

variable "location" {
  description = "Azure region for the storage resources."
  type        = string
}

variable "pep_subnet_id" {
  description = "Subnet used for storage private endpoints."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group that hosts the storage account."
  type        = string
}

variable "tags" {
  description = "Tags applied to the storage resources."
  type        = map(string)
}

variable "vnet_id" {
  description = "ID of the VNet that links the private DNS zones."
  type        = string
}
