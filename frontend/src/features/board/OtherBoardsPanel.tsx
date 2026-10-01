import { useEffect, useState } from "react";
import type * as signalR from "@microsoft/signalr";
import { boardApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { PlayerBoardProgress } from "../../api/types";
import "./OtherBoardsPanel.css";

const FREE_SPACE_POSITION = 12;
const POSITIONS = Array.from({ length: 25 }, (_, i) => i);

interface OtherBoardsPanelProps {
  gameId: string;
  hub: signalR.HubConnection | null;
}

// A glanceable view of how everyone else is doing: each player's 5x5 grid with only
// marked/unmarked state. The API intentionally returns no events, so there's nothing
// to leak about what's on anyone else's board.
export function OtherBoardsPanel({ gameId, hub }: OtherBoardsPanelProps) {
  const [players, setPlayers] = useState<PlayerBoardProgress[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function loadPlayers() {
    try {
      setPlayers(await boardApi.others(gameId));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load other boards.");
    }
  }

  useEffect(() => {
    loadPlayers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]);

  useEffect(() => {
    if (!hub) return;

    const refresh = () => loadPlayers();
    hub.on("MarkCountUpdated", refresh);
    hub.on("BoardCreated", refresh);

    return () => {
      hub.off("MarkCountUpdated", refresh);
      hub.off("BoardCreated", refresh);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hub]);

  return (
    <aside className="card other-boards">
      <h3 className="other-boards-title">Other players</h3>
      {error && <div className="error-banner">{error}</div>}
      {!players && !error && <p className="muted">Loading...</p>}
      {players?.length === 0 && <p className="muted">No one else has joined yet.</p>}
      <div className="other-boards-list">
        {players?.map((player) => (
          <OtherBoard key={player.userId} player={player} />
        ))}
      </div>
    </aside>
  );
}

function OtherBoard({ player }: { player: PlayerBoardProgress }) {
  const marked = new Set(player.markedPositions);

  return (
    <div className="other-board">
      <div className="other-board-header">
        <span className="other-board-name" title={player.username}>
          {player.username}
        </span>
        {player.hasBoard && <span className="muted other-board-count">{marked.size}/25</span>}
      </div>
      {player.hasBoard ? (
        <div className="mini-grid" role="img" aria-label={`${player.username}: ${marked.size} of 25 squares marked`}>
          {POSITIONS.map((position) => (
            <div
              key={position}
              className={[
                "mini-square",
                position === FREE_SPACE_POSITION ? "is-free" : marked.has(position) ? "is-marked" : "",
              ].join(" ")}
            />
          ))}
        </div>
      ) : (
        <p className="muted other-board-empty">No board yet</p>
      )}
    </div>
  );
}
