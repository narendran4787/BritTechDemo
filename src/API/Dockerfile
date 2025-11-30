# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for better layer caching
COPY src/Domain/Domain.csproj ./src/Domain/
COPY src/Application/Application.csproj ./src/Application/
COPY src/Infrastructure/Infrastructure.csproj ./src/Infrastructure/
COPY src/API/API.csproj ./src/API/

# Restore dependencies for API project (which will restore all dependencies)
RUN dotnet restore src/API/API.csproj

# Copy all source files
COPY src/ ./src/

# Build and publish only the API project
RUN dotnet publish src/API/API.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create directory for certificates
RUN mkdir -p /https && chmod 755 /https

# Copy certificate from build context
# IMPORTANT: Certificate must be generated before building the image
# Run: ./generate-dev-cert.sh (or manually create certs/dev-cert.pfx)
# Build context is the root directory, so certs/ is relative to that
COPY certs/dev-cert.pfx /https/dev-cert.pfx

# Set certificate environment variables (can be overridden by docker-compose)
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/dev-cert.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=dev-cert-password

# Expose HTTPS port
EXPOSE 8443

# Set HTTPS URL
ENV ASPNETCORE_URLS=https://+:8443

# Copy published application
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Solution.API.dll"]
