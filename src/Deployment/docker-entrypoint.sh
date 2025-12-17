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
    echo "🐛 Debug mode: Running comprehensive database connectivity test..."
    ./debug-db.sh
    echo "🐛 Debug mode: Done"
fi

exec dotnet GestionHogar.dll
