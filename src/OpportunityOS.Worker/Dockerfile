# Build from the repo root: docker build -f src/OpportunityOS.Worker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpportunityOS.slnx ./
COPY src/OpportunityOS.Worker/OpportunityOS.Worker.csproj src/OpportunityOS.Worker/
COPY src/OpportunityOS.Application/OpportunityOS.Application.csproj src/OpportunityOS.Application/
COPY src/OpportunityOS.Contracts/OpportunityOS.Contracts.csproj src/OpportunityOS.Contracts/
COPY src/OpportunityOS.Domain/OpportunityOS.Domain.csproj src/OpportunityOS.Domain/
COPY src/OpportunityOS.Infrastructure/OpportunityOS.Infrastructure.csproj src/OpportunityOS.Infrastructure/
RUN dotnet restore src/OpportunityOS.Worker/OpportunityOS.Worker.csproj

COPY src/ src/
RUN dotnet publish src/OpportunityOS.Worker/OpportunityOS.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OpportunityOS.Worker.dll"]
