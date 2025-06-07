# Enable dynamic extension install for az CLI
az config set extension.use_dynamic_install=yes_without_prompt
az config set extension.dynamic_install_allow_preview=true

# Set variables
$LOCATION = "westeurope"
$RESOURCE_GROUP = "gohttpbin-rg"
$VNET_NAME = "gohttpbin-vnet"
$SUBNET_APP = "app-subnet"
$SUBNET_GATEWAY = "gateway-subnet"
$FW_SUBNET = "AzureFirewallSubnet"
$CONTAINER_ENV_NAME = "gohttpbin-env"
$CONTAINER_APP_NAME = "gohttpbin"
$IMAGE = "kennethreitz/httpbin"
$GATEWAY_NAME = "gohttpbin-gateway"
$FIREWALL_NAME = "gohttpbin-fw"
$FIREWALL_POLICY_NAME = "gohttpbin-fw-policy"
$FIREWALL_PUBLIC_IP = "gohttpbin-fw-ip"
$FIREWALL_POLICY_COLLECTION = "NetworkRuleCollectionGroup"
$ALLOWED_URL = "httpbin.org"

# Get current user ID and subscription ID
$USER_ID = az ad signed-in-user show --query id -o tsv
$SUBSCRIPTION_ID = az account show --query id -o tsv

Write-Host "Logged in user: $USER_ID"
Write-Host "Subscription ID: $SUBSCRIPTION_ID"

# Grant Network Contributor on the whole resource group
az group create --name $RESOURCE_GROUP --location $LOCATION
az role assignment create --assignee $USER_ID --role "Network Contributor" --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

# Create VNet and subnets
az network vnet create --resource-group $RESOURCE_GROUP --name $VNET_NAME --address-prefixes 10.0.0.0/16
az network vnet subnet create --resource-group $RESOURCE_GROUP --vnet-name $VNET_NAME --name $SUBNET_APP --address-prefix 10.0.1.0/24
az network vnet subnet update --resource-group $RESOURCE_GROUP --vnet-name $VNET_NAME --name $SUBNET_APP --delegations "Microsoft.App/environments"
az network vnet subnet create --resource-group $RESOURCE_GROUP --vnet-name $VNET_NAME --name $SUBNET_GATEWAY --address-prefixes 10.0.2.0/24
az network vnet subnet create --resource-group $RESOURCE_GROUP --vnet-name $VNET_NAME --name $FW_SUBNET --address-prefixes 10.0.3.0/24

# Create Azure Firewall and Public IP
az network public-ip create --resource-group $RESOURCE_GROUP --name $FIREWALL_PUBLIC_IP --sku "Standard"
az network firewall create --resource-group $RESOURCE_GROUP --name $FIREWALL_NAME --location $LOCATION
az network firewall ip-config create --firewall-name $FIREWALL_NAME --resource-group $RESOURCE_GROUP --name "fw-config" --public-ip-address $FIREWALL_PUBLIC_IP --vnet-name $VNET_NAME

# Get Firewall private IP
$FW_PRIVATE_IP = az network firewall show --resource-group $RESOURCE_GROUP --name $FIREWALL_NAME --query "ipConfigurations[0].privateIPAddress" -o tsv
Write-Host "Azure Firewall private IP: $FW_PRIVATE_IP"

# Create firewall policy with outbound rules
az network firewall policy create --resource-group $RESOURCE_GROUP --name $FIREWALL_POLICY_NAME --location $LOCATION
az network firewall policy rule-collection-group create --policy-name $FIREWALL_POLICY_NAME --resource-group $RESOURCE_GROUP --name $FIREWALL_POLICY_COLLECTION --priority 100
az network firewall policy rule-collection-group collection add-filter-collection --resource-group $RESOURCE_GROUP --policy-name $FIREWALL_POLICY_NAME --rule-collection-group-name $FIREWALL_POLICY_COLLECTION --name service_tags --action Allow --rule-name service_tags --rule-type ApplicationRule --source-addresses 10.0.0.0/16 --fqdn-tags HDInsight --collection-priority 200
az network firewall policy rule-collection-group collection add-filter-collection --resource-group $RESOURCE_GROUP --policy-name $FIREWALL_POLICY_NAME --rule-collection-group-name $FIREWALL_POLICY_COLLECTION --name allowed_urls --action Allow --rule-name allowed_urls --rule-type ApplicationRule --protocols Https=443 --source-addresses 10.0.0.0/16 --target-fqdns $ALLOWED_URL --collection-priority 100

