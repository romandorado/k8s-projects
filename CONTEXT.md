# Contexto del Proyecto - Kubernetes Learning

## Estado Actual
- **Fecha**: 2026-07-17 (última actualización: 19:50)
- **Fase**: Despliegue en Kubernetes
- **Git**: Repositorio con 22 commits
- **GitHub**: https://github.com/romandorado/k8s-projects

## Arquitectura Final
```
┌─────────────────────────────────────────────────────────────────┐
│                     KUBERNETES CLUSTER                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   NAMESPACE: terraria                                            │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  TERRARIA SERVER (StatefulSet)                           │  │
│   │  - Puerto: 7777                                          │  │
│   │  - PersistentVolume: 5Gi para mundos                     │  │
│   │  - ConfigMap con parámetros                              │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: investigation-team                                  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  INVESTIGATIONTEAM API                                   │  │
│   │  - .NET 10 API (2 réplicas) → PostgreSQL 16             │  │
│   │  - Puerto API: 80 | Puerto DB: 5432                     │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: investigation-team-frontend                         │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  ANGULAR FRONTEND (Deployment, 1 replica)                │  │
│   │  - Angular 22 + Nginx                                    │  │
│   │  - Puerto: 80 → Service LoadBalancer: 30081              │  │
│   │  - Login, Dashboard CRUD, Chat con Gemini                │  │
│   ├──────────────────────────────────────────────────────────┤  │
│   │  CHAT BACKEND API (Deployment, 1 replica)                │  │
│   │  - .NET 10 API + PostgreSQL 16                           │  │
│   │  - Puerto: 8000 → Service LoadBalancer: 32445            │  │
│   │  - JWT Auth, Gemini AI, InvestigationTeam Proxy          │  │
│   ├──────────────────────────────────────────────────────────┤  │
│   │  CHAT DB (Deployment, 1 replica)                         │  │
│   │  - PostgreSQL 16                                         │  │
│   │  - Puerto: 5432 (ClusterIP)                              │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: supermarket                                         │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  SUPERMARKET STACK                                       │  │
│   │  - Angular 22 Frontend (2 réplicas) → .NET 10 API       │  │
│   │  - .NET 10 API (2 réplicas) → PostgreSQL 16             │  │
│   │  - Puerto Frontend: 80 | Puerto API: 80 | DB: 5432      │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: homepage                                            │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  HOMEPAGE (Deployment, 1 replica)                        │  │
│   │  - Nginx Alpine serving static HTML                      │  │
│   │  - Puerto: 80 → Service LoadBalancer: 30080              │  │
│   │  - Dashboard con links a todos los servicios             │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Stack Tecnológico

| Servicio | Tecnología | Workload | Service Type | Puerto |
|----------|------------|----------|--------------|--------|
| Terraria Server | Docker image | StatefulSet | LoadBalancer | 7777 |
| InvestigationTeam API | .NET 10 | Deployment (2) | LoadBalancer | 80 |
| InvestigationTeam DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |
| InvestigationTeam Frontend | Angular 22 + Nginx | Deployment (1) | LoadBalancer | 30081 |
| InvestigationTeam Chat API | .NET 10 + Gemini | Deployment (1) | LoadBalancer | 32445 |
| InvestigationTeam Chat DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |
| Supermarket Frontend | Angular 22 + Nginx | Deployment (2) | LoadBalancer | 80 |
| Supermarket API | .NET 10 | Deployment (2) | LoadBalancer | 80 |
| Supermarket DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |
| Homepage | HTML/CSS + Nginx | Deployment (1) | LoadBalancer | 30080 |

## Archivos del Proyecto

```
k8s-projects/
├── terraria-server/
│   ├── namespace.yaml
│   ├── pvc.yaml
│   ├── configmap.yaml
│   ├── statefulset.yaml
│   └── service.yaml
├── investigation-team-api/
│   ├── src/InvestigationTeam.Api/   # Backend .NET 10
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── secret.yaml
│   │   ├── postgres-pvc.yaml
│   │   ├── postgres-deployment.yaml
│   │   ├── postgres-service.yaml
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   └── Dockerfile
├── investigation-team-chat-backend/
│   ├── src/InvestigationTeam.Chat.Api/  # Backend .NET 10 + Gemini
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── secret.yaml
│   │   ├── postgres-pvc.yaml
│   │   ├── postgres-deployment.yaml
│   │   ├── postgres-service.yaml
│   │   ├── api-deployment.yaml
│   │   └── api-service.yaml
│   └── Dockerfile
├── investigation-team-frontend/
│   ├── src/app/                     # Frontend Angular 22
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── Dockerfile
│   └── nginx.conf
├── supermarket-api/
│   ├── src/Supermarket.Api/         # Backend .NET 10
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── secret.yaml
│   │   ├── postgres-pvc.yaml
│   │   ├── postgres-deployment.yaml
│   │   ├── postgres-service.yaml
│   │   ├── api-deployment.yaml
│   │   └── api-service.yaml
│   └── Dockerfile
├── supermarket-frontend/
│   ├── src/app/                     # Frontend Angular 22
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── Dockerfile
│   └── nginx.conf
├── homepage/
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── index.html
│   ├── Dockerfile
│   └── nginx.conf
├── CONTEXT.md
└── .gitignore
```

## Pendiente
- Verificar cluster Kubernetes disponible ✅
- Desplegar Homepage ✅ (puerto 30080)
- Desplegar InvestigationTeam API ✅ (puerto 32444)
- Desplegar InvestigationTeam Chat Backend ✅ (puerto 32445)
- Desplegar InvestigationTeam Frontend ✅ (puerto 30081)
- Desplegar Terraria Server
- Desplegar Supermarket (Frontend + API)
- Verificar funcionamiento de todos los servicios

## Notas del Usuario
- Quiere algo usable a futuro
- Tiene experiencia básica con Docker/Kubernetes (1 año)
- Busca practicar cosas reales para su día a día
- Tiene servidor de Terraria que levanta de vez en cuando para jugar con sobrinos
- Skills: .NET, Angular, PostgreSQL
- Idioma: Español (comunicación y documentación)

## Perfil de Trabajo - Opencode (IA)

### Comandos Disponibles
| Comando | Descripción |
|---------|-------------|
| `sudo k3s kubectl get pods --all-namespaces` | Ver estado de pods |
| `sudo k3s kubectl logs -n <ns> <pod> --tail=N` | Ver logs de un pod |
| `sudo k3s kubectl describe pod -n <ns> <pod>` | Diagnosticar problemas de pod |
| `sudo k3s kubectl rollout restart deployment/<name> -n <ns>` | Redeploy sin rebuild |
| `sudo docker build --no-cache -t <tag>:latest .` | Build de imagen Docker |
| `sudo docker save <tag>:latest \| sudo k3s ctr images import -` | Importar imagen a k3s |
| `curl -s -X POST http://localhost:<port>/<path> -H "Content-Type: application/json" -d '{...}'` | Test de endpoints |
| `sudo k3s kubectl exec -n <ns> <pod> -- curl -s http://<svc>:<port>/<path>` | Test dentro del cluster |

