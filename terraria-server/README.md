# Terraria Server - Kubernetes

## Descripción
Servidor de Terraria desplegado en Kubernetes con persistencia de mundos.

## Archivos
- `namespace.yaml` - Namespace para el proyecto
- `pvc.yaml` - PersistentVolumeClaim para mundos
- `configmap.yaml` - Configuración del servidor
- `statefulset.yaml` - StatefulSet del servidor
- `service.yaml` - Service para exponer puerto 7777

## Despliegue

### 1. Crear namespace
```bash
kubectl apply -f namespace.yaml
```

### 2. Desplegar todos los recursos
```bash
kubectl apply -f .
```

### 3. Verificar estado
```bash
kubectl get pods -n terraria
kubectl get svc -n terraria
```

### 4. Conectarse al servidor
El servidor estará disponible en el IP externo del LoadBalancer en puerto 7777.

### 5. Ver logs
```bash
kubectl logs -f terraria-server-0 -n terraria
```

## Configuración
Editar `configmap.yaml` para cambiar:
- `WORLD_NAME` - Nombre del mundo
- `MAX_PLAYERS` - Máximo de jugadores
- `DIFFICULTY` - Dificultad (0=Normal, 1=Expert, 2=Master)
- `SERVER_PASSWORD` - Contraseña del servidor
- `MOTD` - Mensaje del día

## Mantenimiento

### Guardar mundo manualmente
```bash
kubectl exec -it terraria-server-0 -n terraria -- kill -SIGINT 1
```

### Reiniciar servidor
```bash
kubectl rollout restart statefulset terraria-server -n terraria
```

### Eliminar servidor (conservando mundos)
```bash
kubectl delete -f statefulset.yaml -n terraria
kubectl delete -f service.yaml -n terraria
# Los mundos se conservan en el PVC
