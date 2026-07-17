#!/bin/bash
# Test script to verify nginx configuration files

set -e

echo "Testing homepage configuration..."

# Test 1: Check if nginx.conf exists and has expected content
if ! grep -q "listen 80;" nginx.conf; then
    echo "FAIL: nginx.conf missing 'listen 80;' directive"
    exit 1
fi

if ! grep -q "root /usr/share/nginx/html;" nginx.conf; then
    echo "FAIL: nginx.conf missing root directive"
    exit 1
fi

if ! grep -q "try_files \$uri \$uri/ =404;" nginx.conf; then
    echo "FAIL: nginx.conf missing try_files directive"
    exit 1
fi

# Test 2: Check if Dockerfile exists and has expected content
if ! grep -q "FROM nginx:alpine" Dockerfile; then
    echo "FAIL: Dockerfile missing nginx:alpine base image"
    exit 1
fi

if ! grep -q "COPY nginx.conf /etc/nginx/conf.d/default.conf" Dockerfile; then
    echo "FAIL: Dockerfile missing nginx.conf copy instruction"
    exit 1
fi

if ! grep -q "EXPOSE 80" Dockerfile; then
    echo "FAIL: Dockerfile missing EXPOSE 80"
    exit 1
fi

# Test 3: Check if index.html exists
if [ ! -f index.html ]; then
    echo "FAIL: index.html not found"
    exit 1
fi

# Test 4: Check if k8s directory exists
if [ ! -d k8s ]; then
    echo "FAIL: k8s directory not found"
    exit 1
fi

echo "All tests passed!"
exit 0