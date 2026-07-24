# Contexto del Proyecto - Kubernetes Learning

## Estado Actual
- **Fecha**: 2026-07-24 (última actualización: 21:30)
- **Fase**: Terraria Server con TShock 6.1.0 + Terraria 1.4.5.6 + Agent con crafting DB + Wiki fallback + auto events
- **Git**: Repositorio con 30+ commits
- **GitHub**: https://github.com/romandorado/k8s-projects
- **Servidor Externo**: vmi3205971 (gaming.andalusiaone.com) - 6 CPU, 11GB RAM, 96GB SSD
- **Servidor Local**: k3s local (172.30.138.92) - mantenido como backup
- **Sudoers**: roman tiene NOPASSWD para docker, kubectl, k3s (rutas: /usr/bin/docker, /usr/local/bin/kubectl, /usr/local/bin/k3s)

## Servidor Externo - gaming.andalusiaone.com
- **IP**: 5.189.163.39
- **Hostname**: srv01.gaming.andalusiaone.com / vmi3205971
- **OS**: Ubuntu 24.04.4 LTS
- **CPU**: 6 cores AMD EPYC
- **RAM**: 11GB (8.2GB libres)
- **Disk**: 96GB SSD (73GB libres)
- **Docker**: 29.3.1
- **K3s**: v1.36.2+k3s1
- **SSH**: roman@srv01.gaming.andalusiaone.com (key auth)
- **Ingress**: nginx ingress controller en puerto 30808 (HTTP) / 30181 (HTTPS)
- **Dominio**: gaming.andalusiaone.com (apunta a 5.189.163.39)

