import { useEffect } from 'react';
import { useAppDispatch } from '../../app/hooks';
import {
  applyInitialization,
  applyState,
  socketClosed,
  socketConnected,
  socketConnecting,
  socketError,
} from './gameSlice';
import type { BulletState, GridPosition, TankState } from './types';

interface ServerPayload {
  Type?: string;
  Data?: {
    Castles?: Array<{ X?: number; Y?: number }>;
    Rocks?: Array<{ X?: number; Y?: number }>;
    Tanks?: Array<{ Username?: string; X?: number; Y?: number; Base?: number; Head?: number }>;
    Bullets?: Array<{ Id?: string | number; X?: number; Y?: number }>;
    Explosions?: Array<{ X?: number; Y?: number }>;
    InfoText?: string[];
  } | string;
}

const isFiniteNumber = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value);

const safePosition = (x: unknown, y: unknown): GridPosition | null => {
  if (!isFiniteNumber(x) || !isFiniteNumber(y)) {
    return null;
  }

  return { x, y };
};

const getWebSocketUrl = (): string => {
  const configuredUrl = import.meta.env.VITE_WS_URL as string | undefined;
  if (configuredUrl) {
    return configuredUrl;
  }

  const configuredHost = import.meta.env.VITE_WS_HOST as string | undefined;
  const protocol = window.location.protocol === 'https:' ? 'wss' : 'ws';
  const host = configuredHost ?? window.location.host;

  return `${protocol}://${host}/ws`;
};

export const useGameSocket = (): void => {
  const dispatch = useAppDispatch();

  useEffect(() => {
    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;
    let socket: WebSocket | null = null;

    const connect = () => {
      if (disposed) {
        return;
      }

      dispatch(socketConnecting());
      socket = new WebSocket(getWebSocketUrl());

      socket.addEventListener('open', () => {
        dispatch(socketConnected());
        socket?.send(JSON.stringify({ type: 'join', role: 'spectator' }));
      });

      socket.addEventListener('message', (event) => {
        let parsed: ServerPayload;

        try {
          parsed = JSON.parse(event.data) as ServerPayload;
        } catch {
          dispatch(socketError('Invalid JSON from server'));
          return;
        }

        const messageType = parsed.Type;
        const data = parsed.Data;

        if (messageType === 'initialization' && data && typeof data !== 'string') {
          const castles: GridPosition[] = (data.Castles ?? [])
            .map((castle) => safePosition(castle.X, castle.Y))
            .filter((position): position is GridPosition => position !== null);

          const rocks: GridPosition[] = (data.Rocks ?? [])
            .map((rock) => safePosition(rock.X, rock.Y))
            .filter((position): position is GridPosition => position !== null);

          dispatch(applyInitialization({ castles, rocks }));
          return;
        }

        if (messageType === 'state' && data && typeof data !== 'string') {
          const tanks: TankState[] = (data.Tanks ?? [])
            .map((tank) => {
              const position = safePosition(tank.X, tank.Y);
              if (!position || typeof tank.Username !== 'string') {
                return null;
              }

              const base = isFiniteNumber(tank.Base) ? tank.Base : 0;
              const head = isFiniteNumber(tank.Head) ? tank.Head : 0;

              return {
                username: tank.Username,
                x: position.x,
                y: position.y,
                base,
                head,
              };
            })
            .filter((tank): tank is TankState => tank !== null);

          const bullets: BulletState[] = (data.Bullets ?? [])
            .map((bullet) => {
              const position = safePosition(bullet.X, bullet.Y);
              if (!position || (!isFiniteNumber(bullet.Id) && typeof bullet.Id !== 'string')) {
                return null;
              }

              return {
                id: String(bullet.Id),
                x: position.x,
                y: position.y,
              };
            })
            .filter((bullet): bullet is BulletState => bullet !== null);

          const explosions: GridPosition[] = (data.Explosions ?? [])
            .map((explosion) => safePosition(explosion.X, explosion.Y))
            .filter((position): position is GridPosition => position !== null);

          dispatch(
            applyState({
              tanks,
              bullets,
              explosions,
              infoText: Array.isArray(data.InfoText) ? data.InfoText : [],
            }),
          );
          return;
        }

        if (messageType === 'error') {
          dispatch(socketError(typeof data === 'string' ? data : 'Server error'));
        }
      });

      socket.addEventListener('error', () => {
        dispatch(socketError('WebSocket error'));
      });

      socket.addEventListener('close', () => {
        dispatch(socketClosed());

        if (!disposed) {
          retryTimer = setTimeout(connect, 1500);
        }
      });
    };

    connect();

    return () => {
      disposed = true;

      if (retryTimer) {
        clearTimeout(retryTimer);
      }

      if (socket && socket.readyState <= WebSocket.OPEN) {
        socket.close();
      }
    };
  }, [dispatch]);
};
