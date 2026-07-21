# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
# .NET 10 Linux images: Ubuntu Noble (10.0-noble) only — Debian is not supported in .NET 10.

# Base stage: used directly by Visual Studio (F5 / fast mode debug).
# tesseract-ocr is installed here so image text extraction works out of the box.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
RUN apt-get update \
  && apt-get install -y --no-install-recommends tesseract-ocr \
  && rm -rf /var/lib/apt/lists/*
# Verify tesseract is present and runnable — build fails here if something went wrong.
RUN tesseract --version | head -n1
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy every csproj the web app references so `dotnet restore` can resolve the full
# project graph before the rest of the source is copied (keeps the restore layer cached).
COPY ["PicStashly/PicStashly.csproj", "PicStashly/"]
COPY ["PicStashly.Core/PicStashly.Core.csproj", "PicStashly.Core/"]
COPY ["PicStashly.Data/PicStashly.Data.csproj", "PicStashly.Data/"]
COPY ["PicStashly.Data.Npgsql/PicStashly.Data.Npgsql.csproj", "PicStashly.Data.Npgsql/"]
COPY ["PicStashly.Data.MySql/PicStashly.Data.MySql.csproj", "PicStashly.Data.MySql/"]
COPY ["PicStashly.Data.Sqlite/PicStashly.Data.Sqlite.csproj", "PicStashly.Data.Sqlite/"]
COPY ["PicStashly.Data.Sql/PicStashly.Data.Sql.csproj", "PicStashly.Data.Sql/"]
COPY ["PicStashly.Models/PicStashly.Models.csproj", "PicStashly.Models/"]
COPY ["PicStashly.Services/PicStashly.Services.csproj", "PicStashly.Services/"]
RUN dotnet restore "./PicStashly/PicStashly.csproj"
COPY . .
WORKDIR "/src/PicStashly"
# Restore client-side libraries (wwwroot/lib). Build package may not run in Docker, so run CLI explicitly.
RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli --version 2.1.113 && \
    export PATH="${PATH}:/root/.dotnet/tools" && \
    libman restore --verbose
RUN test -d wwwroot/lib && test -f wwwroot/lib/jquery/dist/jquery.min.js || (echo "ERROR: wwwroot/lib missing; LibMan restore may have failed." && exit 1)
RUN dotnet build "./PicStashly.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PicStashly.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
# Ensure wwwroot/lib is in the publish output (publish can omit content added after restore)
RUN cp -r /src/PicStashly/wwwroot/lib /app/publish/wwwroot/
# Scoped CSS (Razor) may be at publish root; copy to wwwroot so UseStaticFiles can serve it
RUN if [ -f /app/publish/PicStashly.styles.css ]; then cp /app/publish/PicStashly.styles.css /app/publish/wwwroot/; fi

# Final stage: inherits tesseract from base (Ubuntu Noble).
# Used by both docker compose and Visual Studio in regular (non-fast) mode.
FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PicStashly.dll"]
