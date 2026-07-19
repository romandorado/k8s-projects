# Contexto del Proyecto - Kubernetes Learning

## Estado Actual
- **Fecha**: 2026-07-19 (última actualización: 02:45)
- **Fase**: Despliegue en Kubernetes
- **Git**: Repositorio con 28 commits
- **GitHub**: https://github.com/romandorado/k8s-projects

## Arquitectura Final
```
┌─────────────────────────────────────────────────────────────────┐
│                     KUBERNETES CLUSTER                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   NAMESPACE: terraria                                            │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  TERRARIA SERVER (StatefulSet)                           │  │
│   │  - Image: terraria-tshock-1455 (TShock 6.0.0 + 1.4.5.5)│  │
│   │  - Puerto: 7777 (game) + 7878 (REST API)                │  │
│   │  - PersistentVolume: 5Gi para mundos + SQLite DB         │  │
│   │  - REST API habilitado con token auth                    │  │
│   │  - Agent user con permisos owner                         │  │
│   │  - ✅ Working: world gen, REST API, scale 0↔1            │  │
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
| Terraria Server | terraria-tshock-1455 (TShock 6.0.0 + 1.4.5.5) | StatefulSet | LoadBalancer | 7777 + 7878 |
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
- [x] **TShock 6.0.0 for 1.4.5.5** — Custom Docker image + REST API funcional
- [ ] **Usuario prueba Terraria** — Crear cuenta para el usuario (con pirata 1.4.5.5)
- [ ] **Eventos automáticos Agent** — Ciclo día/noche, boss fights, amanecer con Groq
- [ ] Desplegar Supermarket (Frontend + API)
- [ ] Verificar funcionamiento de todos los servicios

## Dónde nos quedamos (Sesión 5 - 2026-07-19)

### Último estado conocido
- **Terraria Server**: 1/1 Ready, TShock 6.0.0, v1.4.5.5, REST API en 7878
- **Terraria Agent**: 1/1 Ready, .NET 10 + Groq, comandos `/agente` funcionando
- **Pods corriendo**: `terraria-server-0` (IP: 10.42.0.170), `terraria-agent-xxxxx` (IP: 10.42.0.156)
- **World**: MundoSobrinos2 (Large, 8400x2400, Master difficulty)
- **Commit más reciente**: `5f77d3b` (docs) + `adb2c10` (feat)

### Próximos pasos (cuando el usuario regrese)
1. **Probar conexión real**: El usuario conecta con cliente Terraria 1.4.5.5 pirata a `172.30.138.92:7777`
2. **Crear usuario en TShock**: Cuando el usuario se una, crearle cuenta con `/setup` o vía SQLite
3. **Eventos automáticos**: Implementar narrativa automática (ciclo día/noche, boss fights)
4. **Supermarket**: Desplegar Frontend + API (pendiente desde hace 3 sesiones)

### Archivos clave para continuar
```
terraria-server/docker/Dockerfile          # Custom image TShock 6.0.0 + 1.4.5.5
terraria-server/docker/bootstrap.sh        # Config TShock + user creation + REST API
terraria-server/statefulset.yaml           # K8s manifest con env vars
terraria-agent/src/Terraria.Agent.Api/     # App .NET 10 + Groq
terraria-agent/k8s/deployment.yaml         # K8s deployment Agent
CONTEXT.md                                 # Este archivo (contexto completo)
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

## Problemas Conocidos

### ~~Terraria Server (Roto)~~ ✅ FIXED
- **Causa original**: Falta `WORLD_FILENAME` env var + args `-autocreate`
- **Fix aplicado**: Custom Docker image con TShock 6.0.0 para 1.4.5.5
- **Estado actual**: 1/1 Ready, REST API funcional, Agent conectado, usuario `agent` creado

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
| Groq API key | En K8s secret `investigation-team-chat-api-secret` |
| Memory extraction threshold | Cada 20 mensajes |
| Antonio agent ID | `ac8ca2c7-ae4c-43e0-bb8e-876f03480713` |
| TShock REST token | `terraria-agent-secret-token-2024` |
| TShock user | `agent` / password `agent1234` / grupo `owner` |
| Agent auth | `X-Agent-Token: terraria-agent-secret-token-2024` |
| Connect | `172.30.138.92:7777` (sin contraseña) |

## Notas del Usuario
- Quiere algo usable a futuro
- Tiene experiencia básica con Docker/Kubernetes (1 año)
- Busca practicar cosas reales para su día a día
- Tiene servidor de Terraria que levanta de vez en cuando para jugar con sobrinos
- Skills: .NET, Angular, PostgreSQL
- Idioma: Español (comunicación y documentación)
- Quiere que se guarde todo el contexto entre sesiones

## Perfil de Trabajo - Opencode (IA)

### Comandos Disponibles
| Comando | Descripción |
|---------|-------------|
| `sudo k3s kubectl get pods --all-namespaces` | Ver estado de pods |
| `sudo k3s kubectl logs -n <ns> <pod> --tail=N` | Ver logs de un pod |
| `sudo k3s kubectl describe pod -n <ns> <pod>` | Diagnosticar problemas de pod |
| `sudo k3s kubectl rollout restart deployment/<name> -n <ns>` | Redeploy sin rebuild |
| `sudo docker build --no-cache -t <tag>:latest .` | Build de imagen Docker |
| `sudo docker save <tag>:latest \| sudo k3s ctr images import -` | Importar imagen a k3s |
| `curl -s -X POST http://localhost:<port>/<path> -H "Content-Type: application/json" -d '{...}'` | Test de endpoints |
| `sudo k3s kubectl exec -n <ns> <pod> -- curl -s http://<svc>:<port>/<path>` | Test dentro del cluster |

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

### Stack del Proyecto
- **Runtime**: k3s (轻量 Kubernetes)
- **Registry**: Docker local → k3s ctr images import
- **Frontend**: Angular 22 + Nginx (proxy reverso)
- **Backend**: .NET 10 + Entity Framework Core
- **DB**: PostgreSQL 16
- **AI**: Groq API (llama-3.3-70b-versatile) — antes era Gemini (requiere billing)
- **Auth**: JWT + BCrypt
- **Puertos LoadBalancer**: 30080 (homepage), 30081 (IT frontend), 32444 (IT API), 32445 (chat API)
