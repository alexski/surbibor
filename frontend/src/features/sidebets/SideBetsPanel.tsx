import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import type * as signalR from "@microsoft/signalr";
import { sideBetsApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { GameMember, SideBet } from "../../api/types";
import { useAuthStore } from "../../store/authStore";

interface SideBetsPanelProps {
  gameId: string;
  hub: signalR.HubConnection | null;
  gameActive: boolean;
  members: GameMember[];
  onPointsChanged: () => void;
}

const MIN_STAKE = 5;

function usernameFor(members: GameMember[], userId: string | null | undefined): string {
  if (!userId) return "someone";
  return members.find((m) => m.userId === userId)?.username ?? "someone";
}

function toLocalInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function statusBadge(sideBet: SideBet) {
  if (sideBet.status === "Resolved") {
    return (
      <span className={`badge ${sideBet.outcome ? "badge-confirmed" : "badge-no"}`}>
        {sideBet.outcome ? "Happened" : "Didn't happen"}
      </span>
    );
  }
  if (sideBet.status === "PendingConfirmation") {
    return <span className="badge badge-pending">Awaiting confirmation</span>;
  }
  const bettingClosed = new Date(sideBet.placingClosesAt) <= new Date();
  return <span className="badge badge-open">{bettingClosed ? "Betting closed" : "Open for bets"}</span>;
}

export function SideBetsPanel({ gameId, hub, gameActive, members, onPointsChanged }: SideBetsPanelProps) {
  const [sideBets, setSideBets] = useState<SideBet[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const [newText, setNewText] = useState("");
  const [newClosesAt, setNewClosesAt] = useState(() => toLocalInputValue(new Date(Date.now() + 30 * 60 * 1000)));
  const [newResolvesAt, setNewResolvesAt] = useState(() => toLocalInputValue(new Date(Date.now() + 60 * 60 * 1000)));
  const [creating, setCreating] = useState(false);

  const [wagerPrediction, setWagerPrediction] = useState<Record<string, boolean>>({});
  const [wagerStake, setWagerStake] = useState<Record<string, string>>({});

  const userId = useAuthStore((s) => s.user?.id);
  const myBalance = members.find((m) => m.userId === userId)?.points ?? 0;

  async function loadSideBets() {
    try {
      setSideBets(await sideBetsApi.list(gameId));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load side bets.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadSideBets();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]);

  useEffect(() => {
    if (!hub) return;

    const refresh = () => loadSideBets();
    hub.on("SideBetAdded", refresh);
    hub.on("SideBetWagerPlaced", refresh);
    hub.on("SideBetProposed", refresh);
    hub.on("SideBetResolved", refresh);
    hub.on("SideBetRejected", refresh);

    return () => {
      hub.off("SideBetAdded", refresh);
      hub.off("SideBetWagerPlaced", refresh);
      hub.off("SideBetProposed", refresh);
      hub.off("SideBetResolved", refresh);
      hub.off("SideBetRejected", refresh);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hub]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!newText.trim()) return;
    setCreating(true);
    setError(null);
    try {
      await sideBetsApi.create(
        gameId,
        newText.trim(),
        new Date(newClosesAt).toISOString(),
        new Date(newResolvesAt).toISOString(),
      );
      setNewText("");
      await loadSideBets();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create side bet.");
    } finally {
      setCreating(false);
    }
  }

  async function handlePlaceWager(sideBetId: string) {
    const prediction = wagerPrediction[sideBetId] ?? true;
    const stakeRaw = wagerStake[sideBetId];
    const stake = Number(stakeRaw);

    if (!stakeRaw || Number.isNaN(stake)) {
      setError("Enter a stake amount.");
      return;
    }

    setBusyId(sideBetId);
    setError(null);
    try {
      await sideBetsApi.placeWager(gameId, sideBetId, prediction, stake);
      setWagerStake((prev) => ({ ...prev, [sideBetId]: "" }));
      await loadSideBets();
      onPointsChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to place wager.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleAction(sideBetId: string, action: "propose-yes" | "propose-no" | "confirm" | "reject") {
    setBusyId(sideBetId);
    setError(null);
    try {
      if (action === "propose-yes") await sideBetsApi.propose(gameId, sideBetId, true);
      if (action === "propose-no") await sideBetsApi.propose(gameId, sideBetId, false);
      if (action === "confirm") await sideBetsApi.confirm(gameId, sideBetId);
      if (action === "reject") await sideBetsApi.reject(gameId, sideBetId);
      await loadSideBets();
      if (action === "confirm") onPointsChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Action failed.");
    } finally {
      setBusyId(null);
    }
  }

  if (loading) return <p className="muted">Loading side bets...</p>;

  return (
    <div className="stack">
      {error && <div className="error-banner">{error}</div>}

      <div className="row" style={{ justifyContent: "space-between" }}>
        <span className="muted">Your balance</span>
        <span className="badge badge-active">{myBalance} points</span>
      </div>

      {gameActive && (
        <form onSubmit={handleCreate} className="card stack">
          <div className="field" style={{ margin: 0 }}>
            <label htmlFor="sidebet-text">New side bet</label>
            <input
              id="sidebet-text"
              placeholder="Will the host cry during the finale?"
              value={newText}
              onChange={(e) => setNewText(e.target.value)}
            />
          </div>
          <div className="row">
            <div className="field" style={{ margin: 0, flex: 1 }}>
              <label htmlFor="sidebet-closes">Betting closes</label>
              <input
                id="sidebet-closes"
                type="datetime-local"
                value={newClosesAt}
                onChange={(e) => setNewClosesAt(e.target.value)}
              />
            </div>
            <div className="field" style={{ margin: 0, flex: 1 }}>
              <label htmlFor="sidebet-resolves">Expected to resolve by</label>
              <input
                id="sidebet-resolves"
                type="datetime-local"
                value={newResolvesAt}
                onChange={(e) => setNewResolvesAt(e.target.value)}
              />
            </div>
          </div>
          <button className="btn" type="submit" disabled={creating}>
            {creating ? "Creating..." : "Create side bet"}
          </button>
        </form>
      )}

      <div className="stack">
        {sideBets.length === 0 && <p className="muted">No side bets yet. Create the first one above.</p>}
        {sideBets.map((sb) => {
          const isBusy = busyId === sb.id;
          const isProposer = sb.proposedByUserId === userId;
          const bettingOpen = sb.status === "Open" && new Date(sb.placingClosesAt) > new Date();
          const bettingClosedAwaitingMark = sb.status === "Open" && !bettingOpen;
          const myWager = sb.wagers.find((w) => w.userId === userId);
          const yesTotal = sb.wagers.filter((w) => w.prediction).reduce((sum, w) => sum + w.stake, 0);
          const noTotal = sb.wagers.filter((w) => !w.prediction).reduce((sum, w) => sum + w.stake, 0);

          return (
            <div key={sb.id} className="card stack">
              <div className="row" style={{ justifyContent: "space-between" }}>
                <strong>{sb.text}</strong>
                {statusBadge(sb)}
              </div>

              <div className="row muted" style={{ gap: 20 }}>
                <span>Betting closes: {formatDateTime(sb.placingClosesAt)}</span>
                <span>Expected to resolve: {formatDateTime(sb.expectedResolutionAt)}</span>
              </div>

              <div className="row muted" style={{ gap: 20 }}>
                <span>Yes pool: {yesTotal} pts</span>
                <span>No pool: {noTotal} pts</span>
              </div>

              {sb.wagers.length > 0 && (
                <div className="stack" style={{ gap: 4 }}>
                  {sb.wagers.map((w) => {
                    const won = sb.status === "Resolved" ? w.prediction === sb.outcome : null;
                    return (
                      <div key={w.userId} className="row muted" style={{ justifyContent: "space-between" }}>
                        <span>
                          {usernameFor(members, w.userId)}
                          {w.userId === userId ? " (you)" : ""} bet {w.stake} pts on{" "}
                          <strong>{w.prediction ? "Yes" : "No"}</strong>
                        </span>
                        {won !== null && (
                          <span style={{ color: won ? "var(--success)" : "var(--danger)", fontWeight: 600 }}>
                            {won ? `+${w.stake}` : `-${w.stake}`}
                          </span>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}

              {gameActive && bettingOpen && !myWager && (
                <div className="row" style={{ flexWrap: "wrap" }}>
                  <button
                    type="button"
                    className={`btn btn-sm ${(wagerPrediction[sb.id] ?? true) ? "" : "btn-secondary"}`}
                    onClick={() => setWagerPrediction((prev) => ({ ...prev, [sb.id]: true }))}
                  >
                    Yes, it'll happen
                  </button>
                  <button
                    type="button"
                    className={`btn btn-sm ${wagerPrediction[sb.id] === false ? "" : "btn-secondary"}`}
                    onClick={() => setWagerPrediction((prev) => ({ ...prev, [sb.id]: false }))}
                  >
                    No, it won't
                  </button>
                  <input
                    type="number"
                    placeholder={`${Math.min(MIN_STAKE, myBalance)}-${myBalance} pts`}
                    min={Math.min(MIN_STAKE, myBalance)}
                    max={myBalance}
                    value={wagerStake[sb.id] ?? ""}
                    onChange={(e) => setWagerStake((prev) => ({ ...prev, [sb.id]: e.target.value }))}
                    style={{ width: 140 }}
                  />
                  <button
                    className="btn btn-sm"
                    disabled={isBusy || myBalance <= 0}
                    onClick={() => handlePlaceWager(sb.id)}
                  >
                    {isBusy ? "Placing..." : "Place wager"}
                  </button>
                </div>
              )}

              {gameActive && bettingClosedAwaitingMark && (
                <div className="row">
                  <button
                    className="btn btn-sm"
                    disabled={isBusy}
                    onClick={() => handleAction(sb.id, "propose-yes")}
                  >
                    Mark as happened
                  </button>
                  <button
                    className="btn btn-secondary btn-sm"
                    disabled={isBusy}
                    onClick={() => handleAction(sb.id, "propose-no")}
                  >
                    Mark as didn't happen
                  </button>
                </div>
              )}

              {sb.status === "PendingConfirmation" && (
                <div className="row" style={{ justifyContent: "space-between" }}>
                  <span className="muted">
                    Marked as <strong>{sb.proposedOutcome ? "happened" : "didn't happen"}</strong> by{" "}
                    {usernameFor(members, sb.proposedByUserId)}
                  </span>
                  {gameActive &&
                    (isProposer ? (
                      <span className="muted">Waiting for someone else to agree...</span>
                    ) : (
                      <div className="row">
                        <button className="btn btn-sm" disabled={isBusy} onClick={() => handleAction(sb.id, "confirm")}>
                          Agree
                        </button>
                        <button
                          className="btn btn-secondary btn-sm"
                          disabled={isBusy}
                          onClick={() => handleAction(sb.id, "reject")}
                        >
                          Disagree
                        </button>
                      </div>
                    ))}
                </div>
              )}

              {sb.status === "Resolved" && (
                <span className="muted">
                  Marked by {usernameFor(members, sb.proposedByUserId)}, agreed by{" "}
                  {usernameFor(members, sb.resolvedByUserId)}
                </span>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
