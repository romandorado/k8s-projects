# Supermarket Frontend (Angular + .NET API)

## Descripción
Frontend Angular para gestionar listas de compra del supermercado. Se conecta a la Supermarket API via ingress routing en `/supermarket-api/api`.

## Stack
- **Framework**: Angular 22
- **Server**: Nginx (producción)
- **API**: .NET 10 Web API + PostgreSQL 16
- **API URL**: `/supermarket-api/api` (configurado via `apiUrl` en ShoppingService)

## Estructura
```
supermarket-frontend/
├── src/
│   ├── app/
│   │   ├── components/
│   │   │   ├── item-list/
│   │   │   └── ...
│   │   ├── services/
│   │   │   └── shopping.service.ts   # API calls con apiUrl = '/supermarket-api/api'
│   │   ├── models/
│   │   │   └── item.model.ts
│   │   └── app.config.ts
│   ├── index.html       # base href = '/supermarket/'
│   ├── main.ts
│   └── styles.css
├── k8s/
│   ├── namespace.yaml
│   ├── deployment.yaml
│   └── service.yaml
├── Dockerfile
├── nginx.conf
├── package.json
└── angular.json
```

## Funcionalidades
- **Listas de compra**: Crear, editar, eliminar listas
- **Items**: Agregar, marcar, eliminar productos por categoría
- **Presupuesto**: Definir límite y trackear gastos
- **Categorías**: Fruits, Dairy, Meat, Bakery, Drinks, Cleaning, Other
- **API persistente**: Todos los datos se guardan en PostgreSQL via Supermarket API

## Acceso via Ingress

- **Local**: `http://172.30.138.92:30808/supermarket/`
- **Remoto**: `http://gaming.andalusiaone.com:30808/supermarket/`

El frontend se sirve via nginx ingress con regex rewrite (`/supermarket(/|$)(.*)` → `/$2`).
El `base href` es `/supermarket/`, todas las API calls van a `/supermarket-api/api/*`.

## Desarrollo local

```bash
npm install
ng serve
```

La app estará disponible en http://localhost:4200

## Despliegue en Kubernetes

```bash
# Construir imagen
docker build -t supermarket-frontend:latest .

# Importar a k3s
docker save supermarket-frontend:latest | sudo k3s ctr images import -

# Desplegar
kubectl apply -f k8s/

# Redeploy después de cambios
kubectl rollout restart deployment/supermarket-frontend -n supermarket
```

## Categorías disponibles
| ID | Nombre | Icono |
|---|---|---|
| 0 | Fruits | 🥬 |
| 1 | Dairy | 🥛 |
| 2 | Meat | 🥩 |
| 3 | Bakery | 🍞 |
| 4 | Drinks | 🥤 |
| 5 | Cleaning | 🧹 |
| 6 | Other | 📦 |
