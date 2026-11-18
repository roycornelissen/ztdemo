# Practical Zero Trust with ASP.NET Core and Azure

This repository contains demo code and infrastructure for my session "Building Practical Zero Trust APIs with .NET 10 and Azure". The sample currently targets .NET 11.

Feel free to use it as inspiration, *NOT* as production code 😉

See [Zero Trust samples and research](docs/zero-trust-samples.md) for the demo
walkthrough, implementation notes, reference guidance, and production limitations.

## Container images

Local orchestration uses Aspire in `src\MiniBank.AppHost`. Build deployment images
with the .NET 11 SDK's container publishing support; no Dockerfiles are needed.
From the repository root, with a local Docker or Podman container engine running:

```powershell
dotnet publish .\src\AccountsApi\AccountsApi.csproj -c Release --os linux --arch x64 -t:PublishContainer -p:ContainerRepository=minibank/accounts -p:ContainerImageTag=demo
dotnet publish .\src\PaymentsApi\PaymentsApi.csproj -c Release --os linux --arch x64 -t:PublishContainer -p:ContainerRepository=minibank/payments -p:ContainerImageTag=demo
dotnet publish .\src\Processing\Processing.csproj -c Release --os linux --arch x64 -t:PublishContainer -p:ContainerRepository=minibank/processing -p:ContainerImageTag=demo
```

These commands publish images to the local engine, not Azure Container Registry.
For a registry push, authenticate to the registry and also set
`-p:ContainerRegistry=<registry>.azurecr.io`. Choose a tag that matches the
deployment configuration; the Terraform development environment currently uses
`latest`. Container hardening settings remain in the workload project files,
including the intentionally commented PaymentsApi demo switches.

For the PaymentsApi image demo, the app can start without `AZURE_CLIENT_ID` in a
non-Development container. It logs a warning, and storage operations fail until a
valid user-assigned managed identity and storage endpoints are configured. This
is not a functional payment deployment; no local credential is used as a fallback.

## Optional Dependency-Track demo

Dependency-Track runs separately from MiniBank and Aspire. Its Compose file has
an explicit name, so pass it with `-f`. From the repository root:

```powershell
docker compose -f .\src\dependency-track-compose.yml up -d
```

Open the frontend at <http://localhost:8081>; its API is at
<http://localhost:8080>. This is a local demo configuration, not a production
deployment.

To stop it while retaining its data volumes:

```powershell
docker compose -f .\src\dependency-track-compose.yml down
```