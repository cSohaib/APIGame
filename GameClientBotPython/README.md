# Python Game Client Bot

A simple Python client that connects to the API Game websocket server and sends random actions for a player. It mirrors the structure of the provided C# sample: connection logic is handled for you, and you can customize the three bot functions at the bottom of `main.py`.

## Prerequisites
Install the `websockets` package (Python 3.11+ recommended):

```bash
pip install websockets
```

## Run

```bash
python main.py
```

You'll be prompted for a username and team code, then the bot will join `ws://localhost:5291/ws`, react to initialization and state messages, and send an action whenever a state update is received.

## Customize the bot
Edit the functions in the **Bot logic (edit below)** section of `main.py`:
- `handle_initialization(initialization_data)`: react to the map setup the server sends before the match starts.
- `handle_turn(state_data)`: inspect each turn's state and return the `(a, b)` values to send.
- `choose_action(tanks, bullets, explosions)`: implement your own strategy to pick `(a, b)` each turn (called by `handle_turn`).
