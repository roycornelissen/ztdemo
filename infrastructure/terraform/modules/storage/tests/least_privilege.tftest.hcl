mock_provider "azurerm" {
  mock_resource "azurerm_private_dns_zone" {
    defaults = {
      id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Network/privateDnsZones/mock.local"
    }
  }
}
mock_provider "azapi" {}

variables {
  app_suffix          = "zttest"
  resource_group_name = "rg-zttest"
  location            = "westeurope"
  tags                = {}
  vnet_id             = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Network/virtualNetworks/vnet-test"
  pep_subnet_id       = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Network/virtualNetworks/vnet-test/subnets/pep"
  identity_principal_ids = {
    accounts_api = "00000000-0000-0000-0000-000000000002"
    payments_api = "00000000-0000-0000-0000-000000000003"
    processing   = "00000000-0000-0000-0000-000000000004"
  }
}

override_resource {
  target = azurerm_storage_account.this
  values = {
    id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Storage/storageAccounts/stzttest"
  }
}

override_resource {
  target = azurerm_storage_table.accounts
  values = {
    resource_manager_id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Storage/storageAccounts/stzttest/tableServices/default/tables/accounts"
  }
}

override_resource {
  target = azurerm_storage_table.transactions
  values = {
    resource_manager_id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Storage/storageAccounts/stzttest/tableServices/default/tables/transactions"
  }
}

override_resource {
  target = azurerm_storage_queue.payments
  values = {
    id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-zttest/providers/Microsoft.Storage/storageAccounts/stzttest/queueServices/default/queues/payments"
  }
}

run "workload_data_roles_are_resource_scoped" {
  command = apply

  assert {
    condition = (
      azurerm_role_assignment.payments_sender.scope == azurerm_storage_queue.payments.id &&
      azurerm_role_assignment.payments_sender.role_definition_name == "Storage Queue Data Message Sender" &&
      azurerm_role_assignment.payments_sender.principal_id == var.identity_principal_ids["payments_api"]
    )
    error_message = "PaymentsApi must only send to the payments queue."
  }

  assert {
    condition = (
      azurerm_role_assignment.processing_queue.scope == azurerm_storage_queue.payments.id &&
      azurerm_role_assignment.processing_queue.role_definition_name == "Storage Queue Data Message Processor" &&
      azurerm_role_assignment.processing_queue.principal_id == var.identity_principal_ids["processing"]
    )
    error_message = "Processing must only receive/delete from the payments queue."
  }

  assert {
    condition = (
      azurerm_role_assignment.processing_table.scope == azurerm_storage_table.transactions.resource_manager_id &&
      azurerm_role_assignment.processing_table.role_definition_name == "Storage Table Data Contributor" &&
      azurerm_role_assignment.processing_table.principal_id == var.identity_principal_ids["processing"] &&
      azurerm_role_assignment.processing_accounts_reader.scope == azurerm_storage_table.accounts.resource_manager_id &&
      azurerm_role_assignment.processing_accounts_reader.role_definition_name == "Storage Table Data Reader" &&
      azurerm_role_assignment.processing_accounts_reader.principal_id == var.identity_principal_ids["processing"]
    )
    error_message = "Processing may write transactions but can only read current account ownership."
  }

  assert {
    condition = (
      azurerm_role_assignment.accounts_reader.scope == azurerm_storage_table.accounts.resource_manager_id &&
      azurerm_role_assignment.accounts_reader.role_definition_name == "Storage Table Data Reader" &&
      azurerm_role_assignment.accounts_reader.principal_id == var.identity_principal_ids["accounts_api"] &&
      azurerm_role_assignment.payments_accounts_reader.scope == azurerm_storage_table.accounts.resource_manager_id &&
      azurerm_role_assignment.payments_accounts_reader.role_definition_name == "Storage Table Data Reader" &&
      azurerm_role_assignment.payments_accounts_reader.principal_id == var.identity_principal_ids["payments_api"]
    )
    error_message = "Both APIs need read-only access to the accounts table."
  }
}