## Arquitectura Final
```
┌─────────────────────────────────────────────────────────────────┐
│                     KUBERNETES CLUSTER                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   NAMESPACE: terraria                                            │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  TERRARIA SERVER (StatefulSet)                           │  │
│   │  - Image: terraria-tshock (TShock 6.1.0 + 1.4.5.6)     │  │
│   │  - Puerto: 7777 (game) + 7878 (REST API)                │  │
│   │  - PersistentVolume: 5Gi para mundos + SQLite DB         │  │
│   │  - REST API habilitado con token auth                    │  │
│   │  - Agent user con permisos admin (REST API funcional)     │  │
│   │  - ✅ Working: TShock 6.1.0 + 1.4.5.6, ChatBridge, REST API│  │
│   ├──────────────────────────────────────────────────────────┤  │
│   │  TERRARIA AGENT (Deployment)                              │  │
│   │  - .NET 10 + Groq AI                                     │  │
│   │  - Narrador del juego, ciclo día/noche, boss fights      │  │
│   │  - Puerto: 8080 (ClusterIP)                              │  │
│   │  - Auth via X-Agent-Token header                         │  │
│   │  - ✅ Working: health, narrador, comandos /agente        │  │
│   │  - 7 comandos: narrar, hora, clima, tiempo, invocar,     │  │
│   │    consejo, peligro                                      │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: investigation-team                                  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  INVESTIGATIONTEAM API                                   │  │
│   │  - .NET 10 API (2 réplicas) → PostgreSQL 16             │  │
│   │  - Puerto API: 80 | Puerto DB: 5432                     │  │
│   │  - JWT Auth requerido en Agents/Teams controllers        │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: investigation-team-frontend                         │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  ANGULAR FRONTEND (Deployment, 1 replica)                │  │
│   │  - Angular 22 + Nginx                                    │  │
│   │  - Puerto: 80 → Service LoadBalancer: 30081              │  │
│   │  - Login, Dashboard CRUD, Chat con Groq                  │  │
│   ├──────────────────────────────────────────────────────────┤  │
│   │  CHAT BACKEND API (Deployment, 1 replica)                │  │
│   │  - .NET 10 API + PostgreSQL 16                           │  │
│   │  - Puerto: 8000 → Service LoadBalancer: 32445            │  │
│   │  - JWT Auth, Groq AI (llama-3.3-70b), IT Proxy           │  │
│   │  - Agent Memory System (extracción cada 20 msgs)         │  │
│   ├──────────────────────────────────────────────────────────┤  │
│   │  CHAT DB (Deployment, 1 replica)                         │  │
│   │  - PostgreSQL 16                                         │  │
│   │  - Puerto: 5432 (ClusterIP)                              │  │
│   │  - Tablas: Users, Agents, Teams, ChatSessions,           │  │
│   │    ChatMessages, TeamAgents, AgentMemories               │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: supermarket                                         │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  SUPERMARKET STACK                                       │  │
│   │  - Angular 22 Frontend (2 réplicas) → .NET 10 API       │  │
│   │  - .NET 10 API (2 réplicas) → PostgreSQL 16             │  │
│   │  - Puerto Frontend: 80 | Puerto API: 80 | DB: 5432      │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: homepage                                            │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  HOMEPAGE (Deployment, 1 replica)                        │  │
│   │  - Nginx Alpine serving static HTML                      │  │
│   │  - Puerto: 80 → Service LoadBalancer: 30080              │  │
│   │  - Dashboard con links a todos los servicios             │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Stack Tecnológico

| Servicio | Tecnología | Workload | Service Type | Puerto |
|----------|------------|----------|--------------|--------|
| Terraria Server | terraria-tshock (TShock 6.1.0 + 1.4.5.6) | StatefulSet | NodePort | 7777:30777, 7878:30788, 7879:30789 |
| Terraria Agent | .NET 10 + Groq | Deployment (1) | ClusterIP | 8080 |
| InvestigationTeam API | .NET 10 | Deployment (2) | LoadBalancer | 32444 |
| InvestigationTeam DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |
| InvestigationTeam Frontend | Angular 22 + Nginx | Deployment (1) | LoadBalancer | 30081 |
| InvestigationTeam Chat API | .NET 10 + Groq | Deployment (1) | LoadBalancer | 32445 |
| InvestigationTeam Chat DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |
| Supermarket Frontend | Angular 22 + Nginx | Deployment (2) | LoadBalancer | 80 |
| Supermarket API | .NET 10 | Deployment (2) | LoadBalancer | 80 |
| Supermarket DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |
| Homepage | HTML/CSS + Nginx | Deployment (1) | LoadBalancer | 30080 |

## Archivos del Proyecto

```
k8s-projects/
├── terraria-server/
│   ├── namespace.yaml
│   ├── pvc.yaml
│   ├── configmap.yaml
│   ├── statefulset.yaml
│   ├── service.yaml
│   └── README.md
├── terraria-agent/
│   ├── Dockerfile
│   ├── src/
│   └── k8s/
│       ├── namespace.yaml
│       ├── deployment.yaml
│       ├── service.yaml
│       └── secret.yaml
├── investigation-team-api/
│   ├── src/InvestigationTeam.Api/   # Backend .NET 10
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── secret.yaml              # Incluye JWT key
│   │   ├── postgres-pvc.yaml
│   │   ├── postgres-deployment.yaml
│   │   ├── postgres-service.yaml
│   │   ├── deployment.yaml          # Incluye JWT env vars
│   │   └── service.yaml
│   └── Dockerfile
├── investigation-team-chat-backend/
│   ├── src/InvestigationTeam.Chat.Api/  # Backend .NET 10 + Groq
│   │   ├── Controllers/
│   │   │   ├── ChatController.cs        # Memory loading/extraction, JWT forwarding
│   │   │   ├── AgentsController.cs      # Proxy con [Authorize]
│   │   │   ├── TeamsController.cs       # Proxy con [Authorize]
│   │   │   ├── AuthController.cs        # Login/Register/Profile
│   │   │   └── MemoryController.cs      # GET/DELETE memories con ownership check
│   │   ├── Models/
│   │   │   ├── AgentMemory.cs           # Memory model
│   │   │   └── Requests.cs             # [MinLength(6)] en ChangePassword
│   │   └── Services/
│   │       ├── GeminiService.cs         # Llama a Groq API (nombre legacy)
│   │       ├── InvestigationTeamProxy.cs # JWT forwarding, optional bearer token
│   │       └── IInvestigationTeamProxy.cs
│   ├── k8s/
│   │   ├── namespace.yaml
│   │   ├── secret.yaml
│   │   ├── postgres-pvc.yaml
│   │   ├── postgres-deployment.yaml
│   │   ├── postgres-service.yaml
│   │   ├── api-deployment.yaml
│   │   └── api-service.yaml
│   └── Dockerfile
├── investigation-team-frontend/
│   ├── src/app/components/
│   │   ├── login/login.component.ts         # signal() for change detection
│   │   ├── register/register.component.ts   # Signal fix + "Groq API Key"
│   │   ├── profile/profile.component.ts     # "Groq API Key" label
│   │   ├── chat/chat.component.ts           # Improved error messages
│   │   └── dashboard/
│   │       ├── agents-list.component.ts     # Error alerts on save/delete
│   │       └── teams-list.component.ts      # Agent checkboxes on create
│   ├── k8s/
│   ├── Dockerfile
│   └── nginx.conf                          # Health proxy, regex asset caching
├── supermarket-api/
├── supermarket-frontend/
├── homepage/
├── CONTEXT.md
└── .gitignore
```

## Pendiente
- [x] Verificar cluster Kubernetes disponible
- [x] Desplegar Homepage (puerto 30080)
- [x] Desplegar InvestigationTeam API (puerto 32444)
- [x] Desplegar InvestigationTeam Chat Backend (puerto 32445)
- [x] Desplegar InvestigationTeam Frontend (puerto 30081)
- [x] **ARREGLAR Terraria Server** — World auto-creation + PVC persistence + scale 0↔1
- [x] **Terraria Agent** — App .NET + Groq, narrador del juego, comandos `/agente`
- [x] **TShock 6.1.0 for 1.4.5.6** — Custom Docker image + REST API funcional
- [x] **ChatBridge Plugin** — In-game chat → Agent → Groq narration + TShock commands
- [x] **Hamachi Connection** — Port forwarding configurado para jugar con amigos
- [x] **🔥 CONEXIÓN TERRARIA REMOTA ARREGLADA** — Cliente oficial 1.4.5.6 se conecta al servidor remoto
- [x] **REST API Permissions Fixed** — Permisos `tshock.rest.*` en grupo admin, broadcast + rawcmd funcionan
- [x] **Persistir config TShock** — ConfigMap con config.json + bootstrap.sh copia desde template
- [x] **Rebuild agente con llama-3.1-8b-instant** — Modelo más rápido, menos rate limiting
- [ ] **Eventos automáticos Agent** — Ciclo día/noche, boss fights, amanecer con Groq
- [ ] Verificar funcionamiento de todos los servicios

## Dónde nos quedamos (Sesión 11 - 2026-07-23 noche)

### ✅ Crafting DB + Wiki Fallback funcionando

#### Lo que se hizo en esta sesión
1. **Modelo actualizado a llama-3.3-70b-versatile**: Mejor conocimiento del juego, más preciso con recetas
2. **Crafting DB con 182 items vanilla**: materials, bosses, weapons, armor, accessories, potions, stations, NPCs — todo de Terraria vanilla, sin mods
3. **WikiService integrado como fallback**: Cuando un item no está en la DB local, busca en Terraria Wiki API (`terraria.wiki.gg`), parsea el HTML renderizado, extrae ingredientes y estación de craft
4. **Cache persistente**: Recetas consultadas se guardan en `wiki-cache.json` para no repetir llamadas API
5. **Filtro de mensajes pre-Groq**: `ShouldRespond()` filtra ok/si/jaja/hola/mensajes <3 chars ANTES de llamar a Groq (ahorra tokens, evita spam)
6. **Comandos de juego routing**: time, worldevent, rain, bloodmoon, eclipse, wind, butcher, npc — todos van por ChatBridge plugin
7. **Spawnboss directo**: ChatBridge usa `NPC.NewNPC()` directamente (no REST API)
8. **SCP bug diagnosticado**: `scp -r` no sobrescribía archivos existentes en el servidor remoto — fix: `rm -rf` antes de copiar

#### Estado actual del sistema
- **Terraria Server**: Running, mundo `Aló_telé_delsía` (2+ jugadores conectados)
- **Terraria Agent**: Running, modelo `llama-3.3-70b-versatile` + 182 items DB + Wiki fallback
- **Crafting**: DB local → Wiki fallback → cache persistente
- **Filtro**: ok/si/jaja/hola/short messages → ignorados sin Groq call
- **Comandos**: /agente narrar|hora|clima|tiempo|invocar|consejo|peligro + natural language
- **REST API**: Funcional con permisos `tshock.rest.*` en grupo admin

#### Próximos pasos
1. **Eventos automáticos** — Ciclo día/noche, boss fights, amanecer con narración Groq
2. **Más items en DB** — Añadir items comunes que faltan (vela, Cell Phone, etc. ya cubiertos por Wiki)
3. **Mejorar prompt** — Instrucciones más precisas para crafting, incluir contexto del servidor actual
4. **Commit** — Todo el código nuevo a git

### ✅ Conexión Terraria ARREGLADA + REST API Permissions Fixed

#### Lo que se hizo en esta sesión
1. **Cliente actualizado a oficial**: Usuario compró Terraria 4-pack en Steam (30€). Cliente oficial 1.4.5.6 se conecta correctamente al servidor remoto `5.189.163.39:30777`.
2. **TShock actualizado a 6.1.0**: Docker image rebuild con TShock 6.1.0 (OTAPI 3.3.11) para Terraria 1.4.5.6.
   - Descarga: `https://github.com/Pryaxis/TShock/releases/download/v6.1.0/TShock-6.1.0-for-Terraria-1.4.5.6-linux-x64-Release.zip`
   - **IMPORTANTE**: El zip contiene un `.tar` dentro — hay que `unzip` y luego `tar xf *.tar`
   - Build flags: `--provenance=false --sbom=false` para evitar issues con k3s containerd
