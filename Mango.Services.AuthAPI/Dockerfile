FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["Mango.Services.AuthAPI/Mango.Services.AuthAPI.csproj", "Mango.Services.AuthAPI/"]
RUN dotnet restore "Mango.Services.AuthAPI/Mango.Services.AuthAPI.csproj"
COPY . .
WORKDIR "/src/Mango.Services.AuthAPI"
RUN dotnet build "Mango.Services.AuthAPI.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Mango.Services.AuthAPI.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mango.Services.AuthAPI.dll"]
