variable "app_suffix" {
  description = "Suffix applied to resource names."
  type        = string
}

variable "location" {
  description = "Azure region for the identities."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group containing the managed identities."
  type        = string
}

variable "tags" {
  description = "Resource tags applied to the managed identities."
  type        = map(string)
}
