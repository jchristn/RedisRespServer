@echo off
echo Running Redish Dashboard Docker container...

docker run -it --rm ^
  -p 3002:3002 ^
  jchristn77/redish-ui:%1
