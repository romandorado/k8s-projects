# Homepage Dashboard Design

## Overview

A lightweight homepage service that provides a central dashboard to access all Kubernetes services in the cluster. Simple HTML/CSS served by Nginx, matching the existing dark theme of the Supermarket frontend.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                  K8S CLUSTER                         │
│                                                      │
│   NAMESPACE: homepage                                │
│   ┌──────────────────────────────────────────────┐  │
│   │  HOMEPAGE (Deployment, 1 replica)            │  │
│   │  - Nginx Alpine serving static HTML           │  │
│   │  - Port: 80 → Service LoadBalancer: 30080     │  │
│   │  - 3 cards linking to services                │  │
│   └──────────────────────────────────────────────┘  │
│                                                      │
│   Links to:                                          │
│   - Terraria Server (port 30161)                     │
│   - InvestigationTeam API (port 32444)               │
│   - Supermarket Frontend (port 30222)                │
│                                                      │
└─────────────────────────────────────────────────────┘
```

## Components

### 1. Static HTML (`index.html`)

Single-page dashboard with:
- Header: "K8S Dashboard - Roman's Home Lab"
- 3 service cards in a responsive grid
- Each card contains: icon, name, description, action button
- Dark theme matching supermarket (`#0f172a` background)

### 2. Nginx Config (`nginx.conf`)

Minimal Nginx config:
- Listen on port 80
- Serve static files from `/usr/share/nginx/html/`
- Gzip compression for HTML/CSS

### 3. Dockerfile

```dockerfile
FROM nginx:alpine
COPY index.html /usr/share/nginx/html/
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

Image size: ~30MB (nginx:alpine)

### 4. Kubernetes Manifests

**namespace.yaml** — `homepage` namespace
**deployment.yaml** — 1 replica, nginx:alpine, imagePullPolicy: IfNotPresent
**service.yaml** — LoadBalancer, port 80 → nodePort 30080

## Service Cards

| Card | Icon | Description | Link |
|------|------|-------------|------|
| Terraria Server | 🎮 | Game server - Puerto 7777 | `http://<host>:30161` |
| InvestigationTeam API | 🔍 | .NET 10 API + PostgreSQL | `http://<host>:32444/swagger` |
| Supermarket | 🛒 | Frontend + API + PostgreSQL | `http://<host>:30222` |

## Visual Design

- Background: `#0f172a` (dark navy)
- Cards: `#1e293b` with `#334155` border
- Text: `#f1f5f9` (light)
- Accent: `#3b82f6` (blue) for buttons
- Hover: Cards lift with shadow
- Responsive: 3 columns desktop, 1 column mobile

## Files to Create

```
k8s-projects/homepage/
├── Dockerfile
├── nginx.conf
├── index.html
└── k8s/
    ├── namespace.yaml
    ├── deployment.yaml
    └── service.yaml
```

## Deployment

1. Build Docker image: `docker build -t homepage:latest .`
2. Import to k3s: `docker save homepage:latest | k3s ctr images import -`
3. Apply manifests: `kubectl apply -f k8s/`
4. Access: `http://<host>:30080`

## Constraints

- No external dependencies (pure HTML/CSS)
- No JavaScript required
- Image size < 50MB
- Consistent with existing project style
