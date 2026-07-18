# Contexto del Proyecto - Kubernetes Learning

## Estado Actual
- **Fecha**: 2026-07-18 (última actualización: 22:30)
- **Fase**: Despliegue en Kubernetes
- **Git**: Repositorio con 25 commits
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
│   │  - Puerto: 7777                                          │  │
│   │  - PersistentVolume: 5Gi para mundos                     │  │
│   │  - ConfigMap con parámetros                              │  │
│   │  - ⚠️ ROTO: 135+ restarts, bootstrap互动模式            │  │
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
| Terraria Server | ryshe/terraria (TShock) | StatefulSet | LoadBalancer | 7777 |
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
- [ ] **ARREGLAR Terraria Server** — Ver sección de problemas abajo
- [ ] Desplegar Supermarket (Frontend + API)
- [ ] Verificar funcionamiento de todos los servicios

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

## Problemas Conocidos

### Terraria Server (Roto)
- **Estado**: Pod Running pero 0/1 Ready, 135+ restarts en 31h
- **Error**: `bootstrap.sh: 11: [: =: unexpected operator`
- **Causa**: El servidor entra en modo interactivo (setup de mundo) en vez de crear el mundo automáticamente
- **Falta**: `WORLD_FILENAME` env var, mundo pre-existente, o script de auto-creación
- **Imagen**: `ryshe/terraria:latest` (TShock 6.1.0, Terraria 1.4.5.6)
- **Next step**: Diseñar solución (ver brainstorming pendiente)

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
