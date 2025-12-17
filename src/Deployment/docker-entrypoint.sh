#!/bin/sh
set -e

echo "🚀 Starting GestionHogar Application..."
echo "ℹ️  Migrations will be applied automatically by the application"
echo ""

# Verificar que la DLL existe
if [ ! -f "./GestionHogar.dll" ]; then
    echo "❌ Error: GestionHogar.dll not found!"
    echo "Files in current directory:"
    ls -lah | head -20
    exit 1
fi

echo "✅ Starting application with dotnet runtime..."

# Optional: Debug database connectivity before starting app
if [ "${DEBUG_DB:-false}" = "true" ]; then
    echo "🐛 Debug mode: Testing database connectivity..."
    echo "   Host: db"
    echo "   Port: 5432"
    if command -v nc >/dev/null 2>&1; then
        nc -zv db 5432 2>&1 | head -1
    else
        echo "   nc not available, skipping connectivity test"
    fi
    echo "🐛 Debug mode: Done"
fi

exec dotnet GestionHogar.dll
