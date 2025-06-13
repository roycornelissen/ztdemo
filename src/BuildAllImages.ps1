param (
    [parameter(Mandatory=$true)][string]$tag,
    [parameter(Mandatory=$false)][string]$registry = "minibank.azurecr.io"
)

podman build -t $registry/minibank/accounts:$tag -f .\AccountsApi.Dockerfile . 
podman build -t $registry/minibank/payments:$tag -f .\PaymentsApi.Dockerfile . 
podman build -t $registry/minibank/processing:$tag -f .\Processing.Dockerfile . 

