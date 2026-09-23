import { useEffect, useState } from "react";
import type * as signalR from "@microsoft/signalr";
import { boardApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { Board } from "../../api/types";
import { BoardSetupPanel } from "./BoardSetupPanel";
import { BoardView } from "./BoardView";

interface BoardPanelProps {
  gameId: string;
  hub: signalR.HubConnection | null;
  gameActive: boolean;
}

export function BoardPanel({ gameId, hub, gameActive }: BoardPanelProps) {
  const [board, setBoard] = useState<Board | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  async function loadBoard() {
    try {
      setBoard(await boardApi.get(gameId));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setBoard(null);
      } else {
        setError(err instanceof ApiError ? err.message : "Failed to load board.");
      }
    }
  }

  useEffect(() => {
    loadBoard();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]);

  useEffect(() => {
    if (!hub) return;

    // An event on this board may have changed status — refresh so the square updates immediately.
    const refresh = () => loadBoard();
    hub.on("EventConfirmed", refresh);
    hub.on("EventProposed", refresh);
    hub.on("EventUnproposed", refresh);
    hub.on("EventRejected", refresh);

    return () => {
      hub.off("EventConfirmed", refresh);
      hub.off("EventProposed", refresh);
      hub.off("EventUnproposed", refresh);
      hub.off("EventRejected", refresh);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hub]);

  if (board === undefined) {
    return <p className="muted">Loading board...</p>;
  }

  if (error) {
    return <div className="error-banner">{error}</div>;
  }

  if (board === null) {
    if (!gameActive) {
      return <p className="muted">This game has ended and you never created a board.</p>;
    }
    return <BoardSetupPanel gameId={gameId} onBoardCreated={setBoard} />;
  }

  return <BoardView board={board} gameActive={gameActive} onBoardUpdated={setBoard} />;
}
