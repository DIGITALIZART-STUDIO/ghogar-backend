#!/bin/sh
set -e

echo "Running migrations..."
./efbundle
echo "Migrations ran successfully"

./out/PeruControl
