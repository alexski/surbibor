import type { BoardSquare } from "../api/types";
import "./BingoGrid.css";

interface BingoGridProps {
  squares: BoardSquare[];
  onSquareClick?: (position: number) => void;
  interactive?: boolean;
  currentUserId?: string;
  busyPosition?: number | null;
}

export function BingoGrid({
  squares,
  onSquareClick,
  interactive = true,
  currentUserId,
  busyPosition = null,
}: BingoGridProps) {
  const byPosition = [...squares].sort((a, b) => a.position - b.position);

  return (
    <div className="bingo-grid">
      {byPosition.map((square) => {
        const canMark = interactive && !square.isFree && !square.isMarked && square.eventStatus === "Confirmed";
        const canPropose = interactive && !square.isFree && !square.isMarked && square.eventStatus === "Open";
        const canUndo =
          interactive &&
          !square.isFree &&
          !square.isMarked &&
          square.eventStatus === "PendingConfirmation" &&
          square.eventProposedByUserId === currentUserId;
        // Only the square actually being acted on is disabled while its request is in flight —
        // gating this on a grid-wide flag instead made every OTHER square's badge flicker too,
        // shifting their centered content and making the whole board appear to bounce.
        const isBusy = square.position === busyPosition;
        const canClick = (canMark || canPropose || canUndo) && !isBusy;

        const classNames = [
          "bingo-square",
          square.isFree ? "is-free" : "",
          square.isMarked ? "is-marked" : "",
          !square.isMarked && square.eventStatus === "Confirmed" ? "is-confirmed" : "",
          !square.isMarked && square.eventStatus === "PendingConfirmation" ? "is-pending" : "",
          canMark || canPropose || canUndo ? "is-clickable" : "",
        ]
          .filter(Boolean)
          .join(" ");

        return (
          <button
            key={square.position}
            type="button"
            className={classNames}
            disabled={!canClick}
            onClick={() => canClick && onSquareClick?.(square.position)}
            title={square.isFree ? "Free space" : (square.eventText ?? undefined)}
          >
            {square.isFree ? (
              <span className="bingo-square-free">Sole Survivor Will Win</span>
            ) : (
              <span className="bingo-square-text">{square.eventText}</span>
            )}
            {!square.isFree && square.eventStatus === "PendingConfirmation" && !canUndo && (
              <span className="bingo-square-badge">awaiting confirmation</span>
            )}
            {canMark && <span className="bingo-square-badge bingo-square-badge-confirmed">tap to mark</span>}
            {canPropose && <span className="bingo-square-badge">tap: this happened</span>}
            {canUndo && <span className="bingo-square-badge">tap to undo</span>}
          </button>
        );
      })}
    </div>
  );
}
