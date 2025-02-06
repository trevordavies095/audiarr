# Stage 1: Build and publish the application using the .NET 9.0 preview image
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore "audiarr.csproj"

# Copy the remaining source code and build the project
COPY . ./
RUN dotnet publish "audiarr.csproj" -c Release -o /app/publish

# Stage 2: Create the runtime image using the .NET 9.0 preview runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS runtime
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose ports as needed
EXPOSE 80
EXPOSE 443

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "audiarr.dll"]
