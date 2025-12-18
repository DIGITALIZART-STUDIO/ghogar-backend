# Multi-stage build for production (Debian based to avoid Alpine DNS issues)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

COPY *.sln ./
COPY .config ./.config
COPY GestionHogar.csproj .

# Install EF tools
RUN dotnet tool restore

# Restore packages for the target runtime
RUN HUSKY=0 dotnet restore

# Copy source code
COPY Configuration ./Configuration
COPY Controllers ./Controllers
COPY Migrations ./Migrations
COPY Model ./Model
COPY Properties ./Properties
COPY Utils ./Utils
COPY Services ./Services
COPY Program.cs .
COPY appsettings.json .

# Build and publish the application
RUN HUSKY=0 dotnet publish GestionHogar.csproj \
    -c Release \
    -o /app/out

# Runtime stage - Debian based
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install LibreOffice and dependencies
# Updates apt, installs libreoffice, fonts, and curl, then cleans up to keep image smaller
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libreoffice \
    libreoffice-l10n-es \
    libreoffice-l10n-en-gb \
    curl \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

# Create symlink to ensure 'soffice' command works (just in case)
RUN ln -sf /usr/bin/libreoffice /usr/bin/soffice

# Create directory for LibreOffice user profile
RUN mkdir -p /tmp/.config && chmod 777 /tmp/.config

# Create non-root user
RUN useradd -m -s /bin/bash appuser

# Copy published application and templates
COPY --from=build-env /app/out ./
COPY Templates ./Templates
COPY Fonts /usr/local/share/fonts/
COPY Deployment/docker-entrypoint.sh ./

# Make entrypoint executable
RUN chmod +x ./docker-entrypoint.sh

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

CMD ["./docker-entrypoint.sh"]
