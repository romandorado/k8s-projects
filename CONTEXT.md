# Contexto del Proyecto - Kubernetes Learning

## Estado Actual
- **Fecha**: 2026-07-16 (última actualización: 17:00)
- **Fase**: Los 3 servicios están creados, pendiente de probar despliegue
- **Git**: Repositorio con 5 commits

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
1. **Terraria Server** - StatefulSet, PersistentVolume, Service (Puerto 7777) ✅
2. **InvestigationTeam API** - .NET 10, PostgreSQL, Agentes con perfiles ✅
3. **Supermarket Frontend** - React + Vite, Organización de compras ✅

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

## InvestigationTeam API - Completado
- Namespace: `investigation-team`
- **Backend**: C# .NET 10 Web API
- **Database**: PostgreSQL 16
- **ORM**: Entity Framework Core
- Modelos: Agent (roles: researcher, analyst, writer, coordinator, reviewer)
- Modelos: Team (agrupa agentes)
- Endpoints CRUD completos
- Deployment con 2 réplicas
- Service LoadBalancer en puerto 80
- Health checks HTTP
- Documentación Swagger automática

## Supermarket Frontend - Completado
- Namespace: `supermarket`
- **Frontend**: React 18 + Vite 5
- **Server**: Nginx (producción)
- **Storage**: LocalStorage
- Componentes: AddItemForm, ShoppingList, Categories, BudgetTracker
- Categorías: Frutas, Lácteos, Carnes, Panadería, Bebidas, Limpieza, Otros
- Funcionalidades: Filtrado por categorías, seguimiento de presupuesto
- Deployment con 2 réplicas
- Service LoadBalancer en puerto 80
- Diseño responsive con tema oscuro

## Pendiente
- Probar despliegue completo de los 3 servicios

## Notas del Usuario
- Quiere algo usable a futuro
- Tiene experiencia básica con Docker/Kubernetes (1 año)
- Busca practicar cosas reales para su día a día
- Tiene servidor de Terraria que levanta de vez en cuando para jugar con sobrinos
