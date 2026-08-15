# Agent Hardening — Design Spec

## Problem

El agente de Terraria "a veces es un poco torpe". Fallos observados en logs (sesión 2026-08-07):

1. **Confunde comandos** — `"muralla de carne"` invocó Eater of Worlds, `"dame la ceni"` dio Life Fruit a `'hor net'` (jugador equivocado), comandos inventados o incompletos (`give` sin item, `buff`).
2. **No hace nada** — frases que deberían disparar una acción quedan sin respuesta o con respuesta genérica (`"creame una espada"`).
3. **Inventa resultados** — el agente narra "te he dado X" cuando el servidor rechazó el comando (falso éxito).
4. **Lento / se queda callado** — rate limit del tier gratuito de Groq (~30 RPM) produce silencio o demoras.
5. **Narración rara** — respuestas fuera de contexto o JSON roto/truncado.

**Causa raíz:** el modelo pequeño de Groq (`llama-3.1-8b-instant`) hace el **enrutado de comandos** (tarea determinista que hace mal) y el **rate limit** causa el silencio. Las rutas locales deterministas (give, hora, eventos de stop) no fallan nunca; toda la torpeza vive en el camino de Groq.

## Goal

Mantener Groq como router (enfoque B elegido por el usuario), pero **blindarlo**: validar todo lo que ejecuta, darle conocimiento de jugadores online, y asegurar que nunca se quede callado ni narre falsos éxitos.

## Diseño

### 1. Capa de validación de acciones (`ActionValidator`, nuevo)

Servicio nuevo `Services/ActionValidator.cs`. Antes de ejecutar cualquier `action` que devuelva Groq en `ChatController` (Ruta 3), se valida contra una whitelist de patrones por **familia de comando** (validación por forma, NO regex rígido).

**Principio:** bloquear solo lo estructuralmente inválido (verbos inventados, argumentos inexistentes, comandos incompletos). Aceptar variaciones válidas: jugadores con acentos/espacios, mobs en minúsculas, cantidades opcionales.

**Familias y reglas:**

| Familia | Forma válida | Normalización |
|---|---|---|
| `time` | `day\|night\|noon\|dusk\|midnight` o hora 0-23 | — |
| `bridge rain` | `on\|off\|heavy` | — |
| `bridge slime rain` | `on\|off` | — |
| `worldevent` | `bloodmoon\|eclipse\|fullmoon\|sandstorm\|meteor\|lanternsnight\|meteorshower\|coinrain\|star\|halloween\|xmas\|goblins\|pirates\|martians` | — |
| `hardmode` | sin argumentos | — |
| `spawnboss` | uno de los 13 nombres canónicos | Normalizar nombre (minúsculas, `the twins`→`TheTwins`, `eye of cthulhu`→`EyeOfCthulhu`, `muralla de carne`→`WallOfFlesh`, etc.). Rechazar bosses inventados. |
| `spawnmob` | nombre de mob + cantidad opcional | — |
| `give` | **se desvía a `HandleGiveAsync`**: parsear item/jugador/cantidad y reconstruir `give "Item" Jugador N` | El más tolerante; casi cualquier variación se parsea localmente |
| `heal\|godmode` | sin jugador (a ti mismo) o con jugador | — |
| `maxhp` | sin jugador o con jugador | — |
| `kill\|kick\|mute\|slap` | jugador + argumentos opcionales | — |
| `tp\|tphere` | jugador | — |
| `home\|spawn` | sin argumentos | — |
| `warp` | `list\|add <nombre>\|<nombre>` | — |
| `setspawn\|settle\|butcher\|save` | sin argumentos | — |
| `maxspawns\|spawnrate` | número | — |

**Comportamiento ante inválido:** NO se ejecuta. Narración honesta pidiendo aclaración (formato `"No puedo ejecutar eso. {narración de Groq}"`). Log de la acción rechazada para debugging.

**Extensibilidad:** patrones centralizados en un único sitio (diccionario familia → validador), fácil de ampliar.

### 2. Conocimiento de jugadores online (`OnlinePlayersService`, nuevo)

Servicio nuevo `Services/OnlinePlayersService.cs`. Consulta `/v2/players/list` (REST) con **cache corta (~5s)** para no llamar a la API en cada mensaje.

Dos capas:

1. **Inyección en el prompt**: en `IntentParser`, se añade al `world_status`:
   ```
   JUGADORES ONLINE: Testeador1, Sobrino2
   ```
   (vacío si no hay). Así Groq sabe a quién dirigirse y no inventa nombres.

2. **Guardia de ejecución**: al validar una acción con jugador (`give`, `heal`, `maxhp`, `kill`, `kick`, `tp`, `tphere`, `godmode`, `mute`, `slap`), se extrae el destinatario y se comprueba contra la lista real. Si no está online → NO se ejecuta y se narra honesto (`"{jugador} no está conectado"`).

   - `heal`/`maxhp`/`godmode` sin jugador = a ti mismo → no requiere comprobación (quien pide está online por definición).
   - Si la API de players falla (servidor caído), se **omite la guardia** (fail-open) y se deja que `LooksLikeCommandFailure` capture el rechazo.

