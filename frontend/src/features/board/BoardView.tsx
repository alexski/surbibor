import { useState } from "react";
import { BingoGrid } from "../../components/BingoGrid";
import { boardApi, eventsApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { Board } from "../../api/types";
import { useAuthStore } from "../../store/authStore";

interface BoardViewProps {
  board: Board;
  gameActive: boolean;
  onBoardUpdated: (board: Board) => void;
}

// Squares double as event reporting: a confirmed square marks your board, a plain
// (open) square reports its event as having happened, and a square you personally
// reported (awaiting someone else's confirmation) can be tapped again to undo it —
// all via a single tap, no separate buttons needed.
export function BoardView({ board, gameActive, onBoardUpdated }: BoardViewProps) {
  const [error, setError] = useState<string | null>(null);
  const [busyPosition, setBusyPosition] = useState<number | null>(null);
  const userId = useAuthStore((s) => s.user?.id);

  async function handleSquareClick(position: number) {
    const square = board.squares.find((s) => s.position === position);
    if (!square) return;

    setBusyPosition(position);
    setError(null);
    try {
      if (square.eventStatus === "Confirmed") {
        onBoardUpdated(await boardApi.mark(board.gameId, position));
      } else if (square.eventStatus === "Open" && square.eventId) {
        await eventsApi.propose(board.gameId, square.eventId);
        onBoardUpdated(await boardApi.get(board.gameId));
      } else if (
        square.eventStatus === "PendingConfirmation" &&
        square.eventId &&
        square.eventProposedByUserId === userId
      ) {
        await eventsApi.unpropose(board.gameId, square.eventId);
        onBoardUpdated(await boardApi.get(board.gameId));
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Action failed.");
    } finally {
      setBusyPosition(null);
    }
  }

  const markedCount = board.squares.filter((s) => s.isMarked).length;

  return (
    <div className="stack">
      {error && <div className="error-banner">{error}</div>}
      <p className="muted">
        {markedCount} / 25 marked. Squares highlighted in yellow have a confirmed event — tap them to mark your
        board. Tap any other square to report that its event just happened, or tap it again to undo your report
        while it's still awaiting someone else's confirmation.
      </p>
      <BingoGrid
        squares={board.squares}
        interactive={gameActive}
        onSquareClick={handleSquareClick}
        currentUserId={userId}
        busyPosition={busyPosition}
      />
    </div>
  );
}
