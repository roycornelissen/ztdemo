variable "app_suffix" {
  description = "Suffix used to produce the Log Analytics workspace name."
  type        = string
}

variable "location" {
  description = "Azure region for the Log Analytics workspace."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group of the Minibank workload."
  type        = string
}

variable "tags" {
  description = "Tags applied to the workspace."
  type        = map(string)
}