3. **REST API Permissions arreglado**: El problema principal de esta sesión fue que la REST API devolvía 403 para `broadcast` y `rawcmd`.
   - **Causa raíz**: La REST API usa permisos `tshock.rest.*`, NO `tshock.admin.*`
   - **Fix**: Agregados permisos `tshock.rest.useapi`, `tshock.rest.broadcast`, `tshock.rest.command`, etc. al grupo `admin` en la DB SQLite
   - El usuario `agent` cambió de grupo `owner` → `admin` en la tabla Users
   - `superadmin` es grupo **reservado** en TShock — las entradas en DB son ignoradas

#### Estado actual de la DB (después del fix)
```sql
-- Grupo admin con permisos REST
GroupList row: admin → hereda de newadmin + tshock.rest.useapi,tshock.rest.broadcast,tshock.rest.command,...

-- Usuario agent en grupo admin
Users row: 1|agent|<bcrypt_hash>||admin|2026-07-22|2026-07-22|
```

#### Endpoints REST API funcionando
- `GET /v2/server/status` → 200 OK (sin auth) ✅
- `GET /v2/players/list?token=...` → 200 OK ✅
- `POST /v2/server/broadcast?msg=...&token=...` → 200 OK ✅ (**importante**: param es `msg` como query string, NO body JSON)
- `GET /v3/server/rawcmd?cmd=/playing&token=...` → 200 OK ✅

#### Problema conocido: Config persistence
- `/config` es `emptyDir` — TShock regenera `config.json` en cada restart
- La config dice `UserGroupName: "admin"` (default cuando TShock la genera)
- Los permisos REST en la DB SQLite sobreviven porque están en PVC
- **Truco**: Mientras los permisos estén en la DB del grupo `admin`, la API funciona aunque la config se regenere
- **Pendiente**: Persistir la config de alguna forma (ConfigMap, PVC, o copiar desde world PVC en startup)

#### Puerto de juego
- **NodePort 30777** → Container 7777 (juego)
- **NodePort 30788** → Container 7878 (REST API)
- **NodePort 30789** → Container 7879 (ChatBridge plugin)
- Conexión del cliente: `5.189.163.39:30777` (sin contraseña)

### Próximos pasos (Sesión 10)
1. **Persistir config TShock** — Crear ConfigMap o usar init container para copiar config desde PVC
2. **Commit a git** — Todo el infrastructure as code
3. **Rebuild agente** — Con modelo `llama-3.1-8b-instant` + arreglar llamadas REST (broadcast usa `msg` query param)
4. **Probar conexión completa** — Cliente → Server → Agent → Groq → TShock commands

### Descubrimientos de Sesión 8 (NUEVO)

#### 1. hostNetwork NO puede coexistir con SVCLB LoadBalancer
- SVCLB crea pods con `hostPort: 7777/7878/7879` en el DaemonSet
- StatefulSet con `hostNetwork: true` intenta bindear los mismos puertos → **FailedScheduling**
- **Solución**: StatefulSet SIN hostNetwork (como funciona en local)

#### 2. Error OTAPI solo aparece EN k3s, NO fuera de k3s
- **En k3s** (con SVCLB/kube-proxy): `System.IndexOutOfRangeException: Invalid packet. Message size too small (1)` en `mfwh_CheckBytes`
- **Fuera de k3s** (directamente en el host): El error NO aparece, pero el handshake igual no completa
- **Conclusión**: El error de OTAPI en k3s es causado por la cadena SVCLB/kube-proxy, no por OTAPI per se

#### 3. Proceso zombie root (PID 4751) — NO SE PUEDE MATAR SIN SUDO
- El antiguo TShock que corría con hostNetwork en k3s quedó como zombie root
- Consume ~11GB RAM y ~45% CPU
- Tiene su propio socket en puerto 7777 (inodio diferente)
- `sudo kill -9 4751` requiere contraseña — **pendiente que el usuario ejecute esto**
- **Mientras tanto**: usar puerto 7778 (el zombie no lo tiene)

#### 4. Conexiones fuera de k3s — mismo problema
- TShock ejecutado directamente en el host (sin k3s): `./TShock.Server` con DOTNET_ROOT
- Puerto 7778: El servidor SÍ registra "31.211.185.251:XXXXX is connecting..."
- Pero el handshake igual no completa — cliente queda en "Connecting..."
- Sin plugin ChatBridge: mismo resultado
- Con `-autocreate 3`: mismo resultado

