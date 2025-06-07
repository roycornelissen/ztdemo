param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$keyvault,
    [Parameter(Mandatory=$true)][string]$vnet,
    [Parameter(Mandatory=$true)][string]$subnetPep,
    [Parameter(Mandatory=$true)][string]$paymentsSecretName,    
    [Parameter(Mandatory=$true)][string]$paymentsSecretValue,    
    [Parameter(Mandatory=$true)][string]$accountsSecretName,    
    [Parameter(Mandatory=$true)][string]$accountsSecretValue
)

Write-Host "Creating keyvault..."

# Step 1: Create Key Vault with public access enabled
az keyvault create `
    --name $keyvault `
    --resource-group $rg `
    --location $location `
    --public-network-access Enabled `
    --sku standard

    # Assign the current user as Key Vault Secrets Officer
    $currentUser=$(az account show --query user.name -o tsv)

    az role assignment create `
        --assignee $currentUser `
        --role "Key Vault Secrets Officer" `
        --scope $(az keyvault show --name $keyvault --resource-group $rg --query id -o tsv)

# Step 2: Set the secrets
az keyvault secret set `
    --vault-name $keyvault `
    --name $paymentsSecretName `
    --value $paymentsSecretValue

az keyvault secret set `
    --vault-name $keyvault `
    --name $accountsSecretName `
    --value $accountsSecretValue

# Step 3: Disable public access to Key Vault
az keyvault update `
    --name $keyvault `
    --resource-group $rg `
    --public-network-access Disabled

$kvResourceId = az keyvault show --name $keyvault --resource-group $rg --query id -o tsv

$paymentsUmi = az identity show --name id-payments-api --resource-group $rg --query 'principalId' -o tsv

az role assignment create `
  --assignee $paymentsUmi `
  --role "Key Vault Secrets User" `
  --scope $kvResourceId

$processingUmi = az identity show --name id-processing --resource-group $rg --query 'principalId' -o tsv

az role assignment create `
  --assignee $processingUmi `
  --role "Key Vault Secrets User" `
  --scope $kvResourceId

$accountsUmi = az identity show --name id-accounts-api --resource-group $rg --query 'principalId' -o tsv

az role assignment create `
  --assignee $accountsUmi `
  --role "Key Vault Secrets User" `
  --scope $kvResourceId

# Step 4: Add private endpoint
az network private-endpoint create `
    --name "pep-${keyvault}" `
    --resource-group $rg `
    --vnet-name $vnet `
    --subnet $subnetPep `
    --private-connection-resource-id $kvResourceId `
    --group-ids vault `
    --connection-name "${keyvault}-connection"

# Approve the private endpoint connection
$connectionId = az keyvault show -n $keyvault --query "properties.privateEndpointConnections[0].id" --output tsv

az keyvault private-endpoint-connection approve --id $connectionId

# Create Private DNS Zone for Key Vault
az network private-dns zone create --resource-group $rg --name "privatelink.vaultcore.azure.net"

az network private-dns link vnet create `
  --resource-group $rg `
  --virtual-network $vnet `
  --zone-name "privatelink.vaultcore.azure.net" `
  --name link-keyvault `
  --registration-enabled false

az network private-endpoint dns-zone-group create `
  --resource-group $rg `
  --endpoint-name "pep-${keyvault}" `
  --name keyvault-dnszonegroup `
  --private-dns-zone "privatelink.vaultcore.azure.net" `
  --zone-name keyvault-dnszone

Write-Host "Key Vault created with private endpoint and secret set."

