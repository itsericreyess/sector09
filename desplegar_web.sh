#!/usr/bin/env sh
# Despliegue en un servidor Linux con Docker:  ./desplegar_web.sh [puerto]
set -e
cd "$(dirname "$0")"
PORT="${1:-8080}"
docker build -t sector09-web .
docker rm -f sector09 >/dev/null 2>&1 || true
docker run -d --name sector09 --restart unless-stopped -p "$PORT:80" sector09-web
echo "Sector 09 disponible en http://localhost:$PORT"
