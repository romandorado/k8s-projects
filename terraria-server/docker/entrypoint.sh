#!/bin/bash
set -e

WORLD_NAME="${WORLD_NAME:-World}"
WORLD_SIZE="${WORLD_SIZE:-3}"
DIFFICULTY="${DIFFICULTY:-1}"
MAX_PLAYERS="${MAX_PLAYERS:-8}"
PORT="${PORT:-7777}"
AUTOCREATE="${AUTOCREATE:-1}"

# Create world if it doesn't exist
if [ ! -f "${WORLD_NAME}.wld" ]; then
    echo "Creating world: ${WORLD_NAME} (size: ${WORLD_SIZE}, difficulty: ${DIFFICULTY})"
    mono TerrariaServer.dll -configpath /tshock/config -worldpath /tshock/worlds -worldname "${WORLD_NAME}" -autocreate "${WORLD_SIZE}" -difficulty "${DIFFICULTY}" -maxplayers "${MAX_PLAYERS}" -port "${PORT}"
fi

echo "Starting TShock server..."
exec mono TerrariaServer.dll \
    -configpath /tshock/config \
    -worldpath /tshock/worlds \
    -world "${WORLD_NAME}.wld" \
    -maxplayers "${MAX_PLAYERS}" \
    -port "${PORT}"
