$rg = "rg-myapp"
$envName = "aca-env"
$rg = "rg-minibank"
$keyvault = "kv-minibank"
$paymentsClientSecretName = "payments-client-secret"
$accountsClientSecretName = "accounts-client-secret"

$imagePrefix = "minibank.azurecr.io/minibank"

$gatewayName = "agw-minibank"
$paymentsApp = "ca-payments"
$processingApp = "ca-processing"
$accountsApp = "ca-accounts"

$umiAccountsApi = az identity show --name id-accounts-api --resource-group $rg --query 'id' -o tsv
$umiPaymentsApi = az identity show --name id-payments-api --resource-group $rg --query 'id' -o tsv
$umiProcessing = az identity show --name id-processing --resource-group $rg --query 'id' -o tsv

# --- Deploy payments container app ---
az containerapp create `
  --name $paymentsApp `
  --resource-group $rg `
  --environment $envName `
  --image "$imagePrefix/payments:x64" `
  --ingress external `
  --target-port 8080 `
  --cpu 0.5 --memory 1Gi `
  --min-replicas 1 `
  --max-replicas 3 `
  --registry-server "minibank.azurecr.io" `
  --registry-identity $umiPaymentsApi `
  --user-assigned $umiPaymentsApi

# Get the Key Vault secret URI
$paymentsSecretUri = "https://$keyvault.vault.azure.net/secrets/$paymentsClientSecretName"
$paymentsSecretSpec = "clientsecret=keyvaultref:`"$paymentsSecretUri`",identityref:$umiPaymentsApi"

# Update the container app to add the secret as an environment variable
az containerapp secret set `
    --name $paymentsApp `
    --resource-group $rg `
    --secrets $paymentsSecretSpec

# Set the environment variable in the container app to reference the secret
az containerapp update `
    --name $paymentsApp `
    --resource-group $rg `
    --set-env-vars Entra__ClientSecret=secretref:clientsecret AzureStorage__QueueEndpoint="https://stminibank.queue.core.windows.net/" AzureStorage__TableEndpoint="https://stminibank.table.core.windows.net/"

# --- Deploy accounts container app ---
az containerapp create `
  --name $accountsApp `
  --resource-group $rg `
  --environment $envName `
  --image "$imagePrefix/accounts:x64" `
  --ingress external `
  --target-port 8080 `
  --cpu 0.5 --memory 1Gi `
  --min-replicas 1 `
  --max-replicas 3 `
  --registry-server "minibank.azurecr.io" `
  --registry-identity $umiAccountsApi `
  --user-assigned $umiAccountsApi

$accountsSecretUri = "https://$keyvault.vault.azure.net/secrets/$accountsClientSecretName"
$accountsSecretSpec = "clientsecret=keyvaultref:`"$accountsSecretUri`",identityref:$umiAccountsApi"

# Update the container app to add the secret as an environment variable
az containerapp secret set `
    --name $accountsApp `
    --resource-group $rg `
    --secrets $accountsSecretSpec

# Set the environment variable in the container app to reference the secret
az containerapp update `
    --name $accountsApp `
    --resource-group $rg `
    --set-env-vars Entra__ClientSecret=secretref:clientsecret AzureStorage__TableEndpoint="https://stminibank.table.core.windows.net/"

# --- Application Gateway Backend and Routing for Payments ---
# Get Container App FQDN
$paymentsFqdn=$(az containerapp show `
  --name $paymentsApp `
  --resource-group $rg `
  --query properties.configuration.ingress.fqdn -o tsv)

Write-Host "Payments App FQDN: $paymentsFqdn"

# Add backend pool for payments
az network application-gateway address-pool create `
  --gateway-name $gatewayName `
  --resource-group $rg `
  --name "$gatewayName-payments-pool" `
  --servers $paymentsFqdn

# --- Application Gateway Backend and Routing for Accounts ---
# Get Container App FQDN
$accountsFqdn=$(az containerapp show `
  --name $accountsApp `
  --resource-group $rg `
  --query properties.configuration.ingress.fqdn -o tsv)

Write-Host "Accounts App FQDN: $accountsFqdn"

# Add backend pool
az network application-gateway address-pool create `
  --gateway-name $gatewayName `
  --resource-group $rg `
  --name "$gatewayName-accounts-pool" `
  --servers $accountsFqdn

# HTTP settings for payments
az network application-gateway http-settings create `
  --resource-group $rg `
  --gateway-name $gatewayName `
  --name "$gatewayName-http-settings" `
  --port 8080 `
  --protocol Http `
  --host-name-from-backend-pool true

# Create a path-based rule for /payments and /accounts
az network application-gateway url-path-map create `
  --resource-group $rg `
  --gateway-name $gatewayName `
  --name "$gatewayName-payments-path-map" `
  --default-address-pool "$gatewayName-payments-pool" `
  --default-http-settings "$gatewayName-http-settings" `
  --paths "/payments/*" `
  --address-pool "$gatewayName-payments-pool" `
  --http-settings "$gatewayName-http-settings"

# Create a path-based rule for /payments and /accounts
az network application-gateway url-path-map create `
  --resource-group $rg `
  --gateway-name $gatewayName `
  --name "$gatewayName-accounts-path-map" `
  --default-address-pool "$gatewayName-accounts-pool" `
  --default-http-settings "$gatewayName-http-settings" `
  --paths "/accounts/*" `
  --address-pool "$gatewayName-accounts-pool" `
  --http-settings "$gatewayName-http-settings"

# Use existing HTTP listener (avoid duplicate listener on same port/IP)
$defaultListenerName=$(az network application-gateway http-listener list `
  --gateway-name $gatewayName `
  --resource-group $rg `
  --query '[0].name' -o tsv)

Write-Host "Using existing HTTP listener: $defaultListenerName"

# Rule (must specify priority)
az network application-gateway rule create `
  --resource-group $rg `
  --gateway-name $gatewayName `
  --name "$gatewayName-path-rule" `
  --http-listener $defaultListenerName `
  --rule-type PathBasedRouting `
  --url-path-map "$gatewayName-path-map" `
  --priority 100

Write-Host "Application Gateway is configured to route HTTP traffic to the Container Apps."
Write-Host "Deployment completed. The httpbin container app is running behind the Application Gateway."

