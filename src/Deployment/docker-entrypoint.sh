#!/bin/sh
set -e

echo "🚀 Starting GestionHogar Application..."
echo "ℹ️  Migrations will be applied automatically by the application"
echo ""

# Debug: Verificar el entorno
echo "=== Debug Information ==="
echo "Current directory: $(pwd)"
echo "Files in directory:"
ls -lah
echo ""

# Verificar que la aplicación existe
if [ ! -f "./GestionHogar" ]; then
    echo "❌ Error: GestionHogar executable not found!"
    exit 1
fi

# Verificar que el archivo es ejecutable
if [ ! -x "./GestionHogar" ]; then
    echo "⚠️  GestionHogar is not executable, fixing permissions..."
    chmod +x ./GestionHogar
fi

# Verificar el tipo de archivo
echo "File type: $(file ./GestionHogar)"
echo ""

echo "✅ Starting application..."
./GestionHogar
