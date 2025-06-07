param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$vnetName,
    [Parameter(Mandatory=$true)][string]$subnetPep,
    [Parameter(Mandatory=$true)][string]$subnetApps,
    [Parameter(Mandatory=$true)][string]$subnetAppgw,
    [Parameter(Mandatory=$true)][string]$subnetFirewall
)

az network vnet create `
  --resource-group $rg `
  --name $vnetName `
  --location $location `
  --address-prefix 10.0.0.0/16 `
  --subnet-name $subnetPep `
  --subnet-prefix 10.0.1.0/28

az network vnet subnet create `
  --resource-group $rg `
  --vnet-name $vnetName `
  --name $subnetApps `
  --address-prefix 10.0.2.0/27

az network vnet subnet create `
  --resource-group $rg `
  --vnet-name $vnetName `
  --name $subnetAppgw `
  --address-prefix 10.0.3.0/28

# Azure firewall needs a /26 subnet for its IP configuration
az network vnet subnet create `
  --resource-group $rg `
  --vnet-name $vnetName `
  --name $subnetFirewall `
  --address-prefix 10.0.4.0/26
