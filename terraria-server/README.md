# Terraria Server - Kubernetes

## Descripción
Servidor de Terraria TShock 6.1.0 (Terraria 1.4.5.6) desplegado en Kubernetes con persistencia de mundos, REST API habilitada y ChatBridge plugin para integración con el agente AI.

## Stack
- **Server**: TShock 6.1.0 (OTAPI 3.3.11) para Terraria 1.4.5.6
- **Runtime**: .NET 9 / .NET 10 (TShock)
- **Plugin**: ChatBridge (forwarding de chat in-game al agente)
- **REST API**: TShock REST API (puerto 7878)
- **Base de datos**: SQLite (tshock.sqlite en PVC)

## Archivos
- `namespace.yaml` - Namespace `terraria`
- `pvc.yaml` - PVC de 5Gi para mundos + SQLite DB
- `configmap.yaml` - Config del servidor (WORLD_NAME, MAX_PLAYERS, etc.)
- `statefulset.yaml` - StatefulSet del servidor (puertos 7777/7878/7879)
- `service.yaml` - Services para exponer puertos
- `local-ingress.yaml` - Todos los ingress del cluster (terraria-agent, homepage, IT, supermarket)
- `docker/Dockerfile` - Docker image custom con TShock 6.1.0
- `docker/bootstrap.sh` - Script de inicio (config TShock, crear usuario agent, permisos REST)
- `docker/chatbridge/ChatBridgePlugin.cs` - Plugin para forward de chat

## Puertos

| Puerto | Container | Tipo | Descripción |
|--------|-----------|------|-------------|
| 7777 | 7777 | NodePort 30777 | Juego |
| 7878 | 7878 | NodePort 30788 | REST API TShock |
| 7879 | 7879 | NodePort 30789 | ChatBridge plugin |

## Despliegue

### 1. Local
```bash
# Build Docker image
cd terraria-server/docker
docker build -t terraria-tshock:latest --provenance=false --sbom=false .

# Importar a k3s
docker save terraria-tshock:latest | sudo k3s ctr images import -

# Desplegar
kubectl apply -f namespace.yaml
kubectl apply -f pvc.yaml
kubectl apply -f configmap.yaml
kubectl apply -f statefulset.yaml
kubectl apply -f service.yaml
```

### 2. Remoto (sincronizar)
```bash
# Ver Makefile o usar:
docker save terraria-tshock:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
```

## REST API

La REST API de TShock está habilitada y funcional:
- **Token**: `terraria-agent-secret-token-2024`
- **Usuario agent**: grupo `admin` con permisos `tshock.rest.*`
- **Auth**: `X-Agent-Token` header (NO Bearer token)

Endpoints disponibles:
| Endpoint | Descripción |
|----------|-------------|
| `GET /v2/server/status` | Estado del servidor (sin auth) |
| `GET /v2/players/list?token=...` | Lista de jugadores |
| `POST /v2/server/broadcast?msg=...&token=...` | Broadcast (msg es query param) |
| `GET /v3/server/rawcmd?cmd=/playing&token=...` | Ejecutar comandos |

## ChatBridge Plugin
Forwardea el chat in-game al agente AI:
- Escucha en puerto 7879
- Recibe POST del agente para ejecutar comandos
- Soporta comandos: `bridge rain`, `bridge wind`, `bridge bloodmoon`, `bridge eclipse`

## Comandos del Agente
Los jugadores pueden interactuar con el agente via `/agente [comando]`:
- `/agente narrar [escena]` - Narración épica con Groq
- `/agente hora` - Describe la hora del mundo
- `/agente clima [tipo]` - Cambia el clima
- `/agente tiempo [hora]` - Cambia la hora
- `/agente invocar [boss]` - Invoca un boss
- `/agente consejo` - Consejo de juego
- `/agente peligro` - Advertencia dramática
- `/agente help` - Lista de comandos

## Conexión desde el cliente

### Local
Server > Join via IP > `172.30.138.92:7777`

### Remoto (gaming.andalusiaone.com)
Server > Join via IP > `gaming.andalusiaone.com:30777`

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

### Ver logs
```bash
kubectl logs -f terraria-server-0 -n terraria
```
