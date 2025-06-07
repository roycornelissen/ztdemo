#!/bin/bash

set -e

# Enable dynamic extension install for az CLI
az config set extension.use_dynamic_install=yes_without_prompt
az config set extension.dynamic_install_allow_preview=true

# Set variables
LOCATION="westeurope"
RESOURCE_GROUP="gohttpbin-rg"
VNET_NAME="gohttpbin-vnet"
SUBNET_APP="app-subnet"
SUBNET_GATEWAY="gateway-subnet"
FW_SUBNET="AzureFirewallSubnet"
CONTAINER_ENV_NAME="gohttpbin-env"
CONTAINER_APP_NAME="gohttpbin"
IMAGE="kennethreitz/httpbin"
GATEWAY_NAME="gohttpbin-gateway"
FIREWALL_NAME="gohttpbin-fw"
FIREWALL_POLICY_NAME="gohttpbin-fw-policy"
FIREWALL_PUBLIC_IP="gohttpbin-fw-ip"
FIREWALL_POLICY_COLLECTION="NetworkRuleCollectionGroup"

# External URL to allow
ALLOWED_URL="httpbin.org"

# Get current user ID and subscription ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

echo "Logged in user: $USER_ID"
echo "Subscription ID: $SUBSCRIPTION_ID"

# Grant Network Contributor on the whole resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# az role assignment create \
#   --assignee $USER_ID \
#   --role "Network Contributor" \
#   --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

# Create VNet and subnets
az network vnet create \
  --resource-group $RESOURCE_GROUP \
  --name $VNET_NAME \
  --address-prefixes 10.0.0.0/16 

az network vnet subnet create \
  --resource-group $RESOURCE_GROUP \
  --vnet-name $VNET_NAME \
  --name $SUBNET_APP \
  --address-prefix 10.0.1.0/24

az network vnet subnet update \
  --resource-group $RESOURCE_GROUP \
  --vnet-name $VNET_NAME \
  --name $SUBNET_APP \
  --delegations "Microsoft.App/environments"

az network vnet subnet create \
  --resource-group $RESOURCE_GROUP \
  --vnet-name $VNET_NAME \
  --name $SUBNET_GATEWAY \
  --address-prefixes 10.0.2.0/24

az network vnet subnet create \
  --resource-group $RESOURCE_GROUP \
  --vnet-name $VNET_NAME \
  --name $FW_SUBNET \
  --address-prefixes 10.0.3.0/24

# Create Azure Firewall and Public IP
az network public-ip create \
  --resource-group $RESOURCE_GROUP \
  --name $FIREWALL_PUBLIC_IP \
  --sku "Standard"

az network firewall create \
  --resource-group $RESOURCE_GROUP \
  --name $FIREWALL_NAME \
  --location $LOCATION

az network firewall ip-config create \
  --firewall-name $FIREWALL_NAME \
  --resource-group $RESOURCE_GROUP \
  --name "fw-config" \
  --public-ip-address $FIREWALL_PUBLIC_IP \
  --vnet-name $VNET_NAME

# Get Firewall private IP
FW_PRIVATE_IP=$(az network firewall show \
  --resource-group $RESOURCE_GROUP \
  --name $FIREWALL_NAME \
  --query "ipConfigurations[0].privateIPAddress" -o tsv)

echo "Azure Firewall private IP: $FW_PRIVATE_IP"

# Create firewall policy with outbound rules
az network firewall policy create \
  --resource-group $RESOURCE_GROUP \
  --name $FIREWALL_POLICY_NAME \
  --location $LOCATION

# First rule always has to create a Collection group
az network firewall policy rule-collection-group create \
  --policy-name $FIREWALL_POLICY_NAME \
  --resource-group $RESOURCE_GROUP \
  --name $FIREWALL_POLICY_COLLECTION \
  --priority 100

# Service Tags
az network firewall policy rule-collection-group collection add-filter-collection \
  --resource-group $RESOURCE_GROUP \
  --policy-name $FIREWALL_POLICY_NAME \
  --rule-collection-group-name $FIREWALL_POLICY_COLLECTION \
  --name service_tags \
  --action Allow \
  --rule-name service_tags \
  --rule-type ApplicationRule \
  --source-addresses 10.0.0.0/16 \
  --fqdn-tags HDInsight \
  --collection-priority 200

az network firewall policy rule-collection-group collection add-filter-collection \
  --resource-group $RESOURCE_GROUP \
  --policy-name $FIREWALL_POLICY_NAME \
  --rule-collection-group-name $FIREWALL_POLICY_COLLECTION \
  --name allowed_urls \
  --action Allow \
  --rule-name allowed_urls \
  --rule-type ApplicationRule \
  --protocols Https=443 \
  --source-addresses 10.0.0.0/16 \
  --target-fqdns $ALLOWED_URL \
  --collection-priority 100

FIREWALL_POLICY_ID=$(az network firewall policy show \
  --resource-group $RESOURCE_GROUP \
  --name $FIREWALL_POLICY_NAME \
  --query "id" -o tsv)

