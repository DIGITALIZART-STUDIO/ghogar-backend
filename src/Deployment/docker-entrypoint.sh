#!/bin/sh
set -e

echo "🚀 Starting GestionHogar Application..."
echo "ℹ️  Migrations will be applied automatically by the application"
echo ""

# Verificar que la aplicación existe
if [ ! -f "./GestionHogar" ]; then
    echo "❌ Error: GestionHogar executable not found!"
    echo "Files in current directory:"
    ls -lah | head -20
    exit 1
fi

# Verificar que el archivo es ejecutable
if [ ! -x "./GestionHogar" ]; then
    echo "⚠️  GestionHogar is not executable, fixing permissions..."
    chmod +x ./GestionHogar
fi

echo "✅ GestionHogar found, starting application..."
./GestionHogar
