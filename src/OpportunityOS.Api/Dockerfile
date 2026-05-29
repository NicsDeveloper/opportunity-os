# Build from the repo root: docker build -f src/OpportunityOS.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (leverage layer caching on project files).
COPY OpportunityOS.slnx ./
COPY src/OpportunityOS.Api/OpportunityOS.Api.csproj src/OpportunityOS.Api/
COPY src/OpportunityOS.Application/OpportunityOS.Application.csproj src/OpportunityOS.Application/
COPY src/OpportunityOS.Contracts/OpportunityOS.Contracts.csproj src/OpportunityOS.Contracts/
COPY src/OpportunityOS.Domain/OpportunityOS.Domain.csproj src/OpportunityOS.Domain/
COPY src/OpportunityOS.Infrastructure/OpportunityOS.Infrastructure.csproj src/OpportunityOS.Infrastructure/
RUN dotnet restore src/OpportunityOS.Api/OpportunityOS.Api.csproj

# Build & publish.
COPY src/ src/
RUN dotnet publish src/OpportunityOS.Api/OpportunityOS.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpportunityOS.Api.dll"]