#### 5. Respuesta del servidor a conexiones externas
- Python test conectando a 7778: Recibe datos del servidor (type 82, NetLiquidModule) **ANTES** de enviar ConnectRequest
- Después de enviar ConnectRequest: recibe type 0 (¿mensaje corrupto?)
- Esto sugiere que el servidor envía datos no solicitados a conexiones nuevas

### Diagnóstico consolidado

| Componente | En LAN (local) | En WAN (internet) |
|---|---|---|
| K3s + hostNetwork | ✅ Funciona | ❌ OTAPI error + handshake falla |
| K3s sin hostNetwork (SVCLB) | ✅ Funciona | ❌ Conexión no aparece en logs |
| Directo en host (fuera de k3s) | (sin probar) | ❌ Conexión SÍ aparece pero handshake falla |

**La única diferencia es LAN vs WAN**. El mismo binario, mismo mundo, mismo config funciona en LAN pero no en internet.

### Teorías pendientes de validar

1. **TCP fragmentation/timing**: En LAN los paquetes llegan completos. En WAN llegan fragmentados. El hook `mfwh_CheckBytes` de OTAPI podría leer el buffer antes de que lleguen todos los bytes.
2. **Contabo firewall/security group**: Algo a nivel de VPS podría estar manipulando paquetes Terraria específicamente
3. **IPv6 dual-stack**: El VPS tiene IPv6 (2a02:c207:2320:5971::1). Podría haber interferencia
4. **MTU issue**: Path MTU diferente en WAN vs LAN causando fragmentación

### Próximos pasos (Sesión 9)
1. **⚠️ URGENTE**: El usuario debe ejecutar `sudo kill -9 4751` para liberar RAM y puerto 7777
2. **Obtener tcpdump**: Necesario para ver qué paquetes llegan/salen. Alternativas sin sudo: `ss -ti` para ver estadísticas TCP, o instalar tcpdump en el container del server
3. **Probar con Contabo security group**: Verificar si hay reglas de firewall a nivel de VPS que bloquean/modifican tráfico de juego
4. **Probar sin OTAPI**: ¿Se puede correr el server vanilla de Terraria 1.4.5.5 (sin TShock) en el host? Necesitaríamos el binario vanilla
5. **Probar con MTU reducido**: `ip link set eth0 mtu 1400` (requiere sudo)
6. **Investigar si OTAPI 3.3.10 tiene bug conocido** con conexiones WAN/fragmentación
7. **Alternativa**: Actualizar cliente a 1.4.5.6 y usar TShock 6.1 (OTAPI 3.3.11) — el usuario dijo que NO por ahora

### Archivos clave para debugging
```
terraria-server/docker/Dockerfile          # Custom image TShock 6.1.0 + 1.4.5.6
terraria-server/docker/bootstrap.sh        # Config TShock + user creation + REST API
terraria-server/statefulset.yaml           # K8s manifest con NodePort
/logs dentro del pod: /tshock/logs/        # Server logs (también en stdout)
/config/config.json                        # TShock config (emptyDir, se regen)
/root/.local/share/Terraria/Worlds/        # PVC: mundos + tshock.sqlite
```

## Historial de Sesiones

### Sesión 2 (2026-07-18): Testing Round 4 + Context Save

#### Test Results (28 tests total)
**Fase 1: Infrastructure Resilience** ✅ (6 tests)
- Kill chat API pod → auto-restart, data persists ✅
- Kill Postgres pod → auto-restart, data persists ✅
- App works after DB restart ✅

**Fase 2: Backend Edge Cases** ✅ (8 tests)
- Malformed JWT (garbage/empty/none-alg/wrong-issuer) → all 401 ✅
- Empty message body → 400 with validation ✅
- 100KB message → 400 (MaxLength 10000) ✅
- Delete session twice → no crash ✅
- 47 memories → Groq prompt too large (design limit) ⚠️

**Fase 3: Data Integrity** ✅ (7 tests)
- User2 can't access/delete User1 sessions → 404 ✅
- Add same agent twice → only 1 stored ✅
- Remove nonexistent agent → 200 (no-op, should be 404) ⚠️
- Orphaned team sessions after team delete → known behavior ⚠️

**Fase 4: Frontend / Nginx** ✅ (7 tests)
- HTML5 routing → serves index.html ✅
- CORS preflight → 204 ✅
- Malformed JSON → 400 with parse error ✅
- 20 concurrent logins → all 200 ✅

**Minor Issues Found** (low severity):
1. Health check returns empty body (no DB verification)
2. Remove nonexistent agent returns 200 instead of 404
3. Orphaned team sessions (dangling TeamId) after team delete
4. 47 memories causes empty Groq response (context window limit)

#### Commits in this session
```
56d2ec1  fix: stale Gemini refs, health endpoint, error handling, teams UX
b47e9dd  fix: security hardening — auth, validation, error sanitization
1f6e282  feat: agent memory system + Groq provider + frontend fixes
```

---

### Sesión 3 (2026-07-18): Testing Round 4 + Terraria Fix + Agent Brainstorm

#### Testing Round 4 (28 tests)
Ver Sesión 2 arriba para detalles.

#### Terraria Server Fix
- **Causa raíz**: `bootstrap.sh` necesita `WORLD_FILENAME` env var + args `-autocreate`
- **Fix**: Agregado `WORLD_FILENAME=MundoSobrinos.wld`, args `-autocreate 2 -worldname MundoSobrinos`
- **Mount path**: Cambiado a `/root/.local/share/Terraria/Worlds` (antes el mundo no persistía en PVC)
- **Probe delays**: Aumentados a 300s (generación de mundo toma ~5 min)
- **Resultado**: 1/1 Ready, 0 restarts, mundo persiste en PVC, scale 0↔1 funciona
- **Commit**: `3b8a43c`

