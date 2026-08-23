variable "app_suffix" {
  description = "Suffix applied to resource names."
  type        = string
}

variable "location" {
  description = "Azure region for the VNet."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group of the VNet."
  type        = string
}

variable "tags" {
  description = "Tags applied to the networking resources."
  type        = map(string)
}
