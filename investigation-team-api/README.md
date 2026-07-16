# InvestigationTeam API

## Descripción
API REST para gestionar equipos de investigación con agentes de diferentes perfiles.

## Estructura
```
investigation-team-api/
├── app/
│   ├── main.py           # Aplicación FastAPI
│   ├── requirements.txt  # Dependencias Python
│   └── Dockerfile        # Docker image
└── k8s/
    ├── namespace.yaml    # Namespace
    ├── configmap.yaml    # Configuración
    ├── deployment.yaml   # Deployment (2 réplicas)
    └── service.yaml      # Service LoadBalancer
```

## Modelos de datos

### Agent
- `id`: Identificador único
- `name`: Nombre del agente
- `role`: Rol (researcher, analyst, writer, coordinator, reviewer)
- `description`: Descripción opcional
- `skills`: Lista de habilidades
- `status`: Estado (active, inactive, busy)
- `created_at`: Fecha de creación
- `updated_at`: Última actualización

### Team
- `id`: Identificador único
- `name`: Nombre del equipo
- `description`: Descripción opcional
- `agents`: Lista de IDs de agentes
- `created_at`: Fecha de creación

## Endpoints

### Agentes
- `GET /agents` - Listar todos los agentes
- `GET /agents/{id}` - Obtener un agente
- `POST /agents` - Crear un agente
- `PUT /agents/{id}` - Actualizar un agente
- `DELETE /agents/{id}` - Eliminar un agente

### Teams
- `GET /teams` - Listar todos los equipos
- `GET /teams/{id}` - Obtener un equipo
- `POST /teams` - Crear un equipo
- `POST /teams/{id}/agents/{agent_id}` - Agregar agente a equipo
- `DELETE /teams/{id}/agents/{agent_id}` - Remover agente de equipo

### Otros
- `GET /health` - Health check
- `GET /docs` - Documentación Swagger

## Desarrollo local

```bash
cd app
pip install -r requirements.txt
uvicorn main:app --reload
```

## Despliegue en Kubernetes

```bash
# Construir imagen
docker build -t investigation-team-api:latest .

# Desplegar
kubectl apply -f k8s/

# Verificar
kubectl get pods -n investigation-team
```

## Ejemplos de uso

### Crear un agente
```bash
curl -X POST http://localhost/agents \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Alice",
    "role": "researcher",
    "description": "Especialista en investigación de mercados",
    "skills": ["análisis de datos", "reportes"]
  }'
```

### Crear un equipo
```bash
curl -X POST "http://localhost/teams?name=Equipo%20Alpha&description=Equipo%20de%20investigación"
```
