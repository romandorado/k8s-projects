# InvestigationTeam API (.NET 10 + PostgreSQL)

## Descripción
API REST en C# .NET 10 para gestionar equipos de investigación con agentes. Desplegada en Kubernetes con ingress routing en `/api/*`.

## Stack
- **Backend**: .NET 10 Web API
- **Database**: PostgreSQL 16
- **ORM**: Entity Framework Core
- **Auth**: JWT Bearer tokens
- **Documentación**: Swagger/Swashbuckle (configurable via `SWAGGER_BASE_PATH`)

## Estructura
```
investigation-team-api/
├── src/InvestigationTeam.Api/
│   ├── Controllers/
│   │   ├── AgentsController.cs
│   │   └── TeamsController.cs
│   ├── Models/
│   │   └── Models.cs
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── InvestigationTeam.Api.csproj
├── k8s/
│   ├── namespace.yaml
│   ├── secret.yaml
│   ├── postgres-pvc.yaml
│   ├── postgres-deployment.yaml
│   ├── postgres-service.yaml
│   ├── deployment.yaml      # Incluye SWAGGER_BASE_PATH
│   └── service.yaml
├── Dockerfile
└── README.md
```

## Modelos de datos

### Agent
- `Id`: Guid
- `Name`: string (requerido, max 100)
- `Role`: enum (Researcher, Analyst, Writer, Coordinator, Reviewer)
- `Description`: string (opcional)
- `Skills`: List<string>
- `Status`: enum (Active, Inactive, Busy)
- `CreatedAt`: DateTime
- `UpdatedAt`: DateTime

### Team
- `Id`: Guid
- `Name`: string (requerido, max 100)
- `Description`: string (opcional)
- `AgentIds`: List<Guid>
- `CreatedAt`: DateTime

## Endpoints

### Agentes (requieren JWT)
- `GET /api/agents` - Listar todos los agentes
- `GET /api/agents/{id}` - Obtener un agente
- `POST /api/agents` - Crear un agente
- `PUT /api/agents/{id}` - Actualizar un agente
- `DELETE /api/agents/{id}` - Eliminar un agente

### Teams (requieren JWT)
- `GET /api/teams` - Listar todos los equipos
- `GET /api/teams/{id}` - Obtener un equipo
- `POST /api/teams` - Crear un equipo
- `POST /api/teams/{id}/agents/{agentId}` - Agregar agente a equipo
- `DELETE /api/teams/{id}/agents/{agentId}` - Remover agente de equipo
- `DELETE /api/teams/{id}` - Eliminar equipo

### Otros
- `GET /health` - Health check
- `GET /swagger` - Documentación Swagger

## Acceso via Ingress

Todos los endpoints están disponibles en el cluster via nginx ingress:

- **Local**: `http://172.30.138.92:30808/api/agents`
- **Remoto**: `http://gaming.andalusiaone.com:30808/api/agents`
- **Swagger**: `/api/swagger` (configurado con `SWAGGER_BASE_PATH=/api`)

## Desarrollo local

```bash
cd src/InvestigationTeam.Api
dotnet restore
dotnet run
```

La API estará disponible en http://localhost:8000 y Swagger en http://localhost:8000/swagger

## Despliegue en Kubernetes

```bash
# Construir imagen
docker build -t investigation-team-api:latest .

# Importar a k3s
docker save investigation-team-api:latest | sudo k3s ctr images import -

# Desplegar
kubectl apply -f k8s/

# Redeploy después de cambios de config
kubectl rollout restart deployment/investigation-team-api -n investigation-team
```

## Ejemplos de uso

### Crear un agente
```bash
curl -X POST http://gaming.andalusiaone.com:30808/api/agents \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <JWT_TOKEN>" \
  -d '{
    "name": "Alice",
    "role": 0,
    "description": "Especialista en investigación",
    "skills": ["análisis de datos", "reportes"]
  }'
```

### Crear un equipo
```bash
curl -X POST http://gaming.andalusiaone.com:30808/api/teams \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <JWT_TOKEN>" \
  -d '{
    "name": "Equipo Alpha",
    "description": "Equipo de investigación principal"
  }'
```
