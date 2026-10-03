#!/bin/bash
# Build Redish.Server for linux/amd64 and linux/arm64/v8, push to Docker Hub, then pull into the local repository.
# Run from the src/ directory. Usage: ./Dockerbuild.sh v1.0.0
if [ -z "$1" ]; then
  echo "Provide a tag argument for the build."
  echo "Example: ./Dockerbuild.sh v1.0.0"
  echo "Done"
  exit 1
fi

cd "$(dirname "$0")" || exit 1

echo
echo "Building for linux/amd64 and linux/arm64/v8 and pushing to Docker Hub..."
docker buildx build -f Redish.Server/Dockerfile --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/redish:$1 --tag jchristn77/redish:latest --push . || { echo "Done"; exit 1; }

echo
echo "Pulling images into the local repository..."
docker pull jchristn77/redish:$1
docker pull jchristn77/redish:latest

echo "Done"
