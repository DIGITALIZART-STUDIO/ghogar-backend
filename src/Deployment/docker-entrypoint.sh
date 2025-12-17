#!/bin/sh
set -e

echo "Running migrations..."

# Debug: mostrar información del environment
echo "Debug: Current working directory: $(pwd)"
echo "Debug: Files in directory: $(ls -la)"
echo "Debug: Connection string length: ${#ConnectionStrings__DefaultConnection}"

# Método más robusto: usar printf %q para escapar todos los caracteres especiales
export ConnectionStrings__DefaultConnection=$(printf '%q' "$ConnectionStrings__DefaultConnection")

echo "Debug: Escaped connection string: $ConnectionStrings__DefaultConnection"

# Verificar que el archivo efbundle existe y es ejecutable
if [ ! -f "./efbundle" ]; then
    echo "Error: efbundle file not found!"
    exit 1
fi

if [ ! -x "./efbundle" ]; then
    echo "Error: efbundle is not executable!"
    exit 1
fi

echo "Debug: Executing efbundle..."
./efbundle
echo "Migrations ran successfully"

./GestionHogar
