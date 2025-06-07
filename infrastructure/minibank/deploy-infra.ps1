param(
    [Parameter(Mandatory=$true)][string]$paymentsSecretValue,
    [Parameter(Mandatory=$true)][string]$accountsSecretValue
)

$rg = "rg-myapp"
$location = "westeurope"
$envName = "aca-env"
$vnet = "vnet-payments"
$rg = "rg-minibank"
$location = "westeurope"
$storage = "stminibank"
$subnetApps = "snet-apps"
$subnetAppgw = "snet-appgw"
$subnetPep = "snet-pep"
$subnetFirewall = "AzureFirewallSubnet"
$allowedUrl = "open.er-api.com" # example url for outbound traffic
$keyvault = "kv-minibank"
$paymentsSecretName = "payments-client-secret"
$accountsSecretName = "accounts-client-secret"

az config set extension.dynamic_install_allow_preview=true
az config set extension.use_dynamic_install=yes_without_prompt

az group create --name $rg --location $location

$umiAccountsApi = az identity create --name id-accounts-api --resource-group $rg --location $location | ConvertFrom-Json
$umiPaymentsApi = az identity create --name id-payments-api --resource-group $rg --location $location | ConvertFrom-Json
$umiProcessing = az identity create --name id-processing --resource-group $rg --location $location | ConvertFrom-Json

Write-Host "Create vnet and subnets"
./00-create-vnet-subnets.ps1 -rg $rg -location $location -vnetName $vnet -subnetPep $subnetPep -subnetApps $subnetApps -subnetAppgw $subnetAppgw -subnetFirewall $subnetFirewall

Write-Host "Create storage"
./01-create-storage.ps1 -rg $rg -location $location -storage $storage -vnet $vnet -subnetPep $subnetPep

Write-Host "Create Key Vault"
./01-create-keyvault.ps1 -rg $rg -location $location -keyvault $keyvault -vnet $vnet -subnetPep $subnetPep -paymentsSecretName $paymentsSecretName -paymentsSecretValue $paymentsSecretValue -accountsSecretName $accountsSecretName -accountsSecretValue $accountsSecretValue

Write-Host "Create NSGs and routes"
./02-create-nsgs-and-routes.ps1 -rg $rg -location $location -vnet $vnet -subnetApps $subnetApps -subnetPep $subnetPep -subnetFirewall $subnetFirewall

Write-Host "Create container app environment"
./03-deploy-container-app-env.ps1 -rg $rg -location $location -envName $envName -subnetApps $subnetApps -vnet $vnet

Write-Host "Create Application Gateway"
./04-deploy-app-gateway.ps1 -rg $rg -location $location -subnetAppgw $subnetAppgw -vnet $vnet

Write-Host "Deploy Firewall and configure allowed URLs"
./05-deploy-firewall.ps1 -rg $rg -location $location -subnetApps $subnetApps -subnetFirewall $subnetFirewall -vnet $vnet -allowedUrl $allowedUrl

Write-Host "Assign rbac roles to identities for ACR pull access"
$acrId = az acr show --name minibank --resource-group rg-minibank-dev --query id -o tsv

# allow identities AcrPull role on the acr in rg rg-minibank-dev named minibank
az role assignment create --assignee $umiAccountsApi.principalId --role AcrPull --scope $acrId
az role assignment create --assignee $umiPaymentsApi.principalId --role AcrPull --scope $acrId
az role assignment create --assignee $umiProcessing.principalId --role AcrPull --scope $acrId

az containerapp env identity assign -n $envName -g $rg --user-assigned $umiAccountsApi.id $umiPaymentsApi.id $umiProcessing.id