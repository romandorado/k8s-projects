#!/bin/bash
# Test Suite - Terraria Command Routing + Narration Hooks
# Run from: /home/roman/k8s-projects/terraria-server
# Targets LOCAL cluster (172.30.138.92) — same code as remote.
#
# Endpoints:
#   REST API      http://172.30.138.92:30788
#   Plugin        http://172.30.138.92:30789/execute
#   Agent         http://172.30.138.92:30808/terraria-agent/api/chat

TOKEN="terraria-agent-secret-token-2024"
REST="http://172.30.138.92:30788"
PLUGIN="http://172.30.138.92:30789/execute"
AGENT="http://172.30.138.92:30808/terraria-agent/api/chat"

PASS=0
FAIL=0
FAILED_TESTS=()

check() {
    local group="$1" name="$2" actual="$3" expected="$4"
    if [[ "$actual" == *"$expected"* ]]; then
        PASS=$((PASS+1))
        echo "  ✅ [$group] $name"
    else
        FAIL=$((FAIL+1))
        FAILED_TESTS+=("[$group] $name — got: $actual")
        echo "  ❌ [$group] $name"
        echo "     expected: $expected"
        echo "     actual:   $actual"
    fi
}

# --- Helpers ---------------------------------------------------------
plugin_cmd() {
    curl -s -m 15 -X POST "$PLUGIN" -H "Content-Type: application/json" -d "{\"command\":\"$1\"}"
}

agent_msg() {
    curl -s -m 90 -X POST "$AGENT" -H "Content-Type: application/json" \
        -H "X-Agent-Token: $TOKEN" -d "{\"Player\":\"tester\",\"Text\":\"$1\"}"
}

# Groq free tier ~30 RPM: delay 4s between agent calls
agent_test() {
    local group="$1" name="$2" msg="$3" expected="$4"
    local r
    r=$(agent_msg "$msg")
    check "$group" "$name" "$r" "$expected"
    sleep 4
}

rest_cmd() {
    curl -s -m 15 "$REST/v3/server/rawcmd?cmd=$1&token=$TOKEN"
}

h1() { echo ""; echo "=== $1 ==="; echo "------------------------------------"; }

# =====================================================================
h1 "GRUPO 1: REST API HEALTH"
# ---------------------------------------------------------------------
status=$(curl -s -m 15 "$REST/v2/server/status?token=$TOKEN")
check "REST" "Status 200 + world name" "$status" "MundoSobrinos2"
check "REST" "TShock version 6.1.0" "$status" "6.1.0.0"
check "REST" "Terraria version 1.4.5.6" "$status" "1.4.5.6"

players=$(curl -s -m 15 "$REST/v2/players/list?token=$TOKEN")
check "REST" "Players list endpoint (sin jugadores = [])" "$players" '"players": []'

noauth=$(curl -s -m 15 "$REST/v2/server/status")
check "REST" "Status without token rejected" "$noauth" "401"

nocmd=$(curl -s -m 15 "$REST/v3/server/rawcmd?cmd=/playing&token=WRONG")
check "REST" "Rawcmd wrong token rejected (403)" "$nocmd" "403"

# =====================================================================
h1 "GRUPO 2: PLUGIN COMMANDS (ejecución directa 7879)"
# ---------------------------------------------------------------------
r=$(plugin_cmd "time day");        check "Plugin" "time day" "$r" "day"
r=$(plugin_cmd "time night");      check "Plugin" "time night" "$r" "night"
r=$(plugin_cmd "time noon");       check "Plugin" "time noon" "$r" "noon"
r=$(plugin_cmd "time dusk");       check "Plugin" "time dusk" "$r" "dusk"
r=$(plugin_cmd "time midnight");   check "Plugin" "time midnight" "$r" "midnight"
r=$(plugin_cmd "time 10");         check "Plugin" "time 10 (SetTimeByHour)" "$r" "10:00"
r=$(plugin_cmd "time 22");         check "Plugin" "time 22 (noche)" "$r" "22:00"
r=$(plugin_cmd "time 99");         check "Plugin" "time 99 inválido" "$r" "unknown time"

