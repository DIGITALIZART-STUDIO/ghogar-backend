#!/bin/sh
set -e

echo "🚀 Starting GestionHogar Application..."
echo "ℹ️  Migrations will be applied automatically by the application"
echo ""

# Verificar que la aplicación existe
if [ ! -f "./GestionHogar" ]; then
    echo "❌ Error: GestionHogar executable not found!"
    exit 1
fi

./GestionHogar
