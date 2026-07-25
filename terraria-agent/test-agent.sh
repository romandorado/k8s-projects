#!/bin/bash
# Test Script - Terraria Agent (Post-Upgrade)
# Run from: /home/roman/k8s-projects/terraria-agent

set -e

AGENT_POD=$(sudo k3s kubectl get pods -n terraria -l app=terraria-agent -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
TOKEN="terraria-agent-secret-token-2024"
NAMESPACE="terraria"

if [ -z "$AGENT_POD" ]; then
    echo "❌ Agent pod not found"
    exit 1
fi

echo "🤖 Testing Agent: $AGENT_POD"
echo "================================"

test_chat() {
    local test_name="$1"
    local message="$2"
    local expected="$3"
    local group="$4"
    
    echo ""
    echo "📝 [$group] $test_name"
    echo "   Message: $message"
    
    # Execute test via kubectl exec with python3
    result=$(sudo k3s kubectl exec -n $NAMESPACE $AGENT_POD -- python3 -c "
import urllib.request, json, sys
try:
    req = urllib.request.Request(
        'http://localhost:8080/api/chat',
        data=json.dumps({'message': '''$message'''}).encode(),
        headers={'Content-Type': 'application/json', 'X-Agent-Token': '$TOKEN'},
        method='POST'
    )
    resp = urllib.request.urlopen(req, timeout=30)
    print(resp.read().decode()[:200])
except Exception as e:
    print(f'ERROR: {e}', file=sys.stderr)
    sys.exit(1)
" 2>&1)
    
    if [ $? -eq 0 ]; then
        echo "   ✅ Response: $(echo "$result" | head -c 150)..."
        return 0
    else
        echo "   ❌ Failed: $result"
        return 1
    fi
}

# ============================================
# GRUPO 1: CRAFTING DB
# ============================================
echo ""
echo "📦 GRUPO 1: CRAFTING DB (194 items)"
echo "------------------------------------"

test_chat "Copper Bar" "como se fabrica copper bar" "3x Copper Ore" "Crafting"
test_chat "Excalibur" "como se fabrica excalibur" "12x Hallowed Bars" "Crafting"
test_chat "Terra Blade" "receta de terra blade" "True Night's Edge" "Crafting"
test_chat "Non-existent" "como se fabrica pixel gun ultimate" "no se encontró" "Crafting"
test_chat "Megashark" "megashark" "drop" "Crafting"

# ============================================
# GRUPO 2: BOSS DATA
# ============================================
echo ""
echo "👹 GRUPO 2: BOSS DATA (13 bosses)"
echo "------------------------------------"

test_chat "Eye of Cthulhu HP" "cuanta vida tiene eye of cthulhu" "2800" "Bosses"
test_chat "Moon Lord Stats" "stats de moon lord" "150000" "Bosses"
test_chat "Eater of Worlds" "como invocar eater of worlds" "Worm Food" "Bosses"
test_chat "Non-existent Boss" "cuanta vida tiene dragon lord" "no se encontró" "Bosses"

# ============================================
# GRUPO 3: GAME KNOWLEDGE
# ============================================
echo ""
echo "📚 GRUPO 3: GAME KNOWLEDGE (8 categories)"
echo "------------------------------------"

test_chat "Events" "que eventos hay en terraria" "Blood Moon" "Knowledge"
test_chat "NPCs" "que NPCs dan loot" "NPC" "Knowledge"
test_chat "Biomes" "que biomas hay" "Forest" "Knowledge"
test_chat "Progression" "cual es el orden de progresion" "Moon Lord" "Knowledge"

# ============================================
# GRUPO 4: MEMORY (SQLite)
# ============================================
echo ""
echo "🧠 GRUPO 4: MEMORY (SQLite Persistence)"
echo "------------------------------------"

test_chat "Save Context" "mi personaje se llama Steve y usa armadura de titanio" "recordar" "Memory"
test_chat "Retrieve Context" "como se llama mi personaje" "Steve" "Memory"
test_chat "Armor Context" "que armadura usa mi personaje" "titanio" "Memory"

# ============================================
# GRUPO 5: NARRATION (Groq)
# ============================================
echo ""
echo "🎬 GRUPO 5: NARRATION (Groq)"
echo "------------------------------------"

test_chat "Basic Narration" "narrar una tormenta se acerca al pueblo" "" "Narration"
test_chat "Boss Narration" "narrar moon lord aparece en el cielo" "" "Narration"
test_chat "Game Advice" "dame un consejo para jugar" "" "Narration"

# ============================================
# GRUPO 6: GAME COMMANDS
# ============================================
echo ""
echo "🎮 GRUPO 6: GAME COMMANDS"
echo "------------------------------------"

test_chat "Time" "hora del dia" "" "Commands"
test_chat "Weather" "pon lluvia" "" "Commands"
test_chat "Spawn Boss" "invocar eye of cthulhu" "" "Commands"
test_chat "Broadcast" "broadcast hola a todos" "" "Commands"

# ============================================
# GRUPO 7: EDGE CASES
# ============================================
echo ""
echo "⚠️  GRUPO 7: EDGE CASES"
echo "------------------------------------"

test_chat "Empty Message" "" "" "Edge Cases"
test_chat "Special Characters" "ñáñáñá @#\$%^&*()" "" "Edge Cases"
test_chat "Mixed Language" "how to craft excalibur en español" "Excalibur" "Edge Cases"

# ============================================
# GRUPO 8: COMBINATIONS
# ============================================
echo ""
echo "🔗 GRUPO 8: COMBINATIONS"
echo "------------------------------------"

test_chat "Crafting + Narration" "narrar como Steve forja Excalibur en la Mythril Anvil" "" "Combo"
test_chat "Boss + Context" "invocar moon lord, mi personaje tiene armadura de titanio" "" "Combo"

# ============================================
# SUMMARY
# ============================================
echo ""
echo "================================"
echo "✅ Testing Complete!"
echo "================================"
