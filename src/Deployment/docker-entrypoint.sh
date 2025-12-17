#!/bin/sh
set -e

echo "Running migrations..."
# Escapar caracteres especiales en el connection string antes de ejecutar efbundle
# Los caracteres + = / pueden causar problemas de sintaxis en el script bash del bundle
export ConnectionStrings__DefaultConnection=$(echo "$ConnectionStrings__DefaultConnection" | sed 's/[+=/]/\\&/g')
./efbundle
echo "Migrations ran successfully"

./GestionHogar
