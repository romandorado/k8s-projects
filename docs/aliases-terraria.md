# Aliases - Terraria Server

## Servidor de Terraria (k3s)

| Alias | Comando | Descripción |
|-------|---------|-------------|
| `terraria-on` | `kubectl scale statefulset terraria-server -n terraria --replicas=1` | Arranca el servidor |
| `terraria-off` | `kubectl scale statefulset terraria-server -n terraria --replicas=0` | Para el servidor |
| `terraria-status` | `kubectl get pods -n terraria` | Ver estado de pods |
| `terraria-logs` | `kubectl logs -n terraria terraria-server-0 --tail=50` | Ver logs del servidor |
| `agent-logs` | `kubectl logs -n terraria -l app=terraria-agent --tail=50` | Ver logs del agente |
| `terraria-help` | Muestra esta ayuda | Ver comandos disponibles |

## Conexión Hamachi

Cuando el servidor corre en Linux, tu amigo se conecta via:
- **IP**: `25.35.4.105`
- **Puerto**: `7777`

**Activar forwarding** (cuando usas Linux):
```bash
# En PowerShell como Administrador (Windows):
netsh interface portproxy add v4tov4 listenport=7777 listenaddress=25.35.4.105 connectport=7777 connectaddress=172.30.138.92
```

**Desactivar forwarding** (cuando usas Windows directamente):
```bash
# En PowerShell como Administrador (Windows):
netsh interface portproxy delete v4tov4 listenport=7777 listenaddress=25.35.4.105
```

## Ubicación de archivos

- **Aliases**: `~/.bash_aliases`
- **Config**: `~/.bashrc` (carga bash_aliases automáticamente)
- **Contexto**: `/home/roman/k8s-projects/CONTEXT.md`
