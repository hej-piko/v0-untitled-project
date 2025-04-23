#!/bin/bash

# Install .NET SDK
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 6.0
export PATH="$PATH:$HOME/.dotnet"

# Install required packages
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 6.0.8

# Build the application
dotnet restore
dotnet publish -c Release -o publish

echo "Build completed"