### 3. Prompt más estricto (`IntentParser`)

Reestructurar `SystemPrompt` sin perder reglas útiles:

1. **Formato JSON rígido**: claves exactas `respond`/`action`/`narration` en inglés, sin markdown, sin texto extra.
2. **Few-shot**: 4-5 pares correctos (frase → JSON), incluyendo un ejemplo de acción inválida que debe devolver `action: null` con pregunta de aclaración.
3. **Regla anti-alucinación**: "Si el comando exacto no está en la lista, `action` DEBE ser null y pregunta en la narration. NUNCA inventes comandos ni modifiques la sintaxis."
4. **Honestidad**: reforzar "describe lo que el comando hace, no el resultado" extendido a `give`, `spawnboss`, `spawnmob` (ya intentado para `heal`/`maxhp`).
5. **Limpieza**: consolidar reglas sueltas de parches recientes en bloques ordenados (tiempo, clima, bosses, jugadores).

Mantener intacto el pipeline de parsing (BOM, extracción JSON, `SalvageNarration`, fallback de claves españolas).

### 4. Velocidad y no quedarse callado

1. **Backoff en 429**: hoy un único reintento tras 12s. Añadir backoff exponencial (12s → 24s). Si sigue en 429, responder con mensaje honesto tipo "estoy saturado, repítemelo en un momento" (nunca silencio total).
2. **Narración por defecto**: si la respuesta de Groq llega vacía/truncada y `SalvageNarration` no extrae nada, usar "¡Cuéntamelo otra vez, héroe!" en vez de silencio.
3. **Reducir llamadas innecesarias**: el alivio real de rate limit viene de (a) las rutas locales existentes que ya se saltan Groq (give, hora, stop events), (b) el backoff y (c) el token bucket. La narración de Groq que ya viene con la respuesta se usa siempre (descartarla no ahorra llamadas). Para acciones mecánicas sin drama (`time`, `save`, `setspawn`, `settle`, `butcher`, `maxspawns`, `spawnrate`) se prioriza la narración de Groq SOLO si es sustancial; si es genérica, se reemplaza por una breve ("Hecho: {comando}") para consistencia — sin coste extra de API.
4. **Token bucket compartido**: `AutoEventService` (eventos automáticos de sistema) también consume Groq; usar un token bucket común con el chat para no competir.

## Arquitectura / Flujo

```
Chat/evento → ChatController.HandleEvent
  ├─ Ruta 0/2a/2b/2c (Sistema, atajos locales, hora, give) — sin cambios
  └─ Ruta 3: IntentParser (Groq, prompt reforzado) ──→ IntentResult
        └─ ActionValidator (nuevo)
              ├─ give → normalizar → HandleGiveAsync (confirmación honesta)
              ├─ spawnboss → normalizar nombre → validar contra los 13
              ├─ resto → validación por forma por familia
              └─ inválido → no ejecutar, narración honesta
        └─ OnlinePlayersService (nuevo)
              ├─ inyecta jugadores online en el prompt
              └─ guardia: acciones con jugador → ¿online? si no, no ejecutar
        └─ Ejecución vía TShockClient + LooksLikeCommandFailure (existente)
  └─ Robustez: backoff 429, narración por defecto, token bucket de sistema
```

## Archivos

- `terraria-agent/src/Terraria.Agent.Api/Services/ActionValidator.cs` — NUEVO
- `terraria-agent/src/Terraria.Agent.Api/Services/OnlinePlayersService.cs` — NUEVO
- `terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs` — MODIFICADO (prompt, inyección jugadores, backoff)
- `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs` — MODIFICADO (validación, guardia, narración mecánica)
- `terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs` — MODIFICADO (token bucket)
- `terraria-agent/src/Terraria.Agent.Api/Services/AutoEventService.cs` — MODIFICADO (token bucket)
- `terraria-agent/src/Terraria.Agent.Api/Program.cs` — MODIFICADO (registro DI de los nuevos servicios)

## Testing

- Build C#: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src/Terraria.Agent.Api mcr.microsoft.com/dotnet/sdk:10.0 dotnet build -c Release`
- Casos de validación (unitarios sobre `ActionValidator`):
  - `spawnboss EyeOfCthulhu` → OK; `spawnboss Medusa` → rechazado; `spawnboss eye of cthulhu` → normalizado OK
  - `give` a secas → rechazado; `give "Iron Bar" Testeador1 20` → OK; variaciones sin comillas → normalizado
  - `time 25` → rechazado; `time 10` → OK; `buff` → rechazado
  - `worldevent sandstorm` → OK; `worldevent lluvia` → rechazado (evento inexistente)
- E2E contra el remoto con la ruta `POST /terraria-agent/api/chat` y verificación de acción + narración en la respuesta.
- Despliegue: tag único por deploy (`terraria-agent:v4-...`) en local + remoto (REGLA 2).

## Fuera de alcance (YAGNI)

- Router determinista completo (enfoque A) — el usuario eligió B.
- Modelo de pago / cola persistente (enfoque C) — posible en una iteración futura.
- Nueva funcionalidad del agente más allá de blindar lo existente.