r=$(plugin_cmd "worldevent bloodmoon");   check "Plugin" "worldevent bloodmoon" "$r" "blood moon started"
r=$(plugin_cmd "worldevent eclipse");     check "Plugin" "worldevent eclipse" "$r" "eclipse started"
r=$(plugin_cmd "worldevent fullmoon");    check "Plugin" "worldevent fullmoon" "$r" "full moon"
r=$(plugin_cmd "worldevent sandstorm");   check "Plugin" "worldevent sandstorm" "$r" "sandstorm"
r=$(plugin_cmd "worldevent slime");       check "Plugin" "worldevent slime" "$r" "slime rain"
r=$(plugin_cmd "worldevent lanternsnight"); check "Plugin" "worldevent lanternsnight" "$r" "lantern"
r=$(plugin_cmd "worldevent meteorshower");  check "Plugin" "worldevent meteorshower" "$r" "meteor"
r=$(plugin_cmd "worldevent coinrain");    check "Plugin" "worldevent coinrain" "$r" "coin"
r=$(plugin_cmd "worldevent star");        check "Plugin" "worldevent star" "$r" "star"
r=$(plugin_cmd "worldevent halloween");   check "Plugin" "worldevent halloween" "$r" "halloween"
r=$(plugin_cmd "worldevent xmas");        check "Plugin" "worldevent xmas" "$r" "xmas"
r=$(plugin_cmd "worldevent goblins");     check "Plugin" "invasion goblins" "$r" "goblin"
r=$(plugin_cmd "worldevent pirates");     check "Plugin" "invasion pirates" "$r" "pirate"
r=$(plugin_cmd "worldevent martians");    check "Plugin" "invasion martians" "$r" "martian"

r=$(plugin_cmd "bridge rain on");   check "Plugin" "bridge rain on" "$r" "rain started"
r=$(plugin_cmd "bridge rain off");  check "Plugin" "bridge rain off" "$r" "rain stopped"
r=$(plugin_cmd "bridge slime rain on");  check "Plugin" "bridge slime rain on" "$r" "slime rain started"
r=$(plugin_cmd "bridge slime rain off"); check "Plugin" "bridge slime rain off" "$r" "slime rain stopped"
r=$(plugin_cmd "bridge wind 15");   check "Plugin" "bridge wind 15" "$r" "wind set to 15"

r=$(plugin_cmd "spawnboss KingSlime");  check "Plugin" "spawnboss KingSlime (sin jugadores → no-op)" "$r" "no players online"
r=$(plugin_cmd "spawnboss TheTwins");   check "Plugin" "spawnboss TheTwins (sin jugadores → no-op)" "$r" "no players online"
r=$(plugin_cmd "spawnmob Zombie 5");    check "Plugin" "spawnmob Zombie 5" "$r" "executed"

# cleanup events
plugin_cmd "worldevent bloodmoon" >/dev/null
plugin_cmd "worldevent eclipse" >/dev/null
plugin_cmd "bridge slime rain off" >/dev/null
plugin_cmd "time day" >/dev/null

# =====================================================================
h1 "GRUPO 3: AGENT ROUTING — tiempo, clima, eventos"
# ---------------------------------------------------------------------
echo "  (cada test: 1 llamada Groq + 4s delay para rate limit)"
agent_test "Agent" "tiempo: hora exacta" "pon las 10 de la mañana" '"action":"time 10"'
agent_test "Agent" "tiempo: noche" "haz que sea de noche" '"action":"time night"'
agent_test "Agent" "clima: lluvia" "haz llover" "rain"
agent_test "Agent" "clima: local action rain off" "para la lluvia" "bridge rain off"
agent_test "Agent" "evento: bloodmoon" "que caiga una luna de sangre" "bloodmoon"
agent_test "Agent" "evento: eclipse" "provoca un eclipse" "eclipse"
agent_test "Agent" "evento: slime rain local action" "detén la lluvia de slimes" "bridge slime rain off"
agent_test "Agent" "evento: halloween" "que sea halloween en el mundo" "halloween"

