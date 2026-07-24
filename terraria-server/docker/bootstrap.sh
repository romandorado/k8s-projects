#!/bin/bash

WORLD_NAME="${WORLD_NAME:-World}"
WORLD_SIZE="${WORLD_SIZE:-3}"
DIFFICULTY="${DIFFICULTY:-1}"
MAX_PLAYERS="${MAX_PLAYERS:-8}"
PORT="${PORT:-7777}"
REST_TOKEN="${REST_TOKEN:-terraria-agent-secret-token-2024}"
CONFIGPATH="${CONFIGPATH:-/config}"
LOGPATH="${LOGPATH:-/tshock/logs}"

mkdir -p "$CONFIGPATH" "$LOGPATH" /root/.local/share/Terraria/Worlds

# Copy config from template if not exists (ConfigMap provides base config)
if [ ! -f "$CONFIGPATH/config.json" ] && [ -f "/config-template/config.json" ]; then
    echo "Copying config from template..."
    cp /config-template/config.json "$CONFIGPATH/config.json"
fi

WORLD_DIR="/root/.local/share/Terraria/Worlds"
WORLD_PATH="${WORLD_DIR}/${WORLD_NAME}.wld"

# Clean any incompatible world files
for f in "$WORLD_DIR"/*.wld; do
    [ -e "$f" ] || continue
    bn=$(basename "$f" .wld)
    if [ "$bn" != "$WORLD_NAME" ]; then
        echo "Removing old world: $f"
        rm -f "$f" "${f}.meta" "${f}.bak"
    fi
done

# Create tshock.sqlite with agent user if it doesn't exist
AGENT_DB="/root/.local/share/Terraria/Worlds/tshock.sqlite"
mkdir -p "$(dirname "$AGENT_DB")"
if [ ! -f "$AGENT_DB" ]; then
    echo "Creating TShock database..."
    cp /dev/null "$AGENT_DB"
fi

# Create agent user if not exists
if [ ! -f "$AGENT_DB" ] || [ "$(sqlite3 "$AGENT_DB" "SELECT COUNT(*) FROM Users WHERE Username='agent';" 2>/dev/null)" != "1" ]; then
    echo "Creating agent user..."
    HASH=$(python3 -c "import bcrypt; print(bcrypt.hashpw(b'agent1234', bcrypt.gensalt()).decode())" 2>/dev/null || echo '$2b$12$10nrqQ0pwTBheugORnfd6.i5grPxMH.LkqCkW9EjL/UJKDXGjHrTC')
    sqlite3 "$AGENT_DB" "INSERT OR REPLACE INTO Users (Username, Password, UUID, Usergroup, Registered, LastAccessed, KnownIPs) VALUES ('agent', '$HASH', '', 'admin', datetime('now'), datetime('now'), '');" 2>/dev/null && echo "Agent user created." || echo "Will create after server starts."
fi

echo "Starting TShock 6.1.0 for Terraria 1.4.5.6..."
echo "World: $WORLD_NAME, Size: $WORLD_SIZE, Difficulty: $DIFFICULTY"

exec ./TShock.Server \
    -configpath "$CONFIGPATH" \
    -logpath "$LOGPATH" \
    -world "$WORLD_PATH" \
    -worldname "$WORLD_NAME" \
    -autocreate "$WORLD_SIZE" \
    -difficulty "$DIFFICULTY" \
    -maxplayers "$MAX_PLAYERS" \
    -port "$PORT"
