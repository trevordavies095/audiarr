# Stage 1: Build and publish the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore "audiarr.csproj"

# Copy the entire project and build it
COPY . ./
RUN dotnet publish "audiarr.csproj" -c Release -o /app/publish

# Stage 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Set the environment variable (can be overridden at runtime)
ENV MUSIC_LIBRARY_PATH="/music"

# Set the environment variable to listen on port 5279
ENV ASPNETCORE_URLS=http://+:5279
EXPOSE 5279

# Copy the published application
COPY --from=build /app/publish .

# Ensure appsettings.json is copied explicitly
COPY appsettings.json /app/appsettings.json

# Run the application
ENTRYPOINT ["dotnet", "audiarr.dll"]