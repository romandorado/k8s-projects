# Aliases - Terraria Server

## Servidor de Terraria (k3s)

| Alias | Comando | Descripción |
|-------|---------|-------------|
| `terraria-on` | `k3s kubectl scale statefulset terraria-server -n terraria --replicas=1` | Arranca el servidor |
| `terraria-off` | `k3s kubectl scale statefulset terraria-server -n terraria --replicas=0` | Para el servidor |
| `terraria-status` | `k3s kubectl get pods -n terraria` | Ver estado de pods |
| `terraria-logs` | `k3s kubectl logs -n terraria terraria-server-0 --tail=50` | Ver logs del servidor |
| `agent-logs` | `k3s kubectl logs -n terraria -l app=terraria-agent --tail=50` | Ver logs del agente |
| `all-pods` | `k3s kubectl get pods --all-namespaces` | Todos los pods del cluster |
| `terraria-help` | Muestra esta ayuda | Ver comandos disponibles |

## Conexión al Servidor

### Local (cluster k3s local)
- **IP**: `172.30.138.92`
- **Puerto juego**: `7777`
- **Ingress (HTTP)**: `http://172.30.138.92:30808`
- **REST API (TShock)**: `172.30.138.92:30788`

### Remoto (gaming.andalusiaone.com)
- **Dominio**: `gaming.andalusiaone.com`
- **Puerto juego**: `30777` (NodePort)
- **Ingress (HTTP)**: `http://gaming.andalusiaone.com:30808`
- **REST API (TShock)**: `5.189.163.39:30788`
- **Cómo conectar**: Server > Join via IP > `gaming.andalusiaone.com:30777` (sin contraseña)

## Servicios via Ingress

Todos los servicios accesibles via nginx ingress en puerto 30808:

| Ruta | Servicio |
|------|----------|
| `/` | Homepage |
| `/it` | InvestigationTeam Frontend |
| `/api/*` | InvestigationTeam API (Swagger: `/api/swagger`) |
| `/supermarket` | Supermarket Frontend |
| `/supermarket-api/*` | Supermarket API (Swagger: `/supermarket-api/swagger`) |
| `/terraria-agent/*` | Terraria Agent (Swagger: `/terraria-agent/swagger`) |

## Ubicación de archivos

- **Proyecto**: `/home/roman/k8s-projects/`
- **Makefile**: `/home/roman/k8s-projects/Makefile` (build, deploy, verify)
- **Contexto**: `/home/roman/k8s-projects/CONTEXT.md`
