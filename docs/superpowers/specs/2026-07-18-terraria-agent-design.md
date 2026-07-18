# Terraria Agent - Diseño Detallado

## Resumen

El Terraria Agent es una aplicación .NET 10 que funciona como narrador del juego, integrándose con TShock vía un plugin y Groq AI para generar narración dramática. Los jugadores controlan al agente mediante comandos `/agente` en el chat del juego.

## Arquitectura

```
┌─────────────────────────────────────────────────────────────────┐
│                    TERRARIA SERVER POD                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  TShock + EventNotifier Plugin                           │  │
│  │  - Hook ServerChat → POST /api/chat                     │  │
│  │  - REST API habilitado (puerto 7878)                     │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              ↓ HTTP POST (chat events)
┌─────────────────────────────────────────────────────────────────┐
│                    TERRARIA AGENT POD                            │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  .NET 10 API (puerto 8080)                               │  │
│  │  - ChatController: recibe eventos del plugin             │  │
│  │  - CommandParser: detecta /agente [comando]              │  │
│  │  - TShockClient: ejecuta comandos vía REST               │  │
│  │  - GroqService: genera narración con IA                  │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              ↓ HTTP GET/POST
┌─────────────────────────────────────────────────────────────────┐
│                    GROQ API                                     │
│  - llama-3.3-70b-versatile                                     │
│  - System prompt: "Eres el narrador del mundo Terraria..."      │
└─────────────────────────────────────────────────────────────────┘
```

## Componentes

### 1. Plugin TShock (EventNotifier)

**Estructura:**
```
EventNotifier/
├── EventNotifier.cs
├── EventNotifier.csproj
└── config.json
```

**Eventos capturados:**
| Evento | Hook | Payload |
|--------|------|---------|
| Chat | `ServerChat` | `{ player, text, timestamp }` |
| Jugador entra | `PlayerJoin` | `{ player, ip }` |
| Jugador sale | `PlayerLeave` | `{ player }` |
| Boss spawn | `NpcSpawn` | `{ npcId, npcName }` |
| Jugador muere | `PlayerDeath` | `{ player, killer }` |

**Config:**
```json
{
  "AgentApiUrl": "http://terraria-agent:8080",
  "ApiKey": "terraria-agent-secret",
  "Events": ["chat", "join", "leave", "npcspawn", "death"]
}
```

### 2. Agent API (.NET 10)

**Estructura:**
```
terraria-agent/
├── src/Terraria.Agent.Api/
│   ├── Controllers/
│   │   └── ChatController.cs
│   ├── Services/
│   │   ├── TShockClient.cs
│   │   ├── GroqService.cs
│   │   └── CommandParser.cs
│   ├── Models/
│   │   ├── ChatEvent.cs
│   │   └── AgentCommand.cs
│   ├── Program.cs
│   └── appsettings.json
├── k8s/
│   ├── namespace.yaml
│   ├── deployment.yaml
│   ├── service.yaml
│   └── secret.yaml
└── Dockerfile
```

**Comandos soportados:**
| Comando | Acción | Ejemplo |
|---------|--------|---------|
| `/agente narrar [escena]` | Groq genera narración | `/agente narrar la tormenta se intensifica` |
| `/agente hora` | Informa hora del día | `/agente hora` → "Son las 3:42 PM..." |
| `/agente clima [tipo]` | Cambia clima | `/agente clima lluvia` |
| `/agente tiempo [valor]` | Cambia hora | `/agente tiempo noche` |
| `/agente invocar [boss]` | Invoca boss | `/agente invocar wall of flesh` |
| `/agente consejo` | Tips de juego | `/agente consejo` → Groq da consejo |
| `/agente peligro` | Advierte enemigos | `/agente peligro` → "¡Cuidado!" |

### 3. Groq Integration

**System prompt:**
```
Eres el narrador del mundo "MundoSobrinos" en Terraria. Tu rol:
- Narrar eventos del juego de forma dramática y divertida
- Responder a comandos de jugadores con creatividad
- Mantener el tono épico pero amigable (juegas con sobrinos)
- Usar español casual y emojis ocasionales

Contexto del mundo:
- Mundo: MundoSobrinos (Master, grande)
- Jugadores: [se actualiza dinámicamente]
- Hora del día: [se actualiza]
- Clima actual: [se actualiza]

Reglas:
- Máximo 200 tokens por respuesta
- Responde en español
- Sé conciso (1-2 oraciones máximo)
- Recuerda: en Master los enemigos son brutales, drops exclusivos, y la dificultad es extrema
```

## Configuración del Mundo

| Setting | Valor |
|---------|-------|
| Nombre | MundoSobrinos |
| Tamaño | Grande (`-autocreate 3`) |
| Dificultad | Master (`DIFFICULTY: "2"`) |
| Max jugadores | 8 |
| Autosave | true (cada 10 min) |

## Kubernetes Deployment

**Namespace:** `terraria` (compartido con Terraria Server)

**Recursos:**
```yaml
resources:
  requests:
    memory: "256Mi"
    cpu: "100m"
  limits:
    memory: "512Mi"
    cpu: "250m"
```

**Secrets:**
- `tshock-token`: Token de autenticación REST de TShock
- `groq-api-key`: API key de Groq

**Servicios:**
- `terraria-agent:8080` → ClusterIP (accesible desde el plugin)

## Flujo de Datos

1. Jugador escribe `/agente invocar boss` en chat
2. Plugin detecta mensaje → POST a `http://terraria-agent:8080/api/chat`
3. Agent parsea comando → ejecuta `spawnboss WallOfFlesh` vía TShock REST
4. Agent genera narración con Groq → envía `say` al servidor
5. Todos los jugadores ven el mensaje narrado

## Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| Runtime | .NET 10 |
| AI Provider | Groq (llama-3.3-70b-versatile) |
| Comunicación | HTTP REST |
| Container | Docker (mcr.microsoft.com/dotnet/aspnet:10.0) |
| Orquestación | Kubernetes (k3s) |

## Criterios de Aceptación

1. ✅ Plugin captura eventos del servidor y los envía al Agent
2. ✅ Agent detecta comandos `/agente` y ejecuta acciones
3. ✅ Agent genera narración con Groq para comandos narrativos
4. ✅ Todos los jugadores ven los mensajes del agente
5. ✅ El mundo se crea en tamaño Grande y dificultad Master
6. ✅ El Agent se despliega como Deployment en Kubernetes
7. ✅ Los secrets se almacenan de forma segura en K8s

## Despliegue del Plugin

El plugin se despliega via **custom Docker image** del servidor Terraria:

1. Compilar `EventNotifier.csproj` → `EventNotifier.dll`
2. Copiar DLL a `ServerPlugins/` dentro del Dockerfile
3. Habilitar REST API en configmap: `RestApiEnabled: true`
4. Crear Application REST Token en config (no expira)

**Dockerfile del servidor (modificado):**
```dockerfile
FROM ryshe/terraria:latest
COPY --from=build /app/EventNotifier.dll /ServerPlugins/
```

**Configmap updates:**
```yaml
# terraria-config
RestApiEnabled: "true"
RestApiPort: "7878"
```

## Limitaciones Conocidas

1. **Latencia Groq**: Puede haber 1-3 segundos de delay en narraciones
2. **Rate limits**: Groq tiene límites de requests por minuto
3. **Tokens**: System prompt + contexto puede crecer si hay muchos jugadores
4. **State**: El Agent no persiste estado entre reinicios (stateless)
