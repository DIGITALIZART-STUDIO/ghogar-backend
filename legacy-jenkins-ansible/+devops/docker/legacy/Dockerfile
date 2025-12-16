# Multi-stage build for production
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env
WORKDIR /app

COPY *.sln ./
COPY .config ./.config
COPY GestionHogar.csproj .

# Install EF tools
RUN dotnet tool restore

# Restore packages for the target runtime - this layer gets cached unless project files change
RUN HUSKY=0 dotnet restore --locked-mode -r linux-musl-x64

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
RUN HUSKY=0 dotnet publish GestionHogar.csproj -c Release -r linux-musl-x64 \
    --self-contained true \
    -o /app/out

# Create EF migrations bundle (self-contained)
RUN HUSKY=0 dotnet ef migrations bundle \
    -r linux-musl-x64 \
    --configuration Release \
    --self-contained \
    -o /app/out/efbundle

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS runtime
WORKDIR /app

# Install LibreOffice and dependencies
RUN apk add --no-cache \
    fontconfig \
    libreoffice \
    libreoffice-lang-en_us \
    libreoffice-lang-es \
    curl

# Create symlink to ensure 'soffice' command works
RUN ln -sf /usr/bin/libreoffice /usr/bin/soffice

# Create directory for LibreOffice user profile to avoid permissions issues
RUN mkdir -p /tmp/.config && chmod 777 /tmp/.config

# Create non-root user for security (because unlike some people, I care about security)
RUN adduser -D -s /bin/sh appuser

# Copy published application and templates
COPY --from=build-env /app/out ./
COPY Templates ./Templates
COPY Fonts /usr/local/share/fonts/
COPY Deployment/docker-entrypoint.sh ./

# Make scripts executable
RUN chmod +x ./docker-entrypoint.sh ./efbundle

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

CMD ["./docker-entrypoint.sh"]
