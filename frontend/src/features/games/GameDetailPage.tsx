import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { gamesApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { GameDetail } from "../../api/types";
import { useGameHub } from "../../hooks/useGameHub";
import { useAuthStore } from "../../store/authStore";
import { EventsPanel } from "../events/EventsPanel";
import { BoardPanel } from "../board/BoardPanel";
import { SideBetsPanel } from "../sidebets/SideBetsPanel";

export function GameDetailPage() {
  const { gameId } = useParams<{ gameId: string }>();
  const [game, setGame] = useState<GameDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<"events" | "board" | "bets">("board");
  const hub = useGameHub(gameId);
  const userId = useAuthStore((s) => s.user?.id);

  async function loadGame() {
    if (!gameId) return;
    try {
      setGame(await gamesApi.get(gameId));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load game.");
    }
  }

  useEffect(() => {
    loadGame();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]);

  useEffect(() => {
    if (!hub) return;
    // Points live on game.members, so refresh whenever an action could have changed them.
    const refresh = () => loadGame();
    hub.on("GameWon", refresh);
    hub.on("MarkCountUpdated", refresh);
    hub.on("SideBetResolved", refresh);
    return () => {
      hub.off("GameWon", refresh);
      hub.off("MarkCountUpdated", refresh);
      hub.off("SideBetResolved", refresh);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hub]);

  if (error) return <div className="page error-banner">{error}</div>;
  if (!game || !gameId) return <div className="page muted">Loading...</div>;

  const winner = game.members.find((m) => m.userId === game.winnerId);
  const gameActive = game.status === "Active";
  const myMembership = game.members.find((m) => m.userId === userId);

  return (
    <div className="page stack">
      <div>
        <h1 className="brand-font" style={{ marginBottom: 4 }}>{game.name}</h1>
        {game.description && <p className="muted" style={{ marginTop: 0 }}>{game.description}</p>}
        <div className="row">
          <span className={`badge ${gameActive ? "badge-active" : "badge-completed"}`}>{game.status}</span>
          <span className="muted">Invite code: {game.inviteCode}</span>
          <span className="muted">{game.members.length} players</span>
          {myMembership && <span className="muted">Your points: {myMembership.points}</span>}
        </div>
      </div>

      {!gameActive && (
        <div className="win-banner">
          🎉 {winner ? winner.username : "Someone"} got BINGO! This game has ended.
        </div>
      )}

      <div className="tabs">
        <button className={`tab ${tab === "board" ? "active" : ""}`} onClick={() => setTab("board")}>
          My Board
        </button>
        <button className={`tab ${tab === "events" ? "active" : ""}`} onClick={() => setTab("events")}>
          Events
        </button>
        <button className={`tab ${tab === "bets" ? "active" : ""}`} onClick={() => setTab("bets")}>
          Side Bets
        </button>
      </div>

      {tab === "board" && <BoardPanel gameId={gameId} hub={hub} gameActive={gameActive} />}
      {tab === "events" && <EventsPanel gameId={gameId} hub={hub} gameActive={gameActive} />}
      {tab === "bets" && (
        <SideBetsPanel
          gameId={gameId}
          hub={hub}
          gameActive={gameActive}
          members={game.members}
          onPointsChanged={loadGame}
        />
      )}
    </div>
  );
}
