# APIGame Tanks Arena

APIGame is a turn-based tanks duel where players connect bots to a WebSocket API and battle on a 24x16 grid. The browser client built with Phaser renders the action as a spectator, while all gameplay logic runs on the .NET server.

## Gameplay overview
- Two teams (red and blue) defend their castles while trying to destroy the enemy castle.
- Tanks have independent base and turret directions (0: up, 1: right, 2: down, 3: left).
- Each turn lasts 500 ms. The server broadcasts the full game state (tanks, bullets, explosions) and receives player actions.
- The grid also contains static rocks that block movement and shots.

## Bot actions
Each bot sends an action with two integers every turn:
- `a` controls the **base**: `0` do nothing, `1` rotate clockwise, `2` rotate counter-clockwise, `3` move forward one tile.
- `b` controls the **head**: `0` do nothing, `1` rotate clockwise, `2` rotate counter-clockwise, `3` shoot in the current head direction.

The server responds with snapshots containing tank positions, headings, scores, castles, rocks, and active bullets so bots can plan their next move.

## Repository layout
- `APIGame/` – ASP.NET Core server that drives the simulation and WebSocket API.
- `GameClientBot/`, `GameClientBotJS/`, `GameClientBotPython/` – reference bots showing how to connect and submit actions in C#, JavaScript, and Python.
- `wwwroot/` inside `APIGame/` – Phaser spectator client assets.

## Running the server
1. Navigate to the API project:
   ```bash
   cd APIGame
   ```
2. Start the server:
   ```bash
   dotnet run
   ```
3. Open the Phaser spectator UI at the URL printed in the console, or connect a bot to the exposed WebSocket endpoint.

The server seeds castles and rocks, spawns connecting tanks on their team’s edge, and advances the simulation every half second.

## Developing bots
1. Copy one of the sample bot folders and install its dependencies (for example, `npm install` in `GameClientBotJS`).
2. Point the bot to the running server WebSocket URL.
3. Replace the random-decision logic with your strategy and send actions each turn based on the incoming snapshots.

Happy tank building!
