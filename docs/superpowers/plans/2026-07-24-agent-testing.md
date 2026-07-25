# Plan de Testing - Terraria Agent (Post-Upgrade)

## Objetivo
Verificar que todas las funcionalidades del agente funcionan correctamente después del upgrade de inteligencia.

## Prerrequisitos
- Agente corriendo en k3s (pod `terraria-agent-*` en namespace `terraria`)
- Servidor Terraria corriendo (pod `terraria-server-0`)
- Token de autenticación: `terraria-agent-secret-token-2024`

## Grupo 1: Crafting DB (194 items)

### Prueba 1.1: Item base (Tier 1)
**Mensaje:** `"como se fabrica copper bar"`
**Esperado:** Receta con 3x Copper Ore en Furnace

### Prueba 1.2: Item hardmode (nested)
**Mensaje:** `"como se fabrica excalibur"`
**Esperado:** Receta con 12x Hallowed Bars en Mythril/Orichalcum Anvil

### Prueba 1.3: Item con múltiples recetas
**Mensaje:** `"receta de terra blade"`
**Esperado:** Menciona True Night's Edge + Excalibur en Mythril Anvil

### Prueba 1.4: Item que no existe
**Mensaje:** `"como se fabrica pixel gun ultimate"`
**Esperado:** Respuesta indicando que no se encontró en la DB local

### Prueba 1.5: Item por nombre alternativo
**Mensaje:** `"megashark"`
**Esperado:** Info del Megashark (drop de Mech Boss, no crafting)

---

## Grupo 2: Boss Data (13 bosses)

### Prueba 2.1: Boss pre-hardmode
**Mensaje:** `"cuanta vida tiene eye of cthulhu"`
**Esperado:** 2800 HP, drop: Demonite, Unholy Arrow

### Prueba 2.2: Boss hardmode
**Mensaje:** `"stats de moon lord"`
**Esperado:** 150000 HP, drop: Luminite, Portal Gun, Moon Buggy

### Prueba 2.3: Boss con condiciones
**Mensaje:** `"como invocar eater of worlds"`
**Esperado:** 30 HP por segmento, invocar con Worm Food o en Corrupt Vile Cavern

### Prueba 2.4: Boss que no existe
**Mensaje:** `"cuanta vida tiene dragon lord"`
**Esperado:** Respuesta indicando que no se encontró info del boss

---

## Grupo 3: Game Knowledge (8 categorías)

### Prueba 3.1: Eventos
**Mensaje:** `"que eventos hay en terraria"`
**Esperado:** Lista de eventos (Blood Moon, Eclipse, Goblin Army, etc.)

### Prueba 3.2: NPCs
**Mensaje:** `"que NPCs dan loot"`
**Esperado:** Lista de NPCs con drops importantes

### Prueba 3.3: Biomas
**Mensaje:** `"que biomas hay"`
**Esperado:** Lista de biomas (Forest, Corruption, Crimson, Hallow, etc.)

### Prueba 3.4: Progresión
**Mensaje:** `"cual es el orden de progresion"`
**Esperado:** Orden lógico (pre-hardmode → Mech Bosses → Plantera → Golem → Moon Lord)

---

## Grupo 4: Memoria Persistente (SQLite)

### Prueba 4.1: Guardar contexto
**Mensaje:** `"mi personaje se llama Steve y usa armadura de titanio"`
**Esperado:** Confirmación de que recordará

### Prueba 4.2: Recuperar contexto
**Mensaje:** `"como se llama mi personaje"`
**Esperado:** "Steve" (recuperado de SQLite)

### Prueba 4.3: Contexto después de restart
**Acción:** Restart pod, esperar 30s
**Mensaje:** `"que armadura usa mi personaje"`
**Esperado:** "titanio" (persistido en SQLite)

---

## Grupo 5: Narración (Groq)

### Prueba 5.1: Narración básica
**Mensaje:** `"narrar una tormenta se acerca al pueblo"`
**Esperado:** Narración épica contextual con el estilo del juego

### Prueba 5.2: Narración con boss
**Mensaje:** `"narrar moon lord aparece en el cielo"`
**Esperado:** Narración dramática del boss fight

### Prueba 5.3: Consejo de juego
**Mensaje:** `"dame un consejo para jugar"`
**Esperado:** Consejo útil y contextualizado

---

## Grupo 6: Comandos del Juego

### Prueba 6.1: Hora
**Mensaje:** `"hora del dia"`
**Esperado:** Comando enviado al servidor (time)

### Prueba 6.2: Clima
**Mensaje:** `"pon lluvia"`
**Esperado:** Comando rain enviado

### Prueba 6.3: Invocar boss
**Mensaje:** `"invocar eye of cthulhu"`
**Esperado:** Comando spawnboss enviado vía ChatBridge

### Prueba 6.4: Broadcast
**Mensaje:** `"broadcast hola a todos"`
**Esperado:** Mensaje enviado a todos los jugadores

---

## Grupo 7: Edge Cases

### Prueba 7.1: Mensaje vacío
**Mensaje:** `""`
**Esperado:** Respuesta de ayuda o ignorable

### Prueba 7.2: Mensaje muy largo (>500 chars)
**Mensaje:** (texto largo conMuchas palabras...)
**Esperado:** Respuesta coherente sin crash

### Prueba 7.3: Caracteres especiales
**Mensaje:** `"ñáñáñá @#$%^&*()"`
**Esperado:** Respuesta coherente o de ayuda

### Prueba 7.4: Idioma mixto
**Mensaje:** `"how to craft excalibur en español"`
**Esperado:** Respuesta en español con receta

---

## Grupo 8: Combinaciones

### Prueba 8.1: Crafting + Narración
**Mensaje:** `"narrar como Steve forja Excalibur en la Mythril Anvil"`
**Esperado:** Narración épica + info de crafting correcta

### Prueba 8.2: Boss + Contexto
**Mensaje:** `"invocar moon lord, mi personaje tiene armadura de titanio"`
**Esperado:** Comando + contexto de dificultad

### Prueba 8.3: Múltiples requests
**Acción:** 5 mensajes seguidos (sin delay)
**Esperado:** Todos respondidos correctamente, sin rate limit

---

## Comandos de Ejecución

```bash
# Test individual
kubectl exec -n terraria <agent-pod> -- python3 -c "
import urllib.request, json
req = urllib.request.Request(
    'http://localhost:8080/api/chat',
    data=json.dumps({'message': '<MENSAJE>'}).encode(),
    headers={'Content-Type': 'application/json', 'X-Agent-Token': 'terraria-agent-secret-token-2024'},
    method='POST'
)
resp = urllib.request.urlopen(req, timeout=30)
print(resp.read().decode())
"

# Test batch (todos los grupos)
# Ver script: /home/roman/k8s-projects/terraria-agent/test-agent.sh
```

## Métricas de Éxito
- **Crafting:** 4/5 pruebas correctas (80%)
- **Bosses:** 3/4 pruebas correctas (75%)
- **Game Knowledge:** 3/4 pruebas correctas (75%)
- **Memoria:** 3/3 pruebas correctas (100%)
- **Narración:** 3/3 pruebas correctas (100%)
- **Comandos:** 4/4 pruebas correctas (100%)
- **Edge Cases:** 4/4 sin crash (100%)
- **Combinaciones:** 3/3 correctas (100%)

**Total esperado:** 27/31 pruebas pasan (87%)
