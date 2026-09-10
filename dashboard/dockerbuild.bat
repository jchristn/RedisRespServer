@ECHO OFF
IF "%1" == "" GOTO :Usage
ECHO.
ECHO Building for linux/amd64 and linux/arm64/v8 and pushing to Docker Hub...
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/redish-ui:%1 --tag jchristn77/redish-ui:latest --push .
IF ERRORLEVEL 1 GOTO :Done

ECHO.
ECHO Pulling images into the local repository...
docker pull jchristn77/redish-ui:%1
docker pull jchristn77/redish-ui:latest

GOTO :Done

:Usage
ECHO Provide a tag argument for the build.
ECHO Example: dockerbuild.bat v1.0.0

:Done
ECHO Done
@ECHO ON
