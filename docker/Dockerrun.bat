@echo off
echo Running Redish Server Docker container...

if not exist "logs" mkdir logs

docker run -it --rm ^
  -p 6379:6379 ^
  -v "%cd%\redish.json:/app/redish.json" ^
  -v "%cd%\logs:/app/logs" ^
  jchristn/redish:%1