### Flujo de Debugging (Resiliente)
1. **Verificar el error exacto**: logs del pod, describe pod, curl directo
2. **Probar por capas**: container → pod → service → nginx proxy → browser
3. **Confirmar antes de asumir**: no decir "funciona" sin probar con curl o kubectl
4. **Hard refresh después de rebuild**: recordar al usuario hacer Ctrl+F5
5. **DB nueva = sin datos**: después de EnsureCreated, recordar que hay que registrar usuarios

### Errores Comunes y Soluciones
| Error | Causa | Solución |
|-------|-------|----------|
| `502 Bad Gateway` | Health check falla o servicio no listo | Verificar readiness probe, agregar `/health` endpoint |
| `500 Internal Server Error` | Tabla no existe en DB | `EnsureCreated()` o migraciones EF |
| `401 Unauthorized` en login | No hay usuarios | Registrar usuario primero |
| HTML roto en navegador | `<a>` anidado dentro de `<a>` | Usar `<div>` con `onclick` o `event.stopPropagation()` |
| Pod `0/1 Ready` | Readiness probe retorna error | Verificar que el health check no requiera auth |

### Reglas de Construcción
1. **Siempre probar el backend por curl** antes de declarar que funciona
2. **Rebuild con `--no-cache`** para evitar imágenes stale
3. **Siempre apply + rollout restart** después de cambiar deployment yaml
4. **Verificar pods Ready** después de cada cambio
5. **Documentar cada fix** en progreso o CONTEXT.md

### Stack del Proyecto
- **Runtime**: k3s (轻量 Kubernetes)
- **Registry**: Docker local → k3s ctr images import
- **Frontend**: Angular 22 + Nginx (proxy reverso)
- **Backend**: .NET 10 + Entity Framework Core
- **DB**: PostgreSQL 16
- **AI**: Google Gemini API
- **Auth**: JWT + BCrypt
- **Puertos LoadBalancer**: 30080 (homepage), 30081 (IT frontend), 32444 (IT API), 32445 (chat API)
