# Stage 1: Build and publish the application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore "Audiarr.csproj"

# Copy the remaining source code and build the project
COPY . ./
RUN dotnet publish "Audiarr.csproj" -c Release -o /app/publish

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "Audiarr.dll"]
