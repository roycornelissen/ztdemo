param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$storage,
    [Parameter(Mandatory=$true)][string]$vnet,
    [Parameter(Mandatory=$true)][string]$subnetPep
)

Write-Host "Creating storage account with tables and queues..."

# Create storage account with public network access enabled (default-action Deny)
az storage account create `
  --name $storage `
  --resource-group $rg `
  --location $location `
  --sku Standard_LRS `
  --kind StorageV2 `
  --allow-blob-public-access false `
  --min-tls-version TLS1_2 `
  --default-action Allow `
  --public-network-access Enabled

# Get storage account key
$storageKey = az storage account keys list --account-name $storage --resource-group $rg --query [0].value -o tsv

# Create Table 'accounts'
az storage table create `
  --name accounts `
  --account-name $storage `
  --account-key $storageKey

  # Create Table 'transactions'
az storage table create `
  --name transactions `
  --account-name $storage `
  --account-key $storageKey

# Create Queue 'payments'
az storage queue create `
  --name payments `
  --account-name $storage `
  --account-key $storageKey

# After resources are created, disable public network access again
az storage account update `
  --name $storage `
  --resource-group $rg `
  --default-action Deny `
  --public-network-access Disabled

$storageId = az storage account show --name $storage --resource-group $rg --query id -o tsv

$paymentsUmi = az identity show --name id-payments-api --resource-group $rg --query 'principalId' -o tsv

az role assignment create `
  --assignee $paymentsUmi `
  --role "Storage Queue Data Message Sender" `
  --scope $storageId

$processingUmi = az identity show --name id-processing --resource-group $rg --query 'principalId' -o tsv

az role assignment create `
  --assignee $processingUmi `
  --role "Storage Queue Data Message Processor" `
  --scope $storageId

az role assignment create `
  --assignee $processingUmi `
  --role "Storage Table Data Contributor" `
  --scope $storageId

$accountsUmi = az identity show --name id-accounts-api --resource-group $rg --query 'principalId' -o tsv

az role assignment create `
  --assignee $accountsUmi `
  --role "Storage Table Data Reader" `
  --scope $storageId

Write-Host "Creating private endpoints for storage tables and queues..."

$storageId = az storage account show --name $storage --resource-group $rg --query id -o tsv

az network private-endpoint create `
  --name pep-storage-table `
  --resource-group $rg `
  --vnet-name $vnet `
  --subnet $subnetPep `
  --private-connection-resource-id $storageId `
  --group-id table `
  --connection-name mystorage-table-pe `
  --location $location

az network private-endpoint create `
  --name pep-storage-queue `
  --resource-group $rg `
  --vnet-name $vnet `
  --subnet $subnetPep `
  --private-connection-resource-id $storageId `
  --group-id queue `
  --connection-name mystorage-queue-pe `
  --location $location

az network private-dns zone create --resource-group $rg --name "privatelink.table.core.windows.net"
az network private-dns zone create --resource-group $rg --name "privatelink.queue.core.windows.net"

az network private-dns link vnet create `
  --resource-group $rg `
  --virtual-network $vnet `
  --zone-name "privatelink.table.core.windows.net" `
  --name link-table `
  --registration-enabled false

az network private-dns link vnet create `
  --resource-group $rg `
  --virtual-network $vnet `
  --zone-name "privatelink.queue.core.windows.net" `
  --name link-queue `
  --registration-enabled false

az network private-endpoint dns-zone-group create `
  --resource-group $rg `
  --endpoint-name pep-storage-table `
  --name table-dnszonegroup `
  --private-dns-zone "privatelink.table.core.windows.net" `
  --zone-name table-dnszone

az network private-endpoint dns-zone-group create `
  --resource-group $rg `
  --endpoint-name pep-storage-queue `
  --name queue-dnszonegroup `
  --private-dns-zone "privatelink.queue.core.windows.net" `
  --zone-name queue-dnszone

