# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ProficiencyQuiz.Api.csproj ./
RUN dotnet restore ./ProficiencyQuiz.Api.csproj

# Copy everything and publish
COPY . ./
RUN dotnet publish ./ProficiencyQuiz.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Render uses PORT environment variable
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

COPY --from=build /app/publish ./
EXPOSE 8080

ENTRYPOINT ["dotnet", "ProficiencyQuiz.Api.dll"]
