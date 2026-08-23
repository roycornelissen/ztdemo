terraform {
  backend "azurerm" {
    resource_group_name  = "rg-roy-ztdemo-tf"
    storage_account_name = "stztdemocanvas"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"
    use_azuread_auth     = true
  }
}
