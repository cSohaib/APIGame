import { useMemo, useRef, type CSSProperties } from 'react';
import { useAppSelector } from '../app/hooks';
import {
  ANIMATION_DURATION_MS,
  FRAMES,
  GRID_COLUMNS,
  GRID_ROWS,
  SPRITE_SHEET_COLUMNS,
  SPRITE_SHEET_URL,
  TILE_SIZE,
} from '../features/game/constants';
import type { TankState } from '../features/game/types';

interface TankRenderState extends TankState {
  baseAngle: number;
  headAngle: number;
}

const shortestAngleDelta = (from: number, to: number): number => {
  return ((to - from + 540) % 360) - 180;
};

const normalizeDirection = (direction: number): number => {
  const normalized = direction % 4;
  return normalized >= 0 ? normalized : normalized + 4;
};

const getFrameStyle = (frame: number): CSSProperties => {
  const column = frame % SPRITE_SHEET_COLUMNS;
  const row = Math.floor(frame / SPRITE_SHEET_COLUMNS);

  return {
    width: TILE_SIZE,
    height: TILE_SIZE,
    backgroundImage: `url(${SPRITE_SHEET_URL})`,
    backgroundPosition: `-${column * TILE_SIZE}px -${row * TILE_SIZE}px`,
    backgroundRepeat: 'no-repeat',
    imageRendering: 'pixelated',
  };
};

const groundFrameByParity = (x: number, y: number): number => {
  if (y % 2 === 0) {
    return x % 2 === 0 ? FRAMES.groundTopLeft : FRAMES.groundTopRight;
  }

  return x % 2 === 0 ? FRAMES.groundBottomLeft : FRAMES.groundBottomRight;
};

const useInterpolatedTanks = (tanks: TankState[]): TankRenderState[] => {
  const previousByName = useRef<Map<string, { baseAngle: number; headAngle: number }>>(new Map());

  return useMemo(() => {
    const nextByName = new Map<string, { baseAngle: number; headAngle: number }>();

    const interpolated = tanks.map((tank) => {
      const previous = previousByName.current.get(tank.username);
      const targetBase = normalizeDirection(tank.base) * 90;
      const targetHead = normalizeDirection(tank.head) * 90;

      if (!previous) {
        nextByName.set(tank.username, { baseAngle: targetBase, headAngle: targetHead });
        return { ...tank, baseAngle: targetBase, headAngle: targetHead };
      }

      const baseAngle = previous.baseAngle + shortestAngleDelta(previous.baseAngle, targetBase);
      const headAngle = previous.headAngle + shortestAngleDelta(previous.headAngle, targetHead);

      nextByName.set(tank.username, { baseAngle, headAngle });
      return { ...tank, baseAngle, headAngle };
    });

    previousByName.current = nextByName;
    return interpolated;
  }, [tanks]);
};

const boardWidth = GRID_COLUMNS * TILE_SIZE;
const boardHeight = GRID_ROWS * TILE_SIZE;
const castleFrames = [
  { frame: FRAMES.castleTopLeft, x: 0, y: 0 },
  { frame: FRAMES.castleTopRight, x: 1, y: 0 },
  { frame: FRAMES.castleBottomLeft, x: 0, y: 1 },
  { frame: FRAMES.castleBottomRight, x: 1, y: 1 },
];

export const GameView = () => {
  const { connectionStatus, connectionMessage, castles, rocks, tanks, bullets, explosions, infoText } = useAppSelector(
    (state) => state.game,
  );

  const interpolatedTanks = useInterpolatedTanks(tanks);

  return (
    <div className="game-layout">
      <div className="board-shell">
        <div className="game-board" style={{ width: boardWidth, height: boardHeight }}>
          {Array.from({ length: GRID_ROWS }).map((_, row) =>
            Array.from({ length: GRID_COLUMNS }).map((__, column) => {
              const frame = groundFrameByParity(column, row);

              return (
                <div
                  key={`ground-${column}-${row}`}
                  className="tile"
                  style={{
                    ...getFrameStyle(frame),
                    left: column * TILE_SIZE,
                    top: row * TILE_SIZE,
                  }}
                />
              );
            }),
          )}

          {castles.map((castle, index) => (
            <div
              key={`castle-${index}-${castle.x}-${castle.y}`}
              className="castle"
              style={{
                left: castle.x * TILE_SIZE,
                top: castle.y * TILE_SIZE,
              }}
            >
              {castleFrames.map((item) => (
                <div
                  key={`castle-frame-${index}-${item.frame}`}
                  className="tile"
                  style={{
                    ...getFrameStyle(item.frame),
                    left: item.x * TILE_SIZE,
                    top: item.y * TILE_SIZE,
                  }}
                />
              ))}
            </div>
          ))}

          {rocks.map((rock, index) => (
            <div
              key={`rock-${index}-${rock.x}-${rock.y}`}
              className="tile"
              style={{
                ...getFrameStyle(FRAMES.rock),
                left: rock.x * TILE_SIZE,
                top: rock.y * TILE_SIZE,
              }}
            />
          ))}

          {interpolatedTanks.map((tank) => (
            <div
              key={tank.username}
              className="tank"
              style={{
                left: tank.x * TILE_SIZE + TILE_SIZE / 2,
                top: tank.y * TILE_SIZE + TILE_SIZE / 2,
                transitionDuration: `${ANIMATION_DURATION_MS}ms`,
              }}
            >
              <div
                className="tank-layer"
                style={{
                  ...getFrameStyle(FRAMES.tankBase),
                  transform: `translate(-50%, -50%) rotate(${tank.baseAngle}deg)`,
                  transitionDuration: `${ANIMATION_DURATION_MS}ms`,
                }}
              />
              <div
                className="tank-layer"
                style={{
                  ...getFrameStyle(FRAMES.tankHead),
                  transform: `translate(-50%, -50%) rotate(${tank.headAngle}deg)`,
                  transitionDuration: `${ANIMATION_DURATION_MS}ms`,
                }}
              />
              <span className="tank-label">{tank.username}</span>
            </div>
          ))}

          {bullets.map((bullet) => (
            <div
              key={bullet.id}
              className="bullet"
              style={{
                left: bullet.x * TILE_SIZE + TILE_SIZE / 2,
                top: bullet.y * TILE_SIZE + TILE_SIZE / 2,
                transitionDuration: `${ANIMATION_DURATION_MS}ms`,
              }}
            />
          ))}

          {explosions.map((explosion) => (
            <div
              key={explosion.id}
              className="explosion"
              style={{
                left: explosion.x * TILE_SIZE,
                top: explosion.y * TILE_SIZE,
              }}
            />
          ))}
        </div>
      </div>

      <aside className="info-panel">
        <h1>API Game</h1>
        <p className={`connection connection-${connectionStatus}`}>{connectionMessage}</p>
        <p>Grid: 24 x 16</p>
        <p>Tile size: 64px</p>
        {infoText.length > 0 ? (
          <pre>{infoText.join('\n')}</pre>
        ) : (
          <p className="muted">Waiting for game state updates...</p>
        )}
      </aside>
    </div>
  );
};
