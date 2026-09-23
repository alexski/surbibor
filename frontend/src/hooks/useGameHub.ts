import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { getApiBaseUrl } from "../api/client";
import { useAuthStore } from "../store/authStore";

/**
 * Opens a SignalR connection to the game hub and joins the given game's group.
 * Returns the live HubConnection (or null until connected) so callers can
 * attach their own `.on(...)` handlers in a useEffect.
 */
export function useGameHub(gameId: string | undefined) {
  const token = useAuthStore((s) => s.token);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (!gameId || !token) {
      return;
    }

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${getApiBaseUrl()}/hubs/game`, {
        accessTokenFactory: () => useAuthStore.getState().token ?? "",
      })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = conn;
    let cancelled = false;

    conn
      .start()
      .then(() => conn.invoke("JoinGame", gameId))
      .then(() => {
        if (!cancelled) {
          setConnection(conn);
        }
      })
      .catch((err) => {
        console.error("Failed to connect to game hub", err);
      });

    conn.onreconnected(() => {
      conn.invoke("JoinGame", gameId).catch((err) => console.error("Failed to rejoin game group", err));
    });

    return () => {
      cancelled = true;
      setConnection(null);
      conn.stop().catch(() => undefined);
    };
  }, [gameId, token]);

  return connection;
}