# =====================================================================
h1 "GRUPO 4: AGENT ROUTING — bosses y mobs"
# ---------------------------------------------------------------------
agent_test "Agent" "boss: ojo → EyeOfCthulhu" "invoca al ojo" "EyeOfCthulhu"
agent_test "Agent" "boss: gemelos → TheTwins" "invoca a los gemelos" "TheTwins"
agent_test "Agent" "boss: moon lord" "que aparezca moon lord" "MoonLord"
agent_test "Agent" "mob: zombies" "spawnea 10 zombies" "spawnmob"

# =====================================================================
h1 "GRUPO 5: AGENT ROUTING — jugadores, teleport, world"
# ---------------------------------------------------------------------
agent_test "Agent" "player: heal" "cura a pepe" '"action":"heal'
agent_test "Agent" "player: tp" "teletransporta a pepe a mi posicion" '"action":"tp'
agent_test "Agent" "teleport: spawn" "llevame al spawn" "spawn"
agent_test "Agent" "world: save" "guarda el mundo" "save"
agent_test "Agent" "invasion: goblins" "invoca una invasion de goblins" "goblin"

# =====================================================================
h1 "GRUPO 6: SYSTEM EVENTS (Player=Sistema)"
# ---------------------------------------------------------------------
r=$(curl -s -m 90 -X POST "$AGENT" -H "Content-Type: application/json" \
    -H "X-Agent-Token: $TOKEN" \
    -d '{"Player":"Sistema","Text":"El jugador tester murio asesinado por un Zombi"}' )
check "SysEvent" "muerte narrada" "$r" '"systemEvent":true'
check "SysEvent" "narración no vacía" "$r" "narration"
sleep 4

r=$(curl -s -m 90 -X POST "$AGENT" -H "Content-Type: application/json" \
    -H "X-Agent-Token: $TOKEN" \
    -d '{"Player":"Sistema","Text":"El jefe King Slime fue derrotado"}' )
check "SysEvent" "jefe derrotado narrado" "$r" '"systemEvent":true'
sleep 4

r=$(curl -s -m 90 -X POST "$AGENT" -H "Content-Type: application/json" \
    -H "X-Agent-Token: $TOKEN" \
    -d '{"Player":"Sistema","Text":"El jugador pepe se ha unido al mundo"}' )
check "SysEvent" "join narrado" "$r" '"systemEvent":true'
sleep 4

# =====================================================================
h1 "GRUPO 7: EDGE CASES"
# ---------------------------------------------------------------------
r=$(curl -s -m 15 -X POST "$AGENT" -H "Content-Type: application/json" \
    -H "X-Agent-Token: WRONG" -d '{"Player":"tester","Text":"hola"}')
check "Edge" "agent wrong token → 401" "$r" "401"

r=$(curl -s -m 15 -X POST "$AGENT" -H "Content-Type: application/json" \
    -H "X-Agent-Token: $TOKEN" -d '{"Player":"tester","Text":"hola"}')
check "Edge" "saludo ignorado (sin respuesta)" "$r" ""

r=$(curl -s -m 15 -X POST "$AGENT" -H "Content-Type: application/json" \
    -H "X-Agent-Token: $TOKEN" -d '{"Player":"tester","Text":"."}')
check "Edge" "punto ignorado" "$r" ""

r=$(curl -s -m 15 -X POST "$PLUGIN" -H "Content-Type: application/json" -d '{}')
check "Edge" "plugin sin command (bug: 500, debería 400)" "$r" "not present"

r=$(curl -s -m 15 -X POST "$PLUGIN" -H "Content-Type: application/json" -d '{"command":""}')
check "Edge" "plugin command vacío → 400" "$r" "empty command"

r=$(curl -s -o /dev/null -w "%{http_code}" -m 15 -X GET "$PLUGIN")
check "Edge" "plugin GET → 405" "$r" "405"

# =====================================================================
h1 "RESUMEN FINAL"
echo "  ✅ PASS: $PASS"
echo "  ❌ FAIL: $FAIL"
if [ ${#FAILED_TESTS[@]} -gt 0 ]; then
    echo ""
    echo "  Fallos:"
    for t in "${FAILED_TESTS[@]}"; do
        echo "    - $t"
    done
fi
echo "====================================="
[ "$FAIL" -eq 0 ]
