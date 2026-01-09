# Use the official .NET 10 SDK image as the build environment
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Set the build configuration (default is Release)
ARG BUILD_CONFIGURATION=Release

# Set the working directory inside the container
WORKDIR /src

# Copy the source files from src/ to /src
COPY ["src/", "/src"]

# Restore dependencies
RUN dotnet restore "Mcp.Xray/Mcp.Xray.csproj"

# Copy the remaining source code
COPY . .

# Build the project
RUN dotnet build "Mcp.Xray/Mcp.Xray.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the project
RUN dotnet publish "Mcp.Xray/Mcp.Xray.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# Use the official .NET 10 ASP.NET runtime as the runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Set the working directory for the runtime
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose port 9988 (adjust if needed)
EXPOSE 9988

# Set the entry point for the container
ENTRYPOINT ["dotnet", "Mcp.Xray.dll"]