#### Terraria Agent Brainstorm (INCOMPLETO — continuar aquí)
**Decisions taken:**
- **Conexión**: Consola via `kubectl exec` (no REST API)
- **Comportamiento**: Híbrido (automático + reactivo a comandos)
- **Eventos automáticos**: Ciclo día/noche, boss fights, mensajes de amanecer con Groq
- **Estilo**: Narrador del juego
- **Tecnología**: App .NET + Groq
- **Control por jugadores**: Sí, via `/agente [comando]`
- **Prefijo**: `/agente [comando]`
- **Enfoque**: App .NET como Deployment (Enfoque 2)

**Próximo paso**: Presentar diseño detallado de la app .NET (arquitectura, componentes, flujo de datos)

**Comandos TShock disponibles para el agente:**
- Tiempo: `time day/night/noon/dusk/midnight`
- Clima: `rain [strength]`, `bloodmoon`, `eclipse`, `fullmoon`, `sandstorm`, `wind [speed]`
- Mundo: `meteor`, `hardmode`, `invade` (goblins/pirates/snow)
- NPCs: `spawnboss [type]`, `spawnmob [type] [amount]`, `butcher`
- Mensajes: `say [mensaje]` (broadcast a todos)
- Jugadores: `kick`, `ban`, `tp`, `heal`, `kill`, `give [item]`

**Comandos de jugadores para `/agente`:**
- Pendiente de definir qué comandos exactos soportará

---

### Sesión 4 (2026-07-18): Terraria Agent Deploy + Tasks 7-10

#### Task 7: K8s Manifests
- Namespace `terraria` shared with server
- Deployment: .NET 10, 1 replica, health probes on `/health` (port 8080)
- Service: ClusterIP on port 8080
- Secret: `terraria-agent-secret` with GROQ_API_KEY and TERRARIA_SERVER_URL

#### Task 8: Dockerfile
- Multi-stage build: .NET 10 SDK → runtime
- Final image: `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`
- Port 8080 exposed

#### Task 9: Config Updates
- Updated CONTEXT.md architecture, stack, project files

#### Task 10: Deploy and Test
- Imported Docker image to k3s
- Applied K8s manifests
- Verified pod `terraria-agent-xxxxx` is Running (1/1 Ready)
- Health endpoint test: `200 OK` via port-forward (no curl in TShock container)
- Cross-pod connectivity: Service ClusterIP accessible within namespace

**Commits:**
```
Task 7: K8s manifests
Task 8: Dockerfile
Task 9: Config updates
Task 10: Deploy + test
```

---

### Sesión 1 (2026-07-17): Full Stack Build + Deploy

#### Lo que se hizo
1. **Login/Register stuck button fix** — `signal()` for `loading`/`error` in both components
2. **Nginx proxy routing** — `~ ^/api/(agents|teams)` → IT API, `/api/` → chat API
3. **Chat error messages** — Frontend shows backend error messages; sanitized Groq errors
4. **AI provider migration to Groq** — Google Gemini requires billing. Groq is free.
   - Groq endpoint: `https://api.groq.com/openai/v1/chat/completions`
   - Model: `llama-3.3-70b-versatile`
   - Interface name `IGeminiService` kept (legacy)
5. **Persistent agent memory system**:
   - `AgentMemory` model, DbContext, memory loading into system prompt
   - Extraction every 20 messages (best-effort, async, 5s delay to avoid rate limits)
   - GET/DELETE endpoints with ownership checks
   - `AgentMemories` table created via manual SQL (`CREATE TABLE` + index on AgentId)
6. **Security hardening**:
   - IT API: JWT `[Authorize]` on Agents/Teams controllers
   - Chat API: Memory endpoints require session ownership (`Forbid()`)
   - Chat API: Validate agent/team exists before creating session (returns 400)
   - Chat API: Reject session with both agentId and teamId
   - Groq error messages sanitized (no rate limit details, org_id leaked)
   - Backend `ChangePasswordRequest` has `[MinLength(6)]` on NewPassword
7. **Frontend fixes**:
   - Register/Profile forms: "Gemini API Key" → "Groq API Key", link to `console.groq.com`
   - Teams create form: agent checkboxes shown on create (was only on edit)
   - Error callbacks show alerts instead of silent `() => {}`
8. **Nginx**: Health endpoint location added; static assets cached 1 year via regex location
9. **IT API JWT auth**: `Microsoft.AspNetCore.Authentication.JwtBearer` added, K8s secret updated
10. **Chat proxy forwards JWT**: `IInvestigationTeamProxy` methods accept optional `bearerToken` param

#### Commits (Sesión 1)
```
b47e9dd  fix: security hardening — auth, validation, error sanitization
1f6e282  feat: agent memory system + Groq provider + frontend fixes
```

---

### Sesión 5 (2026-07-19): TShock 6.0.0 for 1.4.5.5 + REST API

#### Problema
- Cliente pirata del usuario era Terraria v1.4.5.5
- Servidor existente usaba v1.4.5.6 (imagen ryshe/terraria:latest)
- Error: "No tienes la misma versión que este servidor"
- No existe imagen Docker `ryshe/terraria` para TShock 6 + 1.4.5.5

#### Solución
- Custom Docker image: `terraria-tshock-1455:latest`
- TShock 6.0.0 (GitHub releases) + .NET 9 runtime
- Bootstrap script con config.json preconfigurado

#### Configuración TShock
- `RestApiEnabled: true` (puerto 7878)
- `DisableLoginBeforeJoin: true` (sin login obligatorio)
- `ApplicationRestTokens` con token para usuario `agent` (grupo `owner`)
- SQLite DB en PVC (`/root/.local/share/Terraria/Worlds/tshock.sqlite`)
- User `agent` creado con permisos owner

#### Problemas encontrados y resueltos
1. **Liveness probe matando pod durante world generation** → `initialDelaySeconds: 1800` (30 min)
2. **`-autocreate` flag ignorado** → TShock 6 OTAPI crea Large world sin importar el valor
3. **REST API devolvía 403** → `RestApiEnabled: false` en config por defecto
4. **Token no reconocido** → `ApplicationRestTokens` key debe ser el token real
5. **User "agent" no existía** → Creado vía SQLite con BCrypt hash
6. **SQLite DB en emptyDir se borraba** → Movido a PVC de worlds
7. **Agent auth usaba Bearer** → Cambiado a `X-Agent-Token` header

