param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$envName,
    [Parameter(Mandatory=$true)][string]$subnetApps,
    [Parameter(Mandatory=$true)][string]$vnet
)

$subnetId = az network vnet subnet show `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetApps `
  --query id -o tsv

az containerapp env create `
  --name $envName `
  --resource-group $rg `
  --location $location `
  --infrastructure-subnet-resource-id $subnetId `
  --internal-only