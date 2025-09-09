#!/bin/bash

echo "Running Redish Server Docker container..."

mkdir -p logs

docker run -it --rm \
  -p 6379:6379 \
  -v "$(pwd)/redish.json:/app/redish.json" \
  -v "$(pwd)/logs:/app/logs" \
  jchristn/redish:%1