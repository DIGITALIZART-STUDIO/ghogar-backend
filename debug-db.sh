#!/bin/bash

echo "🔍 Database Connectivity Diagnostic"
echo "==================================="

# Check if we're in a container
if [ -f "/.dockerenv" ]; then
    echo "✅ Running inside Docker container"
else
    echo "⚠️  Not running inside Docker container"
fi

echo ""
echo "🌐 Network Information:"
echo "Current hostname: $(hostname)"
echo "IP addresses:"
ip addr show | grep "inet " | grep -v "127.0.0.1" | awk '{print "  " $2}'

echo ""
echo "🔍 DNS Resolution:"
echo "Testing db host resolution:"
if nslookup db 2>/dev/null; then
    echo "✅ DNS resolution successful"
else
    echo "❌ DNS resolution failed"
    echo "Trying to find db container IP..."
    # Try to find the db container IP
    DB_IP=$(getent hosts db | awk '{print $1}' | head -1)
    if [ ! -z "$DB_IP" ]; then
        echo "Found db container IP: $DB_IP"
    else
        echo "Could not find db container IP"
    fi
fi

echo ""
echo "🔌 Port Connectivity:"
echo "Testing connection to db:5432..."
if nc -z -w5 db 5432 2>/dev/null; then
    echo "✅ Port 5432 is open on db"
else
    echo "❌ Cannot connect to db:5432"
    # Try with timeout
    timeout 5 bash -c "</dev/tcp/db/5432" && echo "✅ Port 5432 accessible" || echo "❌ Port 5432 not accessible"
fi

echo ""
echo "🐘 PostgreSQL Health Check:"
if command -v pg_isready >/dev/null 2>&1; then
    echo "Testing pg_isready..."
    pg_isready -h db -p 5432 -U gestionhogar_user -d gestionhogar
else
    echo "pg_isready not available, trying telnet-like test..."
    timeout 5 bash -c "</dev/tcp/db/5432" && echo "✅ TCP connection successful" || echo "❌ TCP connection failed"
fi

echo ""
echo "📊 Environment Variables:"
echo "DATABASE_URL: ${DATABASE_URL:0:50}..."
echo "POSTGRES_DB: $POSTGRES_DB"
echo "POSTGRES_USER: $POSTGRES_USER"
echo "POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:0:10}..."
echo "DATABASE_HOST: ${DATABASE_HOST:-db}"

echo ""
echo "🔧 Recommendations:"
if ! nc -z -w5 db 5432 2>/dev/null; then
    echo "- Check if PostgreSQL container is running"
    echo "- Verify network configuration"
    echo "- Try setting DATABASE_HOST to container IP"
else
    echo "- Network connectivity is OK"
    echo "- Check PostgreSQL authentication"
    echo "- Verify database credentials"
fi
