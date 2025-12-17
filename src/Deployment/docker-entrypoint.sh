#!/bin/sh
set -e

echo "Running migrations..."

# Debug: mostrar información del environment
echo "Debug: Current working directory: $(pwd)"
echo "Debug: Connection string length: ${#ConnectionStrings__DefaultConnection}"

# Método compatible con sh: escapar caracteres problemáticos específicos
# Escapamos: comillas simples, dobles, backticks, dólar, y caracteres de control
export ConnectionStrings__DefaultConnection=$(
    printf '%s\n' "$ConnectionStrings__DefaultConnection" | \
    sed "s/'/'\\\\''/g; s/^\(.*\)$/'\1'/"
)

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
