import asyncio
import json
import random
from typing import Any, Iterable, Tuple

import websockets

WS_ADDRESS = "ws://localhost:5291/ws"


# ---------- Connection helpers (no need to edit) ----------
async def send_string(ws: websockets.WebSocketClientProtocol, message: str) -> None:
    await ws.send(message)


async def send_join(ws: websockets.WebSocketClientProtocol, username: str, team: str) -> None:
    payload = {"type": "join", "role": "player", "username": username, "team": team}
    await send_string(ws, json.dumps(payload))


async def send_action(
    ws: websockets.WebSocketClientProtocol, username: str, a_value: int, b_value: int
) -> None:
    if ws.closed:
        return

    action = {"type": "action", "a": a_value, "b": b_value}
    await send_string(ws, json.dumps(action))
    print(f"{username} sent action: ({a_value}, {b_value})")


async def receive_loop(ws: websockets.WebSocketClientProtocol, username: str) -> None:
    async for raw in ws:
        try:
            message = json.loads(raw)
        except json.JSONDecodeError:
            print(f"Received raw message: {raw}")
            continue

        msg_type = message.get("Type")
        data = message.get("Data", {}) if isinstance(message, dict) else {}

        if msg_type == "initialization":
            handle_initialization(data)
        elif msg_type == "state":
            a_value, b_value = handle_turn(data)
            await send_action(ws, username, a_value, b_value)
        elif msg_type == "error":
            print(f"Error from server: {data.get('Message') or data}")
        else:
            print(f"Received message: {message}")


async def main() -> None:
    print("Welcome to the API Game websocket client.")
    username = input("Enter username: ") or "player"
    team = input("Enter team code: ") or ""

    try:
        async with websockets.connect(WS_ADDRESS) as ws:
            await send_join(ws, username, team)
            await receive_loop(ws, username)
    except Exception as exc:  # noqa: BLE001
        print(f"Failed to connect to server: {exc}")


# ---------- Bot logic (edit below) ----------
def handle_initialization(initialization_data: dict[str, Any]) -> None:
    """React to the initial map data when the match starts."""

    castles = initialization_data.get("Castles", [])
    rocks = initialization_data.get("Rocks", [])
    print(f"Received castles: {len(castles)}, rocks: {len(rocks)}")


def handle_turn(state_data: dict[str, Any]) -> Tuple[int, int]:
    """Process each turn's state and decide the action to send back."""

    tanks = state_data.get("Tanks", [])
    bullets = state_data.get("Bullets", [])
    explosions = state_data.get("Explosions", [])

    print(
        f"Tanks: {len(tanks)}, bullets: {len(bullets)}, explosions: {len(explosions)}"
    )

    return choose_action(tanks, bullets, explosions)


def choose_action(
    tanks: Iterable[Any], bullets: Iterable[Any], explosions: Iterable[Any]
) -> Tuple[int, int]:
    """
    Decide what to send for (a, b) this turn.

    The default mirrors the C# sample: random choices with a higher weight on 3.
    Update this function with your own strategy.
    """

    options = [0, 1, 2, 3, 3, 3, 3]
    a_value = random.choice(options)
    b_value = random.choice(options)
    return a_value, b_value


if __name__ == "__main__":
    asyncio.run(main())
