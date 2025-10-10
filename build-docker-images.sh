#!/bin/bash

# Build Docker images for code execution environments
# Run this script from the repository root

echo "Building Docker executor images..."

# Python executor
echo "Building Python executor..."
docker build -f Dockerfiles/python-executor.Dockerfile -t python-executor:latest .
if [ $? -eq 0 ]; then
    echo "✓ Python executor built successfully"
else
    echo "✗ Failed to build Python executor"
    exit 1
fi

# Node.js executor
echo "Building Node.js executor..."
docker build -f Dockerfiles/nodejs-executor.Dockerfile -t nodejs-executor:latest .
if [ $? -eq 0 ]; then
    echo "✓ Node.js executor built successfully"
else
    echo "✗ Failed to build Node.js executor"
    exit 1
fi

# .NET executor
echo "Building .NET executor..."
docker build -f Dockerfiles/dotnet-executor.Dockerfile -t dotnet-executor:latest .
if [ $? -eq 0 ]; then
    echo "✓ .NET executor built successfully"
else
    echo "✗ Failed to build .NET executor"
    exit 1
fi

# Java executor
echo "Building Java executor..."
docker build -f Dockerfiles/java-executor.Dockerfile -t java-executor:latest .
if [ $? -eq 0 ]; then
    echo "✓ Java executor built successfully"
else
    echo "✗ Failed to build Java executor"
    exit 1
fi

echo ""
echo "All Docker images built successfully!"
echo ""
echo "Images created:"
docker images | grep -E "python-executor|nodejs-executor|dotnet-executor|java-executor"
