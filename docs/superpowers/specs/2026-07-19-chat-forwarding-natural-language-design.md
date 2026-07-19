# Spec: Chat Forwarding + Natural Language Agent

**Fecha**: 2026-07-19
**Estado**: Aprobado
**Autor**: roman + opencode

## Resumen

Conectar el Terraria Agent al chat del juego mediante un plugin TShock que reenvía todos los mensajes, y hacer que el agent entienda lenguaje natural (no solo comandos `/agente` predefinidos).

## Problema

El agent tiene un endpoint `/api/chat` que espera `ChatEvent` objects, pero nada se lo envía. Cuando un jugador escribe `/agente help` en el chat del juego, el mensaje nunca llega al agent. El agent está aislado.

## Solución

### Componente 1: TShock Plugin (ChatBridge)

Plugin mínimo que captura mensajes de chat y los reenvía al agent vía HTTP POST.

**Ubicación**: `terraria-server/docker/chatbridge/`

**Código del plugin** (~40 líneas):
- Clase `ChatBridgePlugin` que hereda de `TerrariaPlugin`
- Hook en `ServerApi.Hooks.ServerChat` para capturar cada mensaje
- POST a `http://terraria-agent:8080/api/chat` con body `{ "Player": "...", "Text": "..." }`
- Fire-and-forget (no bloquea el game server)
- Configurable via env var `AGENT_URL`

**Dockerfile change**: El plugin se compila durante el build de TShock:
- `dotnet new classlib` + referencia a `TShockAPI.dll` del release zip
- Se copia a `/tshock/ServerPlugins/ChatBridgePlugin.dll`
- La env var `AGENT_URL` se configura en el StatefulSet

**Referencias necesarias para compilar**:
- `TShockAPI.dll` (del release zip de TShock 6.0.0)
- `TerrariaServerAPI.dll` (del release zip)
- `OTAPI.dll` (del release zip)
- `Terraria.dll` (del release zip)
- `Newtonsoft.Json` (NuGet)
- `System.Text.Json` (built-in .NET 9)

### Componente 2: Agent — Natural Language

Se modifica el agent para procesar TODOS los mensajes de chat (no solo `/agente`).

**Flujo**:

```
Mensaje del jugador
        │
        ▼
¿Es comando /agente?
   ├── SÍ → CommandParser existente (narrar, hora, clima, tiempo, invocar, consejo, peligro)
   └── NO → Groq IntentParser (lenguaje natural)
                  │
                  ├── Acción detectada → ejecutar TShock command + narrar
                  └── Solo charla → responder narrativamente (o ignorar)
```

**Nuevo servicio: IntentParser**

Envía a Groq un prompt que analiza el mensaje del jugador y responde con JSON:

```json
{
  "action": "<tshock_command_opcional>",
  "narration": "<respuesta narrativa>"
}
```

**Prompt del IntentParser**:
- Incluye lista de comandos TShock disponibles
- Puede mapear lenguaje natural a comandos ("haz que llueva" → `rain 1`)
- Puede decidir NO narrar (responde `null`)
- Responde en español, tono épico/dramático
- Máximo 150 tokens

**Comandos mapeables por lenguaje natural**:

| Jugador dice | Comando TShock |
|-------------|---------------|
| "haz que llueva" / "lluvia" | `rain 1` |
| "para de llover" / "sol" | `rain 0` |
| "nieve" | `rain 2` |
| "tormenta" | `rain 3` |
| "pon el sol" / "es de día" | `time day` |
| "haz de noche" | `time night` |
| "mediodía" | `time noon` |
| "atardecer" | `time dusk` |
| "medianoche" | `time midnight` |
| "invoca al ojo" / "ojo de Cthulhu" | `spawnboss EyeOfCthulhu` |
| "invoca al gusano" / "devorador" | `spawnboss EaterOfWorlds` |
| "invoca al esqueleto" | `spawnboss Skeletron` |
| "invoca al slime" / "rey slime" | `spawnboss KingSlime` |
| "invoca a la abeja" / "abeja reina" | `spawnboss QueenBee` |
| "invoca a los gemelos" | `spawnboss TheTwins` |
| "invoca al destructor" | `spawnboss TheDestroyer` |
| "invoca al primo" / "Skeletron Prime" | `spawnboss SkeletronPrime` |
| "invoca a Plantera" | `spawnboss Plantera` |
| "invoca al Golem" | `spawnboss Golem` |
| "invoca al cultista" | `spawnboss LunaticCultist` |
| "invoca al Moon Lord" / "señor lunar" | `spawnboss MoonLord` |
| "luna de sangre" | `bloodmoon` |
| "eclipse" | `eclipse` |
| "lluvia de meteoritos" | `meteor` |

### Componente 3: Frecuencia / Anti-spam

El narrador **no responde a cada mensaje** del chat:

| Condición | Responde |
|-----------|----------|
| Mensaje empieza con `/agente` | Siempre |
| Mensaje menciona "narrador" o "agente" | Siempre |
| Chat normal (~70% de mensajes) | Groq decide: ~30% responde, ~70% ignora (`narration: null`) |

**Mecánica**: Groq recibe el prompt con la instrucción de responder `null` cuando no quiera narrar. El agent solo ejecuta/comunica si `narration != null`.

### Cambios en archivos

| Archivo | Cambio |
|---------|--------|
| `terraria-server/docker/chatbridge/ChatBridgePlugin.cs` | **NUEVO** — Plugin TShock |
| `terraria-server/docker/chatbridge/ChatBridge.csproj` | **NUEVO** — Proyecto .NET |
| `terraria-server/docker/Dockerfile` | Agregar build del plugin |
| `terraria-server/docker/bootstrap.sh` | Sin cambios (plugin va en ServerPlugins/) |
| `terraria-server/statefulset.yaml` | Agregar env var `AGENT_URL` |
| `terraria-agent/src/.../Controllers/ChatController.cs` | Agregar flujo natural language |
| `terraria-agent/src/.../Services/IntentParser.cs` | **NUEVO** — Servicio Groq para intención |
| `terraria-agent/src/.../Services/CommandParser.cs` | Sin cambios (se mantiene para /agente) |

### Datos de prueba

| Escenario | Input | Resultado esperado |
|-----------|-------|-------------------|
| Comando /agente | `/agente hora` | Narración descriptiva de la hora |
| Lenguaje natural → acción | "llueve por favor" | `rain 1` ejecutado + narración |
| Lenguaje natural → invocar | "invoca al ojo" | `spawnboss EyeOfCthulhu` + narración épica |
| Solo conversación | "buenas noches" | Solo narración (sin acción TShock) |
| Narrador decide ignorar | "yo que sé" | `null` → no se broadcastea |
| Mención directa | "narrador cuéntame algo" | Siempre responde |

### Infraestructura

- El plugin se compila en el Docker build de TShock (misma imagen, sin sidecar)
- El agent sigue siendo ClusterIP (no expuesto externamente)
- Comunicación: TShock plugin → `http://terraria-agent:8080/api/chat` (dentro del cluster)
- La env var `AGENT_URL` se configura en el StatefulSet de TShock

### Orden de implementación

1. Crear el proyecto del plugin ChatBridge
2. Modificar el Dockerfile de TShock para compilar el plugin
3. Agregar `AGENT_URL` al StatefulSet
4. Crear `IntentParser.cs` en el agent
5. Modificar `ChatController.cs` para usar IntentParser
6. Rebuild + redeploy TShock server
7. Rebuild + redeploy Agent
8. Test: escribir en el chat y verificar que el agent responde