# Attach policy to firewall
az network firewall update \
  --name $FIREWALL_NAME \
  --resource-group $RESOURCE_GROUP \
  --firewall-policy $FIREWALL_POLICY_ID \
  --no-wait

echo "Firewall is applying its new policy in the background."

# --- Container App Environment ---
az containerapp env create \
  --name $CONTAINER_ENV_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --infrastructure-subnet-resource-id $(az network vnet subnet show \
    --resource-group $RESOURCE_GROUP \
    --vnet-name $VNET_NAME \
    --name $SUBNET_APP \
    --query id -o tsv)

# --- Application Gateway ---
# Create public IP for Gateway
az network public-ip create \
  --resource-group $RESOURCE_GROUP \
  --name "${GATEWAY_NAME}-ip" \
  --sku Standard

# Get the public IP address value
GATEWAY_PUBLIC_IP=$(az network public-ip show \
  --resource-group $RESOURCE_GROUP \
  --name "${GATEWAY_NAME}-ip" \
  --query ipAddress -o tsv)

echo "Application Gateway public IP: $GATEWAY_PUBLIC_IP"

# Create Application Gateway (with required --priority for default rule)
az network application-gateway create \
  --resource-group $RESOURCE_GROUP \
  --name $GATEWAY_NAME \
  --location $LOCATION \
  --vnet-name $VNET_NAME \
  --subnet $SUBNET_GATEWAY \
  --public-ip-address "${GATEWAY_NAME}-ip" \
  --sku Standard_v2 \
  --capacity 1 \
  --priority 100

# --- Deploy httpbin container app ---
az containerapp create \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINER_ENV_NAME \
  --image $IMAGE \
  --ingress external \
  --target-port 80 \
  --cpu 0.5 --memory 1Gi

# --- Application Gateway Backend and Routing ---
# Get Container App FQDN
CONTAINERAPP_FQDN=$(az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query properties.configuration.ingress.fqdn -o tsv)

echo "Container App FQDN: $CONTAINERAPP_FQDN"

# Add backend pool
az network application-gateway address-pool create \
  --gateway-name $GATEWAY_NAME \
  --resource-group $RESOURCE_GROUP \
  --name "${GATEWAY_NAME}-pool" \
  --servers $CONTAINERAPP_FQDN

# HTTP settings
az network application-gateway http-settings create \
  --resource-group $RESOURCE_GROUP \
  --gateway-name $GATEWAY_NAME \
  --name "${GATEWAY_NAME}-app-http-settings" \
  --port 80 \
  --protocol Http \
  --host-name-from-backend-pool true

# Use existing HTTP listener (avoid duplicate listener on same port/IP)
DEFAULT_LISTENER_NAME=$(az network application-gateway http-listener list \
  --gateway-name $GATEWAY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query '[0].name' -o tsv)

echo "Using existing HTTP listener: $DEFAULT_LISTENER_NAME"

# Rule (must specify priority)
az network application-gateway rule create \
  --resource-group $RESOURCE_GROUP \
  --gateway-name $GATEWAY_NAME \
  --name "${GATEWAY_NAME}-rule" \
  --http-listener "$DEFAULT_LISTENER_NAME" \
  --rule-type Basic \
  --address-pool "${GATEWAY_NAME}-pool" \
  --http-settings "${GATEWAY_NAME}-app-http-settings" \
  --priority 100

echo "Application Gateway is configured to route HTTP traffic to the Container App."
echo "Deployment completed. The httpbin container app is running behind the Application Gateway."

# Wait for the public IP to become available
echo "Waiting for Application Gateway public IP to become available..."
for i in {1..30}; do
  GATEWAY_PUBLIC_IP=$(az network public-ip show \
    --resource-group $RESOURCE_GROUP \
    --name "${GATEWAY_NAME}-ip" \
    --query ipAddress -o tsv)
  if [[ -n "$GATEWAY_PUBLIC_IP" && "$GATEWAY_PUBLIC_IP" != "null" ]]; then
    echo "Application Gateway public IP is: $GATEWAY_PUBLIC_IP"
    break
  fi
  sleep 10
done

if [[ -z "$GATEWAY_PUBLIC_IP" || "$GATEWAY_PUBLIC_IP" == "null" ]]; then
  echo "Failed to retrieve Application Gateway public IP."
  exit 1
fi

# Test HTTP access to the Application Gateway public IP
echo "Testing HTTP access to http://$GATEWAY_PUBLIC_IP ..."
for i in {1..12}; do
  if curl -sSf --max-time 5 "http://$GATEWAY_PUBLIC_IP/get"; then
    echo "HTTP test succeeded!"
    exit 0
  else
    echo "HTTP test failed, retrying in 10 seconds..."
    sleep 10
  fi
done

echo "HTTP test failed after multiple retries."
exit 1

# TODO
# - Container App environment (done)
# - Gateway to allow Internet to access the environment (partially done, frontend rules aren't working yet)
# - Test app (partially done)
# - NSGs to lock down the subnet -> subnet communication
# - Subnet + peered queue and db
# - Vault with secret
# - 3 apps (2 connected to Gateway)
