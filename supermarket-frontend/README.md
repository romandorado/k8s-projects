# Supermarket Frontend (React + Vite)

## Descripción
Frontend para organizar compras del supermercado con categorías, presupuesto y persistencia local.

## Stack
- **Framework**: React 18
- **Build Tool**: Vite 5
- **Server**: Nginx (producción)
- **Storage**: LocalStorage

## Estructura
```
supermarket-frontend/
├── src/
│   ├── components/
│   │   ├── AddItemForm.jsx    # Formulario agregar productos
│   │   ├── ShoppingList.jsx   # Lista de compra agrupada
│   │   ├── Categories.jsx     # Filtro por categorías
│   │   └── BudgetTracker.jsx  # Seguimiento de presupuesto
│   ├── styles/
│   │   └── index.css          # Estilos CSS
│   ├── App.jsx                # Componente principal
│   └── main.jsx               # Entry point
├── k8s/
│   ├── namespace.yaml
│   ├── deployment.yaml
│   └── service.yaml
├── Dockerfile
├── nginx.conf
├── package.json
└── vite.config.js
```

## Funcionalidades
- **Lista de compra**: Agregar, eliminar, marcar productos
- **Categorías**: Frutas, Lácteos, Carnes, Panadería, Bebidas, Limpieza, Otros
- **Presupuesto**: Definir límite y trackear gastos
- **Persistencia**: Todo se guarda en LocalStorage
- **Diseño responsive**: Funciona en desktop y móvil

## Desarrollo local

```bash
npm install
npm run dev
```

La app estará disponible en http://localhost:5173

## Despliegue en Kubernetes

```bash
# Construir imagen
docker build -t supermarket-frontend:latest .

# Desplegar
kubectl apply -f k8s/

# Verificar
kubectl get pods -n supermarket
```

## Categorías disponibles
| ID | Nombre | Icono |
|---|---|---|
| fruits | Frutas y Verduras | 🥬 |
| dairy | Lácteos | 🥛 |
| meat | Carnes | 🥩 |
| bakery | Panadería | 🍞 |
| drinks | Bebidas | 🥤 |
| cleaning | Limpieza | 🧹 |
| other | Otros | 📦 |
