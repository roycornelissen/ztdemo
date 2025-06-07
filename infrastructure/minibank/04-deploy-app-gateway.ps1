param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$subnetAppgw,
    [Parameter(Mandatory=$true)][string]$vnet
)

Write-Host "Deploying Application Gateway with public IP..."

$gatewayName = "agw-minibank"
$gatewayPublicIpName = "pip-minibank-gateway"

# --- Application Gateway ---
# Create public IP for Gateway
az network public-ip create `
  --resource-group $rg `
  --name $gatewayPublicIpName `
  --sku Standard `
  --zone 1

# Get the public IP address value
$gatewayPublicIp=$(az network public-ip show `
  --resource-group $rg `
  --name $gatewayPublicIpName `
  --query ipAddress -o tsv)

Write-Host "Application Gateway public IP: $gatewayPublicIp"

# Create Application Gateway (with required --priority for default rule)
az network application-gateway create `
  --resource-group $rg `
  --name $gatewayName `
  --location $location `
  --vnet-name $vnet `
  --subnet $subnetAppgw `
  --public-ip-address $gatewayPublicIpName `
  --sku Standard_v2 `
  --capacity 1 `
  --priority 100
