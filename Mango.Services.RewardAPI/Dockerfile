FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["Mango.Services.RewardAPI/Mango.Services.RewardAPI.csproj", "Mango.Services.RewardAPI/"]
RUN dotnet restore "Mango.Services.RewardAPI/Mango.Services.RewardAPI.csproj"
COPY . .
WORKDIR "/src/Mango.Services.RewardAPI"
RUN dotnet build "Mango.Services.RewardAPI.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Mango.Services.RewardAPI.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mango.Services.RewardAPI.dll"]
