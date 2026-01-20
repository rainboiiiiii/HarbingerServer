# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["GameBackend.Api.csproj", "./"]
RUN dotnet restore "GameBackend.Api.csproj"

# Copy the rest of the source code
COPY . .

# Build and publish the application
RUN dotnet publish "GameBackend.Api.csproj" -c Release -o /app/publish

# Use the official ASP.NET Core runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 8080 (standard for .NET 8)
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Start the application
ENTRYPOINT ["dotnet", "GameBackend.Api.dll"]
