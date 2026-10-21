import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { NOTIFICATIONS_HUB, type LiveNotification } from '@mastera/api-client';

/**
 * Task 6.3/6.7: live notifications for one salon. The dashboard only depends on this interface, so tests
 * (and the app before sign-in) can use a fake instead of a real SignalR connection.
 */
export interface Realtime {
  /** Starts listening for the salon; returns a function that stops. */
  subscribe(salon: string, onNotification: (notification: LiveNotification) => void): () => void;
}

export const noRealtime: Realtime = { subscribe: () => () => {} };

/** The real connection: SignalR with automatic reconnect, cookie auth (same origin). */
export function createSignalRRealtime(baseUrl = ''): Realtime {
  return {
    subscribe(salon, onNotification) {
      const connection: HubConnection = new HubConnectionBuilder()
        .withUrl(`${baseUrl}${NOTIFICATIONS_HUB}?tenant=${encodeURIComponent(salon)}`, { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();
      connection.on('notification', onNotification);
      const started = connection.start().catch(() => {
        // Offline or signed out: the bell just stays quiet; automatic reconnect covers short drops.
      });
      return () => {
        void started.then(() => connection.stop());
      };
    },
  };
}

/** A hand-driven Realtime for tests: emit() delivers a notification to the current subscriber. */
export function createFakeRealtime() {
  const listeners = new Map<string, (n: LiveNotification) => void>();
  const realtime: Realtime = {
    subscribe(salon, onNotification) {
      listeners.set(salon, onNotification);
      return () => listeners.delete(salon);
    },
  };
  return {
    realtime,
    subscribedSalons: () => [...listeners.keys()],
    emit(salon: string, notification: LiveNotification) {
      listeners.get(salon)?.(notification);
    },
  };
}
