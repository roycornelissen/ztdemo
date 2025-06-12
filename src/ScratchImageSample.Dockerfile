FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env
WORKDIR /app

RUN apk add clang binutils musl-dev build-base zlib-static

COPY . ./
RUN dotnet restore --runtime linux-musl-x64 DockerTest.csproj
RUN dotnet publish -r linux-musl-x64 -c Release --no-restore -o out DockerTest.csproj

# https://hub.docker.com/_/scratch/
FROM scratch AS runtime 
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["/app/DockerTest"]  

# The following csproj settings are necessery to build a self-contained app, AOT compiled
#<Project Sdk="Microsoft.NET.Sdk">
#
#  <PropertyGroup>
#    <OutputType>Exe</OutputType>
#    <TargetFramework>net9.0</TargetFramework>
#    <ImplicitUsings>enable</ImplicitUsings>
#    <Nullable>enable</Nullable>
#    
#    <!-- Minimum required settings for AOT deployment-->
#    <StaticallyLinked>true</StaticallyLinked>
#    <PublishAot>true</PublishAot>
#    <StaticExecutable>true</StaticExecutable>
#    
#    <!--Further reduce size by excluding reflection-->
#    <IlcDisableReflection>true</IlcDisableReflection>
#    <!--Further reduce image size by excluding ICU-->
#    <InvariantGlobalization>true</InvariantGlobalization>
#    <!--Further reduce size by excluding debug symbols-->
#    <StripSymbols>true</StripSymbols>
#  </PropertyGroup>
#</Project>