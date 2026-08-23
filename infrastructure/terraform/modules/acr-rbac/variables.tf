variable "acr_name" {
  description = "Name of the existing Azure Container Registry."
  type        = string
}

variable "acr_resource_group_name" {
  description = "Resource group containing the existing Azure Container Registry."
  type        = string
}

variable "container_user_principal_id" {
  description = "Principal ID of the container image pull identity."
  type        = string
}
