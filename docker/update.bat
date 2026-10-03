@ECHO OFF
REM Pull the latest published images and recreate the stack. Non-destructive: named volumes are preserved.
docker compose -f "%~dp0compose.yaml" pull
docker compose -f "%~dp0compose.yaml" down
docker compose -f "%~dp0compose.yaml" up -d
docker ps -a
@ECHO ON
