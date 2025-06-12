param(
    [Parameter(Mandatory=$true)][string]$paymentsSecretValue,
    [Parameter(Mandatory=$true)][string]$accountsSecretValue
)

$rg = "rg-myapp"
$location = "westeurope"
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

az identity create --name id-accounts-api --resource-group $rg --location $location | ConvertFrom-Json
az identity create --name id-payments-api --resource-group $rg --location $location | ConvertFrom-Json
az identity create --name id-processing --resource-group $rg --location $location | ConvertFrom-Json

Write-Host "Create vnet and subnets"
./00-create-vnet-subnets.ps1 -rg $rg -location $location -vnetName $vnet -subnetPep $subnetPep -subnetApps $subnetApps -subnetAppgw $subnetAppgw -subnetFirewall $subnetFirewall

Write-Host "Create storage"
./01-create-storage.ps1 -rg $rg -location $location -storage $storage -vnet $vnet -subnetPep $subnetPep

Write-Host "Create Key Vault"
./01-create-keyvault.ps1 -rg $rg -location $location -keyvault $keyvault -vnet $vnet -subnetPep $subnetPep -paymentsSecretName $paymentsSecretName -paymentsSecretValue $paymentsSecretValue -accountsSecretName $accountsSecretName -accountsSecretValue $accountsSecretValue

Write-Host "Create NSGs and routes"
./02-create-nsgs-and-routes.ps1 -rg $rg -location $location -vnet $vnet -subnetApps $subnetApps -subnetPep $subnetPep -subnetFirewall $subnetFirewall

Write-Host "Deploy Firewall and configure allowed URLs"
./05-deploy-firewall.ps1 -rg $rg -location $location -subnetApps $subnetApps -subnetFirewall $subnetFirewall -vnet $vnet -allowedUrl $allowedUrl

Write-Host "Deploy container apps"
az deployment group create -g $rg --name minibank-apps --template-file ./ca-appgw/main.bicep

