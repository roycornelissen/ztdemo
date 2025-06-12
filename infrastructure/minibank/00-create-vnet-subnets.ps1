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
  --subnet-name $subnetApps `
  --subnet-prefix 10.0.0.0/23

az network vnet subnet create `
  --resource-group $rg `
  --vnet-name $vnetName `
  --name $subnetAppgw `
  --address-prefix 10.0.2.0/24 `
  --private-link-service-network-policies Disabled

  # Azure firewall needs a /26 subnet for its IP configuration
az network vnet subnet create `
  --resource-group $rg `
  --vnet-name $vnetName `
  --name $subnetFirewall `
  --address-prefix 10.0.3.0/24  `
  --private-link-service-network-policies Disabled

  az network vnet subnet create `
  --resource-group $rg `
  --vnet-name $vnetName `
  --name $subnetPep `
  --address-prefix 10.0.4.0/24 `
  --private-link-service-network-policies Disabled
