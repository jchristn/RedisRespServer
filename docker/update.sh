#!/bin/bash
# Pull the latest published images and recreate the stack. Non-destructive: named volumes are preserved.
DIR="$(cd "$(dirname "$0")" && pwd)"
docker compose -f "$DIR/compose.yaml" pull
docker compose -f "$DIR/compose.yaml" down
docker compose -f "$DIR/compose.yaml" up -d
docker ps -a
