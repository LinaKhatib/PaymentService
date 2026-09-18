
# BUILD 
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/TransactionService/TransactionService.csproj", "src/TransactionService/"]
RUN dotnet restore "src/TransactionService/TransactionService.csproj"

COPY . .
WORKDIR "/src/src/TransactionService"
RUN dotnet publish "TransactionService.csproj" -c Release -o /app/publish --no-restore

# RUNTIME
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TransactionService.dll"]