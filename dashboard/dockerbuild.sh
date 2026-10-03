#!/bin/bash
# Build the Redish dashboard for linux/amd64 and linux/arm64/v8, push to Docker Hub, then pull into the local repository.
# Run from the dashboard/ directory. Usage: ./dockerbuild.sh v1.0.0
if [ -z "$1" ]; then
  echo "Provide a tag argument for the build."
  echo "Example: ./dockerbuild.sh v1.0.0"
  echo "Done"
  exit 1
fi

cd "$(dirname "$0")" || exit 1

echo
echo "Building for linux/amd64 and linux/arm64/v8 and pushing to Docker Hub..."
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/redish-ui:$1 --tag jchristn77/redish-ui:latest --push . || { echo "Done"; exit 1; }

echo
echo "Pulling images into the local repository..."
docker pull jchristn77/redish-ui:$1
docker pull jchristn77/redish-ui:latest

echo "Done"
