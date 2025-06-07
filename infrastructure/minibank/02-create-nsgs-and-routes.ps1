param(
    [Parameter(Mandatory=$true)][string]$rg,
    [Parameter(Mandatory=$true)][string]$location,
    [Parameter(Mandatory=$true)][string]$vnet,
    [Parameter(Mandatory=$true)][string]$subnetApps,
    [Parameter(Mandatory=$true)][string]$subnetPep,
    [Parameter(Mandatory=$true)][string]$subnetFirewall
)

# Create NSG for container apps
az network nsg create --resource-group $rg --name nsg-containerapps

az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-containerapps `
  --name AllowAppGwIngress `
  --priority 100 `
  --direction Inbound `
  --access Allow `
  --protocol Tcp `
  --source-address-prefixes AzureLoadBalancer `
  --destination-port-ranges 443 `
  --destination-address-prefixes '*' `
  --description "Allow traffic from App Gateway on port 443"

az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-containerapps `
  --name DenyAllInbound `
  --priority 4096 `
  --direction Inbound `
  --access Deny `
  --protocol '*' `
  --source-address-prefixes '*' `
  --destination-port-ranges '*' `
  --destination-address-prefixes '*' `
  --description "Deny all other inbound traffic"

az network vnet subnet update `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetApps `
  --delegations "Microsoft.App/environments" `
  --network-security-group nsg-containerapps

$subnetAppsPrefix = az network vnet subnet show `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetApps `
  --query addressPrefix -o tsv

$subnetPepPrefix = az network vnet subnet show `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetPep `
  --query addressPrefix -o tsv

$subnetFirewallPrefix = az network vnet subnet show `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetFirewall `
  --query addressPrefix -o tsv

# Create NSG for private endpoints subnet
az network nsg create --resource-group $rg --name nsg-privateendpoints

# Allow traffic from $subnetApps to $subnetPep on port 443 (Outbound rule on nsg-containerapps)
az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-containerapps `
  --name AllowToPrivateEndpoints443 `
  --priority 100 `
  --direction Outbound `
  --access Allow `
  --protocol Tcp `
  --destination-address-prefixes $subnetPepPrefix `
  --destination-port-ranges 443 `
  --source-address-prefixes $subnetAppsPrefix `
  --description "Allow outbound traffic from app subnet to private endpoints subnet on port 443"

# Allow traffic from $subnetApps to $subnetFirewall on port * (Outbound rule on nsg-containerapps)
az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-containerapps `
  --name AllowToFirewall `
  --priority 110 `
  --direction Outbound `
  --access Allow `
  --protocol Tcp `
  --destination-address-prefixes $subnetFirewallPrefix `
  --destination-port-ranges * `
  --source-address-prefixes $subnetFirewallPrefix `
  --description "Allow outbound traffic from app subnet to firewall subnet on port 443"

# Deny all other outbound traffic from nsg-containerapps (optional, for strict lockdown)
az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-containerapps `
  --name DenyAllOutbound `
  --priority 120 `
  --direction Outbound `
  --access Deny `
  --protocol '*' `
  --source-address-prefixes '*' `
  --destination-address-prefixes '*' `
  --destination-port-ranges '*' `
  --description "Deny all other outbound traffic"

# Associate NSG to private endpoints subnet (if not already associated)
az network vnet subnet update `
  --resource-group $rg `
  --vnet-name $vnet `
  --name $subnetPep `
  --network-security-group nsg-privateendpoints

# Allow inbound traffic from $subnetApps to $subnetPep on port 443 (Inbound rule on nsg-privateendpoints)
az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-privateendpoints `
  --name AllowFromAppSubnet443 `
  --priority 100 `
  --direction Inbound `
  --access Allow `
  --protocol Tcp `
  --source-address-prefixes $subnetAppsPrefix `
  --destination-port-ranges 443 `
  --destination-address-prefixes $subnetPepPrefix `
  --description "Allow inbound traffic from app subnet to private endpoints subnet on port 443"

# Deny all other inbound traffic to nsg-privateendpoints
az network nsg rule create `
  --resource-group $rg `
  --nsg-name nsg-privateendpoints `
  --name DenyAllOtherInbound `
  --priority 110 `
  --direction Inbound `
  --access Deny `
  --protocol '*' `
  --source-address-prefixes $subnetAppsPrefix `
  --destination-address-prefixes '*' `
  --destination-port-ranges '*' `
  --description "Deny all other inbound traffic from apps subnet"

