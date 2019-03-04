@echo off
cls

dotnet restore build.proj
dotnet fake run build.fsx target $@