$FIREWALL_POLICY_ID = az network firewall policy show --resource-group $RESOURCE_GROUP --name $FIREWALL_POLICY_NAME --query "id" -o tsv
az network firewall update --name $FIREWALL_NAME --resource-group $RESOURCE_GROUP --firewall-policy $FIREWALL_POLICY_ID --no-wait
Write-Host "Firewall is applying its new policy in the background."

# Create Container App Environment
$SUBNET_APP_ID = az network vnet subnet show --resource-group $RESOURCE_GROUP --vnet-name $VNET_NAME --name $SUBNET_APP --query id -o tsv
az containerapp env create --name $CONTAINER_ENV_NAME --resource-group $RESOURCE_GROUP --location $LOCATION --infrastructure-subnet-resource-id $SUBNET_APP_ID

# Create Application Gateway
az network public-ip create --resource-group $RESOURCE_GROUP --name "$GATEWAY_NAME-ip" --sku Standard
$GATEWAY_PUBLIC_IP = az network public-ip show --resource-group $RESOURCE_GROUP --name "$GATEWAY_NAME-ip" --query ipAddress -o tsv
Write-Host "Application Gateway public IP: $GATEWAY_PUBLIC_IP"
az network application-gateway create --resource-group $RESOURCE_GROUP --name $GATEWAY_NAME --location $LOCATION --vnet-name $VNET_NAME --subnet $SUBNET_GATEWAY --public-ip-address "$GATEWAY_NAME-ip" --sku Standard_v2 --capacity 1 --priority 100

# Deploy httpbin container app
az containerapp create --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --environment $CONTAINER_ENV_NAME --image $IMAGE --ingress external --target-port 80 --cpu 0.5 --memory 1Gi

# Configure Application Gateway Backend and Routing
$CONTAINERAPP_FQDN = az containerapp show --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --query properties.configuration.ingress.fqdn -o tsv
Write-Host "Container App FQDN: $CONTAINERAPP_FQDN"
az network application-gateway address-pool create --gateway-name $GATEWAY_NAME --resource-group $RESOURCE_GROUP --name "$GATEWAY_NAME-pool" --servers $CONTAINERAPP_FQDN
az network application-gateway http-settings create --resource-group $RESOURCE_GROUP --gateway-name $GATEWAY_NAME --name "$GATEWAY_NAME-app-http-settings" --port 80 --protocol Http --host-name-from-backend-pool true
$DEFAULT_LISTENER_NAME = az network application-gateway http-listener list --gateway-name $GATEWAY_NAME --resource-group $RESOURCE_GROUP --query '[0].name' -o tsv
Write-Host "Using existing HTTP listener: $DEFAULT_LISTENER_NAME"
az network application-gateway rule create --resource-group $RESOURCE_GROUP --gateway-name $GATEWAY_NAME --name "$GATEWAY_NAME-rule" --http-listener $DEFAULT_LISTENER_NAME --rule-type Basic --address-pool "$GATEWAY_NAME-pool" --http-settings "$GATEWAY_NAME-app-http-settings" --priority 100
Write-Host "Application Gateway is configured to route HTTP traffic to the Container App."

# Wait for Application Gateway public IP to become available
Write-Host "Waiting for Application Gateway public IP to become available..."
for ($i = 1; $i -le 30; $i++) {
  $GATEWAY_PUBLIC_IP = az network public-ip show --resource-group $RESOURCE_GROUP --name "$GATEWAY_NAME-ip" --query ipAddress -o tsv
  if ($GATEWAY_PUBLIC_IP -and $GATEWAY_PUBLIC_IP -ne "null") {
    Write-Host "Application Gateway public IP is: $GATEWAY_PUBLIC_IP"
    break
  }
  Start-Sleep -Seconds 10
}

if (-not $GATEWAY_PUBLIC_IP -or $GATEWAY_PUBLIC_IP -eq "null") {
  Write-Host "Failed to retrieve Application Gateway public IP."
  exit 1
}

# Test HTTP access to the Application Gateway public IP
Write-Host "Testing HTTP access to http://$GATEWAY_PUBLIC_IP ..."
for ($i = 1; $i -le 12; $i++) {
  try {
    Invoke-WebRequest -Uri "http://$GATEWAY_PUBLIC_IP/get" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "HTTP test succeeded!"
    exit 0
  } catch {
    Write-Host "HTTP test failed, retrying in 10 seconds..."
    Start-Sleep -Seconds 10
  }
}

Write-Host "HTTP test failed after multiple retries."
exit 1
