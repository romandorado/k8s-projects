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

# Remove stale config to regenerate
rm -f "$CONFIGPATH/config.json"

# Create proper config.json
cat > "$CONFIGPATH/config.json" << ENDCONF
{
  "Settings": {
    "ServerPassword": "",
    "ServerPort": $PORT,
    "MaxSlots": $MAX_PLAYERS,
    "ServerName": "Terraria Sobrinos",
    "AutoSave": true,
    "DisableLoginBeforeJoin": true,
    "DisableTombstones": true,
    "SpawnProtection": false,
    "RequireLogin": false,
    "AllowLoginAnyUsername": true,
    "StorageType": "sqlite",
    "SqliteDBPath": "/root/.local/share/Terraria/Worlds/tshock.sqlite",
    "RestApiEnabled": true,
    "RestApiPort": 7878,
    "EnableTokenEndpointAuthentication": true,
    "ApplicationRestTokens": {
      "${REST_TOKEN}": {
        "Username": "agent",
        "UserGroupName": "owner"
      }
    },
    "DisableUUIDLogin": false,
    "KickEmptyUUID": false
  }
}
ENDCONF

# Create tshock.sqlite with agent user if it doesn't exist
if [ ! -f "$CONFIGPATH/tshock.sqlite" ]; then
    echo "Creating TShock database..."
    cp /dev/null "$CONFIGPATH/tshock.sqlite"
fi

# Create agent user if not exists
AGENT_DB="$CONFIGPATH/tshock.sqlite"
if [ ! -f "$AGENT_DB" ] || [ "$(sqlite3 "$AGENT_DB" "SELECT COUNT(*) FROM Users WHERE Username='agent';" 2>/dev/null)" != "1" ]; then
    echo "Creating agent user..."
    # Wait for TShock to create the DB schema
    sleep 2
    HASH=$(python3 -c "import bcrypt; print(bcrypt.hashpw(b'agent1234', bcrypt.gensalt()).decode())")
    sqlite3 "$AGENT_DB" "INSERT OR REPLACE INTO Users (Username, Password, UUID, Usergroup, Registered, LastAccessed, KnownIPs) VALUES ('agent', '$HASH', '', 'owner', datetime('now'), datetime('now'), '');" 2>/dev/null && echo "Agent user created." || echo "Will create after server starts."
fi

echo "Starting TShock 6.0.0 for Terraria 1.4.5.5..."
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
