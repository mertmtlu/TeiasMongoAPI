#!/bin/bash

echo "=== Docker Path Detection Test ==="
echo ""

# Test 1: Check if docker.exe exists and works
echo "Test 1: Checking docker.exe at Windows Docker Desktop location..."
DOCKER_EXE="/mnt/c/Program Files/Docker/Docker/resources/bin/docker.exe"
if [ -f "$DOCKER_EXE" ]; then
    echo "✓ File exists: $DOCKER_EXE"
    if "$DOCKER_EXE" --version 2>/dev/null; then
        echo "✓ Docker version check succeeded"
    else
        echo "✗ Docker version check failed (daemon may not be running)"
    fi
else
    echo "✗ File not found: $DOCKER_EXE"
fi
echo ""

# Test 2: Check current PATH
echo "Test 2: Checking if Docker is in PATH..."
if command -v docker &> /dev/null; then
    echo "✓ 'docker' found in PATH at: $(command -v docker)"
    docker --version 2>&1 || echo "✗ Docker command failed"
else
    echo "✗ 'docker' not found in PATH"
fi
echo ""

# Test 3: Check environment variable
echo "Test 3: Checking DOCKER_PATH environment variable..."
if [ -n "$DOCKER_PATH" ]; then
    echo "✓ DOCKER_PATH is set to: $DOCKER_PATH"
    if [ -f "$DOCKER_PATH" ]; then
        echo "✓ File exists"
        if "$DOCKER_PATH" --version 2>/dev/null; then
            echo "✓ Docker version check succeeded"
        else
            echo "✗ Docker version check failed"
        fi
    else
        echo "✗ File not found at DOCKER_PATH"
    fi
else
    echo "ℹ DOCKER_PATH not set (will use auto-detection)"
fi
echo ""

# Test 4: Recommend solution
echo "=== Recommended Solution ==="
echo ""
echo "Based on the tests above, to fix the Docker PATH issue:"
echo ""
if [ -f "$DOCKER_EXE" ]; then
    echo "1. Set DOCKER_PATH environment variable:"
    echo "   export DOCKER_PATH=\"$DOCKER_EXE\""
    echo ""
    echo "2. Add to ~/.bashrc for persistence:"
    echo "   echo 'export DOCKER_PATH=\"$DOCKER_EXE\"' >> ~/.bashrc"
    echo "   source ~/.bashrc"
    echo ""
    echo "3. OR start Docker Desktop on Windows:"
    echo "   - Open Docker Desktop application"
    echo "   - Wait for it to fully start"
    echo "   - Enable WSL2 integration in Docker Desktop settings"
else
    echo "Docker Desktop does not appear to be installed."
    echo "Please install Docker Desktop from: https://www.docker.com/products/docker-desktop/"
fi
echo ""
echo "After making changes, restart TeiasMongoAPI service/application."