#### Endpoints probados
- `POST /v2/server/broadcast` → 200 OK ✅
- `GET /v3/server/rawcmd` → 200 OK ✅
- `GET /v2/players/list` → 200 OK ✅
- `POST /api/chat` (Agent) → 200 OK + Groq narration ✅

#### Commits
```
adb2c10  feat: TShock 6.0.0 for Terraria 1.4.5.5 + REST API
```

---

### Sesión 6 (2026-07-19): ChatBridge + Groq Optimization + Hamachi

#### ChatBridge Plugin - End to End ✅
- **Plugin**: `ChatBridgePlugin.cs` — forwards in-game chat to agent via POST
- **Agent**: IntentParser via Groq → narrates + executes TShock commands
- **Puerto plugin**: 7879 (HttpListener) para comandos del agente
- **Comandos bridge**: `bridge rain on/off/heavy`, `bridge wind <speed>`, `bridge bloodmoon on/off`, `bridge eclipse on/off`
- **Conversation memory**: Last 8 messages per player stored in ConcurrentDictionary

#### Bugs Found & Fixed
1. `ServerChatEventArgs.Player` → `PlayerHooks.PlayerChat` (TShock 6 API change)
2. Missing `X-Agent-Token` header on POST to agent
3. Wrong TShock 6 commands (`/rain` → `/worldevent rain slime`, `/spawnboss` needs in-game execution)
4. Bridge prefix strip: `lower[8..]` → `lower[7..]` (was cutting first char of command)
5. Double `[Agent]` prefix in broadcast
6. Conversation memory context loss (was recreating list each time)

#### Groq Rate Limiting
- **Problem**: Free tier rate limits (~30 RPM) hit hard during testing (51 test cases)
- **Solution**: Changed model from `llama-3.3-70b-versatile` to `llama-3.1-8b-instant` (faster, more quota)
- **System prompt**: Updated with confirmation-before-action rules for ambiguous commands
- **Retry logic**: Added 12s retry on 429 TooManyRequests

#### Test Suite Created
- **File**: `/tmp/test_chatbridge.sh` — 51 tests across 5 groups
- **Groups**: Direct commands, casual chat, ambiguous words, indirect commands, edge cases
- **Status**: 46/51 pass when Groq responds (rate limiting blocks rapid testing)

#### Hamachi Connection
- **Setup**: PC Amigo → Hamachi → User's Windows → Port Forward → Linux Server
- **Port forwarding**: `netsh interface portproxy` rules saved in CONTEXT.md
- **Status**: Working when server runs on Windows; Linux server currently scaled to 0

#### Current State
- **Servidor Terraria Linux**: Escalado a 0 (jugando en Windows)
- **Agente**: Running pero sin servidor que controlar
- **Groq**: Modelo cambiado a `llama-3.1-8b-instant` (pendiente rebuild)
- **IntentParser**: System prompt actualizado con reglas de confirmación

#### Pendiente
- [ ] Rebuild agente con modelo `llama-3.1-8b-instant` y prompt reducido
- [ ] Correr test suite completo (con delays más largos para Groq)
- [ ] Levantar servidor Linux y probar conexión Hamachi→Linux

---

---

---

### Sesión 7 (2026-07-21): Migración a Servidor Externo + Conexión Terraria Rota

#### Lo que se hizo
1. **Servidor externo configurado**: K3s instalado en `5.189.163.39` (Contabo VPS)
2. **Todas las imágenes Docker transferidas** y cargadas en k3s
3. **Todos los servicios desplegados**: terraria, agent, investigation-team, supermarket, homepage
4. **nginx ingress instalado**: Funcionando en NodePort 30808/30181
5. **Terraria server desplegado**: `hostNetwork: true`, mundo `MundoSobrinos2` cargado
6. **Health probe fix**: Cambiado de puerto 7777 a 7878 (REST API) para evitar world-save spam
7. **CPU reducido**: De 57%+ a ~35% después del fix de probes

#### Problema CRÍTICO: Conexión Remota Rota
- **Error**: `System.IndexOutOfRangeException: Invalid packet. Message size too small (0)`
- **Ubicación**: `Terraria.NetMessage.mfwh_CheckBytes` (OTAPI hook)
- **Diagnóstico**:
  - No es versión (cliente = servidor = 1.4.5.5)
  - No es firewall (iptables INPUT ACCEPT, sin NetworkPolicies)
  - Las conexiones del usuario SÍ llegan al servidor
  - El servidor ACKea pero no procesa el handshake
- **Investigación pendiente**: tcpdump con conexión real, probar fuera de k3s

#### Commits
```
Pendiente de commit
```

---

### Sesión 9 (2026-07-23): Conexión Arreglada + REST API Permissions Fixed

#### Lo que se hizo
1. **Cliente oficial comprado**: 4-pack Terraria en Steam (30€). Cliente oficial 1.4.5.6.
2. **TShock 6.1.0 + 1.4.5.6**: Rebuild de Docker image con nueva versión.
   - Zip contiene `.tar` interno: `unzip` → `tar xf *.tar`
   - Download: `https://github.com/Pryaxis/TShock/releases/download/v6.1.0/TShock-6.1.0-for-Terraria-1.4.5.6-linux-x64-Release.zip`
3. **Conexión remota funciona**: `5.189.163.39:30777` → Cliente se conecta, juega, mundo `MundoSobrinos2`.
4. **REST API Permissions FIXED** (descubrimiento principal):
   - **Problema**: `v2/server/broadcast` y `v3/server/rawcmd` devolvían 403 "Not authorized"
   - **Diagnóstico**: La REST API usa permisos `tshock.rest.*`, NO los permisos `tshock.admin.*` del grupo
   - **Fix**: Agregar permisos REST al grupo `admin` en SQLite DB:
     - `tshock.rest.useapi` (requerido para usar cualquier endpoint REST)
     - `tshock.rest.broadcast` (para broadcast)
     - `tshock.rest.command` (para rawcmd — NO es `tshock.rest.rawcmd`)
     - `tshock.rest.ban`, `.kick`, `.kill`, `.mute`, `.slap`, `.whisper`, `.warp`, `.tp`, `.time`, `.world`, `.npc`, `.group`, `.userinfo`, `.config`
   - Usuario `agent` cambiado de grupo `owner` → `admin` en tabla Users
   - `superadmin` es grupo RESERVADO de TShock — las entradas custom en DB son ignoradas
