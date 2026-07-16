# Homepage Dashboard - Task 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create project structure and Nginx configuration for a static HTML homepage dashboard

**Architecture:** Single-container static site served by Nginx alpine, following existing patterns from supermarket-frontend

**Tech Stack:** Nginx alpine, Docker

## Global Constraints

- Working directory: `/home/roman/k8s-projects/homepage`
- Follow existing patterns from `supermarket-frontend/`
- Use Nginx alpine base image
- Serve static files on port 80
- No backend proxy required (unlike supermarket-frontend)

## File Structure

```
k8s-projects/homepage/
├── nginx.conf          # Nginx server configuration
├── Dockerfile          # Container build instructions
└── k8s/                # Kubernetes manifests (created but empty for now)
```

## Tasks

### Task 1: Create Project Structure and Nginx Configuration

**Files:**
- Create: `/home/roman/k8s-projects/homepage/nginx.conf`
- Create: `/home/roman/k8s-projects/homepage/Dockerfile`
- Create: `/home/roman/k8s-projects/homepage/index.html` (placeholder)
- Create: `/home/roman/k8s-projects/homepage/k8s/` (empty directory)
- Test: `/home/roman/k8s-projects/homepage/test-config.sh`

**Interfaces:**
- Consumes: None (first task)
- Produces: Nginx config serving static files on port 80, Dockerfile for building image

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p /home/roman/k8s-projects/homepage/k8s
```

- [ ] **Step 2: Create placeholder index.html**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Homepage Dashboard</title>
</head>
<body>
    <h1>Homepage Dashboard</h1>
    <p>Coming soon...</p>
</body>
</html>
```

- [ ] **Step 3: Create nginx.conf**

```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ =404;
    }

    gzip on;
    gzip_types text/html text/css application/javascript;
}
```

- [ ] **Step 4: Create Dockerfile**

```dockerfile
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY index.html /usr/share/nginx/html/
EXPOSE 80
```

- [ ] **Step 5: Create test script**

```bash
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
```

- [ ] **Step 6: Make test script executable and run it**

```bash
chmod +x test-config.sh
./test-config.sh
```

Expected: "All tests passed!"

- [ ] **Step 7: Commit changes**

```bash
cd /home/roman/k8s-projects
git add homepage/
git commit -m "feat(homepage): add project structure with nginx config and Dockerfile"
```

## Verification

After implementation, verify:
1. Directory structure exists: `ls -la /home/roman/k8s-projects/homepage/`
2. Test script passes: `./test-config.sh`
3. Docker build succeeds (optional): `docker build -t homepage-test .`
