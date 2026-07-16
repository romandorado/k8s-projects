# Contexto del Proyecto - Kubernetes Learning

## Estado Actual
- **Fecha**: 2026-07-16 (última actualización: 16:40)
- **Fase**: Terraria Server - Manifests creados, pendiente de probar

## Arquitectura Diseñada
```
┌─────────────────────────────────────────┐
│              Kubernetes Cluster          │
├─────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────────┐  │
│  │ Investigation│  │   Terraria      │  │
│  │ Team API     │  │   Server        │  │
│  │ (Stateless)  │  │   (Stateful)    │  │
│  └─────────────┘  └─────────────────┘  │
│  ┌─────────────────────────────────┐   │
│  │  Supermarket Frontend           │   │
│  │  (React + Nginx)                │   │
│  └─────────────────────────────────┘   │
│  ┌─────────────┐  ┌─────────────────┐  │
│  │ PostgreSQL   │  │  Redis Cache    │  │
│  │ (Database)   │  │  (Optional)     │  │
│  └─────────────┘  └─────────────────┘  │
└─────────────────────────────────────────┘
```

## Servicios a Desarrollar
1. **Terraria Server** - StatefulSet, PersistentVolume, Service (Puerto 7777)
2. **InvestigationTeam API** - REST API, Agentes con perfiles, Base de datos
3. **Supermarket Frontend** - React/Vue, Organización de compras

## Archivos Creados
- `k8s-projects/terraria-server/namespace.yaml`
- `k8s-projects/terraria-server/pvc.yaml`
- `k8s-projects/terraria-server/configmap.yaml`
- `k8s-projects/terraria-server/statefulset.yaml`
- `k8s-projects/terraria-server/service.yaml`
- `k8s-projects/terraria-server/README.md`

## Terraria Server - Completado
- Namespace: `terraria`
- StatefulSet con imagen `ryshe/terraria:latest`
- PersistentVolumeClaim: 5Gi para mundos
- Service: LoadBalancer en puerto 7777
- ConfigMap con parámetros configurables
- Health checks (readiness y liveness)

## Pendiente
- Probar despliegue de Terraria Server
- Crear InvestigationTeam API
- Crear Supermarket Frontend
- Probar despliegue completo

## Notas del Usuario
- Quiere algo usable a futuro
- Tiene experiencia básica con Docker/Kubernetes (1 año)
- Busca practicar cosas reales para su día a día
- Tiene servidor de Terraria que levanta de vez en cuando para jugar con sobrinos