5. **Broadcast parameter**: Endpoint usa `msg` como **query parameter** (`?msg=...`), NO body JSON
6. **Config persistence identificada como pendiente**: `/config` es `emptyDir`, se regenera. Los permisos en DB sí persisten (PVC).

#### Descubrimientos técnicos clave
- TShock cachea grupos en memoria al startup — cambios en DB requieren restart del pod
- `superadmin` con `*` en DB no funciona porque TShock lo reserva y override
- El endpoint `v2/rawcmd` no existe — solo `v3/server/rawcmd`
- `v2/server/status` y `v2/players/list` no requieren permisos REST especiales
- `v2/server/broadcast` requiere `tshock.rest.broadcast`
- `v3/server/rawcmd` requiere `tshock.rest.command`
- `v3/server/rawcmd` ejecuta comandos como el usuario REST, verificando permisos de comando individuales
- El config `CONFIGPATH` apunta a `/config` (emptyDir) — TShock genera config.json allí con defaults
- StatefulSet volume `terraria-config` es `emptyDir`, `terraria-world` es PVC

#### Puerto NodePort
- Juego: 30777 → 7777
- REST API: 30788 → 7878
- ChatBridge plugin: 30789 → 7879

#### Commits
```
Pendiente de commit (REST API fix + TShock 6.1.0 upgrade)
```

---

### Sesión 10 (2026-07-23): Config Persistence + Agent Rebuild

#### Lo que se hizo
1. **Config TShock persistente**: Creado ConfigMap `terraria-config-json` con config.json completo
   - `UserGroupName: "admin"` (no "owner") para permisos REST
   - Bootstrap.sh modificado: copia desde `/config-template/config.json` si no existe
   - StatefulSet actualizado con volume mount adicional para ConfigMap
2. **Agente reconstruido**: Docker image rebuild con modelo `llama-3.1-8b-instant`
   - Variable de entorno `Groq__Model` agregada al deployment
   - Modelo más rápido, menos rate limiting en tier gratuito
3. **Agent user group fix**: Bootstrap.sh ahora crea usuario `agent` con grupo `admin` (no `owner`)
4. **Commits**:
   ```
   bf206ca  feat: persist TShock config + rebuild agent with llama-3.1-8b-instant
   ```

#### Estado actual
- **Terraria Server**: Running (1/1 Ready), config persistente en ConfigMap
- **Terraria Agent**: Running (1/1 Ready), modelo llama-3.1-8b-instant
- **REST API**: Funcional con permisos `tshock.rest.*` en grupo admin
- **Config**: `/config/config.json` viene de ConfigMap, se copia al iniciar si no existe

#### Pendiente
- [ ] **Eventos automáticos Agent** — Ciclo día/noche, boss fights, amanecer con Groq
- [ ] Verificar funcionamiento de todos los servicios

## Problemas Conocidos

### ✅ Terraria Server - Conexión Remota (RESUELTO)
- **Antes**: Cliente 1.4.5.5 pirata no podía conectarse al servidor remoto
- **Solución**: Usuario compró Terraria oficial (1.4.5.6) + TShock 6.1.0 (OTAPI 3.3.11)
- **Estado**: Funcionando en `5.189.163.39:30777`

### ⚠️ Config TShock no persiste
- `/config` es `emptyDir` — se regenera en cada pod restart
- Los permisos REST en la DB SQLite sí sobreviven (están en PVC)
- **Pendiente**: Persistir config vía ConfigMap o init container

### Groq Rate Limiting ⚠️
- **Problema**: Tier gratuito tiene ~30 RPM, el system prompt consume ~800 tokens/petición
- **Solución aplicada**: Cambiado a `llama-3.1-8b-instant` (más rápido, más quota)
- **Pendiente**: Rebuild del agente con el nuevo modelo

### InvestigationTeam - Minor Issues
1. `agentIds` not persisted in Team creation via POST (teams use `AddAgentToTeam` endpoint)
2. Health check returns empty body (no DB verification)
3. Remove nonexistent agent returns 200 instead of 404
4. Orphaned team sessions after team delete (dangling TeamId)
5. 47+ memories causes empty Groq response (context window limit)

## Credenciales y Config

| Credencial | Valor |
|-----------|-------|
| test@test.com password | `123456` |
| user2@test.com password | `123456` |
| DB name (chat) | `investigation_team_chat` |
| DB name (IT) | `investigation_team` |
| JWT key (compartido) | `super-secret-key-change-in-production-1234567890123456` |
| Groq API key | Ver K8s secret `terraria-agent-secret` |
| Groq model | `llama-3.1-8b-instant` (cambiado de `llama-3.3-70b-versatile`) |
| Memory extraction threshold | Cada 20 mensajes |
| Antonio agent ID | `ac8ca2c7-ae4c-43e0-bb8e-876f03480713` |
| TShock REST token | `terraria-agent-secret-token-2024` |
| TShock user | `agent` / password `agent1234` / grupo `admin` |
| Agent auth | `X-Agent-Token: terraria-agent-secret-token-2024` |
| Connect (Linux local) | `172.30.138.92:7777` (sin contraseña) |
| Connect (Servidor Externo) | `5.189.163.39:30777` (sin contraseña) ✅ FUNCIONA |
| Connect (Hamachi) | `25.35.4.105:7777` (cuando servidor está en Linux) |

## Notas del Usuario
- Quiere algo usable a futuro
- Tiene experiencia básica con Docker/Kubernetes (1 año)
- Busca practicar cosas reales para su día a día
- Tiene servidor de Terraria que levanta de vez en cuando para jugar con sobrinos
- Skills: .NET, Angular, PostgreSQL
- Idioma: Español (comunicación y documentación)
- Quiere que se guarde todo el contexto entre sesiones

## Ingress Routing (gaming.andalusiaone.com:30808)

