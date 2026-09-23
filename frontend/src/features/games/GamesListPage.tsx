import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { gamesApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { GameSummary } from "../../api/types";

export function GamesListPage() {
  const [games, setGames] = useState<GameSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [creating, setCreating] = useState(false);

  const [inviteCode, setInviteCode] = useState("");
  const [joining, setJoining] = useState(false);

  const navigate = useNavigate();

  async function loadGames() {
    setLoading(true);
    try {
      setGames(await gamesApi.list());
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load games.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadGames();
  }, []);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setCreating(true);
    setError(null);
    try {
      const game = await gamesApi.create(name, description);
      navigate(`/games/${game.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create game.");
    } finally {
      setCreating(false);
    }
  }

  async function handleJoin(e: FormEvent) {
    e.preventDefault();
    setJoining(true);
    setError(null);
    try {
      const game = await gamesApi.join(inviteCode.trim());
      navigate(`/games/${game.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to join game.");
    } finally {
      setJoining(false);
    }
  }

  return (
    <div className="page stack">
      <h1 className="brand-font">Your games</h1>
      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <p className="muted">Loading...</p>
      ) : games.length === 0 ? (
        <p className="muted">You haven't joined any games yet. Create one or join with an invite code below.</p>
      ) : (
        <div className="stack">
          {games.map((game) => (
            <Link key={game.id} to={`/games/${game.id}`} className="game-list-item">
              <div>
                <strong>{game.name}</strong>
                {game.description && <div className="muted">{game.description}</div>}
              </div>
              <span className={`badge ${game.status === "Active" ? "badge-active brand-font" : "badge-completed brand-font"}`}>
                {game.status}
              </span>
            </Link>
          ))}
        </div>
      )}

      <div className="row" style={{ alignItems: "stretch" }}>
        <div className="card stack" style={{ flex: 1, minWidth: 260 }}>
          <h2 className="brand-font" style={{ marginTop: 0 }}>Create a game</h2>
          <form onSubmit={handleCreate} className="stack">
            <div className="field">
              <label htmlFor="name">Game name</label>
              <input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="description">Description (optional)</label>
              <input id="description" value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <button className="btn" type="submit" disabled={creating}>
              {creating ? "Creating..." : "Create game"}
            </button>
          </form>
        </div>

        <div className="card stack" style={{ flex: 1, minWidth: 260 }}>
          <h2 className="brand-font" style={{ marginTop: 0 }}>Join a game</h2>
          <form onSubmit={handleJoin} className="stack">
            <div className="field">
              <label htmlFor="inviteCode">Invite code</label>
              <input
                id="inviteCode"
                required
                value={inviteCode}
                onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                placeholder="e.g. X53RZ5"
              />
            </div>
            <button className="btn" type="submit" disabled={joining}>
              {joining ? "Joining..." : "Join game"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
