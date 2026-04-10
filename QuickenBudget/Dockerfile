# https://github.com/vmenn-at-live/QuickenBudget
# Copyright (c) 2026 by Valerian Menn - MIT License

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["QuickenBudget/QuickenBudget.csproj", "QuickenBudget/"]
RUN dotnet restore "./QuickenBudget/QuickenBudget.csproj"
COPY . .
WORKDIR "/src/QuickenBudget"
RUN dotnet build "./QuickenBudget.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./QuickenBudget.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Debug stage (only used by Visual Studio during F5/debug)
FROM base AS debug
# Install extra debug tools/packages here
USER root
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
USER $APP_UID

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final

# Enable globalization
USER root
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuickenBudget.dll"]
