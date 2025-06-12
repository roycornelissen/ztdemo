param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$subnetApps,
    [Parameter(Mandatory=$true)][string]$subnetFirewall,
    [Parameter(Mandatory=$true)][string]$vnet,
    [Parameter(Mandatory=$true)][string]$allowedUrl
)

$firewallName = "afw-minibank"
$firewallPublicIp = "pip-minibank-firewall"
$firewallPolicyName="afwp-minibank"
$firewallPolicyCollection="NetworkRuleCollectionGroup"

az network public-ip create `
  --resource-group $rg `
  --name $firewallPublicIp `
  --sku "Standard" `
  --zone 1

az network firewall create `
  --resource-group $rg `
  --name $firewallName `
  --location $location

az network firewall ip-config create `
  --firewall-name $firewallName `
  --resource-group $rg `
  --name "afw-ip-config" `
  --public-ip-address $firewallPublicIp `
  --vnet-name $vnet

$firewallPrivateIp = $(az network firewall show `
  --resource-group $rg `
  --name $firewallName `
  --query "ipConfigurations[0].privateIPAddress" -o tsv)

Write-Output "Azure Firewall private IP: $firewallPrivateIp"

# Create firewall policy with outbound rules
az network firewall policy create `
  --resource-group $rg `
  --name $firewallPolicyName `
  --location $location

# First rule always has to create a Collection group
az network firewall policy rule-collection-group create `
  --policy-name $firewallPolicyName `
  --resource-group $rg `
  --name $firewallPolicyCollection `
  --priority 100

  # route all internet traffic from $subnetApps to Azure Firewall
az network route-table create `
  --name rt-firewall `
  --resource-group $rg `
  --location $location

az network route-table route create `
  --resource-group $rg `
  --route-table-name rt-firewall `
  --name internal-vnet-traffic `
  --address-prefix 10.0.0.0/16 `
  --next-hop-type VirtualNetworkGateway

az network route-table route create `
  --resource-group $rg `
  --route-table-name rt-firewall `
  --name internet-via-fw `
  --address-prefix 0.0.0.0/0 `
  --next-hop-type VirtualAppliance `
  --next-hop-ip-address $firewallPrivateIp

az network vnet subnet update `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetApps `
  --route-table rt-firewall

# allow the apps to go out to the allowed URL for external services
$subnetAppsPrefix = az network vnet subnet show `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetApps `
  --query addressPrefix -o tsv

az network firewall policy rule-collection-group collection add-filter-collection `
  --resource-group $rg `
  --policy-name $firewallPolicyName `
  --rule-collection-group-name $firewallPolicyCollection `
  --name allowed_urls `
  --action Allow `
  --rule-name allowed_url_external `
  --rule-type ApplicationRule `
  --protocols Https=443 `
  --source-addresses $subnetAppsPrefix `
  --target-fqdns $allowedUrl "minibank.azurecr.io" "login.microsoftonline.com" "login.windows.net" "sts.windows.net" "mcr.microsoft.com" `
  --collection-priority 100
  
$firewallPolicyId=$(az network firewall policy show `
  --resource-group $rg `
  --name $firewallPolicyName `
  --query "id" -o tsv)

Write-Host "Azure Firewall Policy ID: $firewallPolicyId"

# Attach policy to firewall
az network firewall update `
  --name $firewallName `
  --resource-group $rg `
  --firewall-policy $firewallPolicyId `
  --no-wait

Write-Host "Firewall is applying its new policy in the background."  
