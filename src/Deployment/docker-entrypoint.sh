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
exec dotnet GestionHogar.dll
