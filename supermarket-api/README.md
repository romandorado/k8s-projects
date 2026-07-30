# Supermarket API (.NET 10 + PostgreSQL)

## Descripción
API REST en C# .NET 10 para gestionar listas de compra del supermercado. Desplegada en Kubernetes con ingress routing en `/supermarket-api/*`.

## Stack
- **Backend**: .NET 10 Web API
- **Database**: PostgreSQL 16
- **ORM**: Entity Framework Core
- **Documentación**: Swagger/Swashbuckle (configurable via `SWAGGER_BASE_PATH`)

## Estructura
```
supermarket-api/
├── src/Supermarket.Api/
│   ├── Controllers/
│   │   ├── ItemsController.cs    # CRUD de productos
│   │   └── ListsController.cs    # CRUD de listas
│   ├── Models/
│   │   └── Models.cs             # Entidades y DTOs
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Program.cs
│   └── appsettings.json
├── k8s/
│   ├── namespace.yaml
│   ├── secret.yaml
│   ├── postgres-pvc.yaml
│   ├── postgres-deployment.yaml
│   ├── postgres-service.yaml
│   ├── api-deployment.yaml      # Incluye SWAGGER_BASE_PATH
│   └── api-service.yaml
├── Dockerfile
└── README.md
```

## Endpoints

### Items
- `GET /api/items` - Listar items
- `GET /api/items/{id}` - Obtener item
- `POST /api/items` - Crear item
- `PUT /api/items/{id}` - Actualizar item
- `DELETE /api/items/{id}` - Eliminar item
- `PATCH /api/items/{id}/toggle` - Marcar/desmarcar
- `DELETE /api/items/completed` - Eliminar completados

### Lists
- `GET /api/lists` - Listar listas
- `GET /api/lists/{id}` - Obtener lista con items
- `POST /api/lists` - Crear lista
- `PUT /api/lists/{id}` - Actualizar lista
- `DELETE /api/lists/{id}` - Eliminar lista
- `GET /api/lists/{id}/summary` - Resumen de lista

### Otros
- `GET /health` - Health check
- `GET /swagger` - Documentación Swagger

## Acceso via Ingress

Todos los endpoints están disponibles en el cluster via nginx ingress:

- **Local**: `http://172.30.138.92:30808/supermarket-api/api/items`
- **Remoto**: `http://gaming.andalusiaone.com:30808/supermarket-api/api/items`
- **Swagger**: `/supermarket-api/swagger` (configurado con `SWAGGER_BASE_PATH=/supermarket-api`)

## Modelo de datos

### ShoppingItem
```json
{
  "id": "uuid",
  "name": "Leche",
  "quantity": 2,
  "category": "Dairy",
  "price": 1.50,
  "checked": false,
  "listId": "uuid",
  "createdAt": "2026-07-16T12:00:00Z",
  "updatedAt": "2026-07-16T12:00:00Z"
}
```

### Categorías
- `Fruits` - Frutas y Verduras
- `Dairy` - Lácteos
- `Meat` - Carnes
- `Bakery` - Panadería
- `Drinks` - Bebidas
- `Cleaning` - Limpieza
- `Other` - Otros

## Desarrollo local

```bash
cd src/Supermarket.Api
dotnet restore
dotnet run
```

API disponible en http://localhost:8000
Swagger en http://localhost:8000/swagger

## Despliegue en Kubernetes

```bash
# Construir imagen
docker build -t supermarket-api:latest .

# Importar a k3s
docker save supermarket-api:latest | sudo k3s ctr images import -

# Desplegar
kubectl apply -f k8s/

# Redeploy después de cambios
kubectl rollout restart deployment/supermarket-api -n supermarket
```

## Ejemplos de uso

### Crear una lista
```bash
curl -X POST http://gaming.andalusiaone.com:30808/supermarket-api/api/lists \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Compra semanal",
    "budget": 100.00
  }'
```

### Agregar item a una lista
```bash
curl -X POST "http://gaming.andalusiaone.com:30808/supermarket-api/api/items?listId=UUID_LISTA" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Leche",
    "quantity": 2,
    "category": 1,
    "price": 1.50
  }'
```
