@echo off
dotnet tool install fake-cli --tool-path .fake --version 5.13.7
dotnet tool install paket --tool-path .paket

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

.fake\fake.exe run build.fsx %*