| Ruta | Servicio | Namespace |
|------|----------|-----------|
| `/` | homepage | homepage |
| `/it` | investigation-team-frontend-svc | investigation-team-frontend |
| `/api/*` | investigation-team-api | investigation-team |
| `/chat-api/*` | investigation-team-chat-api-svc | investigation-team-frontend |
| `/supermarket` | supermarket-frontend | supermarket |
| `/supermarket-api/*` | supermarket-api | supermarket |
| `/terraria-agent` | terraria-agent | terraria |
| Puerto 7777 (TCP) | terraria-server | terraria (NodePort 30777) |
| Puerto 7878 (TCP) | terraria-server REST API | terraria (NodePort 30788) |

## Conexión Hamachi - Terraria (Linux/Windows)

### Setup
- **Servidor Linux**: k3s en `172.30.138.92`
- **PC del usuario**: Windows con Hamachi, IP Hamachi: `25.35.4.105`
- **Amigo**: Windows con Hamachi
- **Red Hamachi**: El usuario crea la red, el amigo se une

### Reglas de port forwarding (en Windows del usuario, PowerShell como Admin)

**Activar** (cuando el servidor corre en Linux):
```cmd
netsh interface portproxy add v4tov4 listenport=7777 listenaddress=25.35.4.105 connectport=7777 connectaddress=172.30.138.92
netsh advfirewall firewall add rule name="Terraria 7777" dir=in action=allow protocol=TCP localport=7777
```

**Desactivar** (cuando el servidor corre en Windows directamente):
```cmd
netsh interface portproxy delete v4tov4 listenport=7777 listenaddress=25.35.4.105
```

**Verificar**:
```cmd
netsh interface portproxy show all
```

### Conexión del amigo
- IP: `25.35.4.105`
- Puerto: `7777`

## Perfil de Trabajo - Opencode (IA)

### Comandos Disponibles (Servidor Externo)
| Comando | Descripción |
|---------|-------------|
| `ssh roman@srv01.gaming.andalusiaone.com` | Conectar por SSH |
| `kubectl get pods --all-namespaces` | Ver estado de pods |
| `kubectl logs -n <ns> <pod> --tail=N` | Ver logs de un pod |
| `kubectl describe pod -n <ns> <pod>` | Diagnosticar problemas de pod |
| `kubectl rollout restart deployment/<name> -n <ns>` | Redeploy sin rebuild |

### Comandos Disponibles (Local)
| Comando | Descripción |
|---------|-------------|
| `sudo k3s kubectl get pods --all-namespaces` | Ver estado de pods |
| `sudo k3s kubectl logs -n <ns> <pod> --tail=N` | Ver logs de un pod |
| `sudo k3s kubectl describe pod -n <ns> <pod>` | Diagnosticar problemas de pod |
| `sudo k3s kubectl rollout restart deployment/<name> -n <ns>` | Redeploy sin rebuild |

### Flujo de Debugging (Resiliente)
1. **Verificar el error exacto**: logs del pod, describe pod, curl directo
2. **Probar por capas**: container → pod → service → nginx proxy → browser
3. **Confirmar antes de asumir**: no decir "funciona" sin probar con curl o kubectl
4. **Hard refresh después de rebuild**: recordar al usuario hacer Ctrl+F5
5. **DB nueva = sin datos**: después de EnsureCreated, recordar que hay que registrar usuarios

### Errores Comunes y Soluciones
| Error | Causa | Solución |
|-------|-------|----------|
| `502 Bad Gateway` | Health check falla o servicio no listo | Verificar readiness probe, agregar `/health` endpoint |
| `500 Internal Server Error` | Tabla no existe en DB | `EnsureCreated()` o migraciones EF |
| `401 Unauthorized` en login | No hay usuarios | Registrar usuario primero |
| HTML roto en navegador | `<a>` anidado dentro de `<a>` | Usar `<div>` con `onclick` o `event.stopPropagation()` |
| Pod `0/1 Ready` | Readiness probe retorna error | Verificar que el health check no requiera auth |
| Pod `Running` pero 0 Ready | Liveness/Readiness probe falla | Verificar que el server esté escuchando en el puerto correcto |

### Reglas de Construcción
1. **Siempre probar el backend por curl** antes de declarar que funciona
2. **Rebuild con `--no-cache`** para evitar imágenes stale
3. **Siempre apply + rollout restart** después de cambiar deployment yaml
4. **Verificar pods Ready** después de cada cambio
5. **Documentar cada fix** en progreso o CONTEXT.md

### Stack del Proyecto (Servidor Externo)
- **Runtime**: k3s v1.36.2 (bare metal)
- **Registry**: Docker local → k3s ctr images import
- **Ingress**: nginx ingress controller (puerto 30808)
- **Frontend**: Angular 22 + Nginx (proxy reverso)
- **Backend**: .NET 10 + Entity Framework Core
- **DB**: PostgreSQL 16
- **AI**: Groq API (llama-3.1-8b-instant) — antes era Gemini (requiere billing)
- **Auth**: JWT + BCrypt
- **Dominio**: gaming.andalusiaone.com

## Sesión 12 (2026-07-24): Local Cluster Update + TShock 6.1.0 + Security

### Lo que se hizo
1. **Local cluster actualizado a TShock 6.1.0**: Rebuild de Docker image con TShock 6.1.0 + Terraria 1.4.5.6
2. **ChatBridge plugin arreglado**: API changes en Terraria 1.4.5.6 (Main.CallMeteor → /worldevent, NPC.StartInvasion → /worldevent, NPCID constants → int values)
3. **Mundo restaurado**: WORLD_NAME mismatch (Alo_teledelsia → MundoSobrinos2) causaba world generation en vez de load
4. **Seguridad**: API keys removidas del repo, secret.yaml en .gitignore, secret.yaml eliminados del tracking de git
5. **Commit squashed**: 11 commits → 1 commit limpio (sin secrets)

### Estado actual del cluster local
- **TShock**: 6.1.0.0 (Terraria 1.4.5.6)
- **Mundo**: MundoSobrinos2 (restaurado desde .bak2)
- **Pod**: terraria-server-0 (1/1 Ready)
- **Puertos**: 7777 (juego), 7878 (REST API), 7879 (ChatBridge)
