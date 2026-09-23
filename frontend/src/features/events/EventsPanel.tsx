import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import type * as signalR from "@microsoft/signalr";
import { boardApi, eventsApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { EventItem } from "../../api/types";
import { useAuthStore } from "../../store/authStore";

interface EventsPanelProps {
  gameId: string;
  hub: signalR.HubConnection | null;
  gameActive: boolean;
}

function badgeForStatus(status: EventItem["status"]) {
  switch (status) {
    case "Open":
      return <span className="badge badge-open">Open</span>;
    case "PendingConfirmation":
      return <span className="badge badge-pending">Pending confirmation</span>;
    case "Confirmed":
      return <span className="badge badge-confirmed">Confirmed</span>;
  }
}

function normalizeWords(text: string): string[] {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, " ")
    .split(/\s+/)
    .filter(Boolean);
}

// Cheap word-overlap + substring scoring — good enough to surface near-duplicate
// event text without pulling in a fuzzy-search dependency.
function similarityScore(query: string, target: string): number {
  const queryWords = normalizeWords(query);
  if (queryWords.length === 0) return 0;

  const targetNormalized = target.toLowerCase();
  const targetWords = new Set(normalizeWords(target));

  const matchedWords = queryWords.filter((w) => targetWords.has(w) || targetNormalized.includes(w)).length;
  let score = matchedWords / queryWords.length;

  if (targetNormalized.includes(query.trim().toLowerCase())) {
    score += 0.5;
  }

  return score;
}

export function EventsPanel({ gameId, hub, gameActive }: EventsPanelProps) {
  const [events, setEvents] = useState<EventItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [newEventText, setNewEventText] = useState("");
  const [adding, setAdding] = useState(false);
  const [busyEventId, setBusyEventId] = useState<string | null>(null);
  const [hasBoard, setHasBoard] = useState(false);
  const userId = useAuthStore((s) => s.user?.id);

  const similarEvents = useMemo(() => {
    const query = newEventText.trim();
    if (query.length < 2) return [];

    return events
      .map((evt) => ({ evt, score: similarityScore(query, evt.text) }))
      .filter(({ score }) => score >= 0.4)
      .sort((a, b) => b.score - a.score)
      .slice(0, 5)
      .map(({ evt }) => evt);
  }, [newEventText, events]);

  async function loadEvents() {
    try {
      setEvents(await eventsApi.list(gameId));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load events.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadEvents();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]);

  useEffect(() => {
    let cancelled = false;
    // A built board unlocks quick "This happened" actions straight from the
    // similar-events suggestions; before that, players are still picking events
    // for their board, so we only show status there.
    boardApi
      .get(gameId)
      .then(() => {
        if (!cancelled) setHasBoard(true);
      })
      .catch(() => {
        if (!cancelled) setHasBoard(false);
      });
    return () => {
      cancelled = true;
    };
  }, [gameId]);

  useEffect(() => {
    if (!hub) return;

    const refresh = () => loadEvents();
    hub.on("EventAdded", refresh);
    hub.on("EventProposed", refresh);
    hub.on("EventConfirmed", refresh);
    hub.on("EventUnproposed", refresh);
    hub.on("EventRejected", refresh);

    return () => {
      hub.off("EventAdded", refresh);
      hub.off("EventProposed", refresh);
      hub.off("EventConfirmed", refresh);
      hub.off("EventUnproposed", refresh);
      hub.off("EventRejected", refresh);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hub]);

  async function handleAdd(e: FormEvent) {
    e.preventDefault();
    if (!newEventText.trim()) return;
    setAdding(true);
    setError(null);
    try {
      await eventsApi.add(gameId, newEventText.trim());
      setNewEventText("");
      await loadEvents();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to add event.");
    } finally {
      setAdding(false);
    }
  }

  async function handleAction(eventId: string, action: "propose" | "unpropose" | "confirm" | "reject") {
    setBusyEventId(eventId);
    setError(null);
    try {
      if (action === "propose") await eventsApi.propose(gameId, eventId);
      if (action === "unpropose") await eventsApi.unpropose(gameId, eventId);
      if (action === "confirm") await eventsApi.confirm(gameId, eventId);
      if (action === "reject") await eventsApi.reject(gameId, eventId);
      await loadEvents();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Action failed.");
    } finally {
      setBusyEventId(null);
    }
  }

  if (loading) return <p className="muted">Loading events...</p>;

  return (
    <div className="stack">
      {error && <div className="error-banner">{error}</div>}

      {gameActive && (
        <div className="stack" style={{ gap: 6 }}>
          <form onSubmit={handleAdd} className="row">
            <input
              placeholder="Add a new possible event..."
              value={newEventText}
              onChange={(e) => setNewEventText(e.target.value)}
              style={{ flex: 1 }}
            />
            <button className="btn" type="submit" disabled={adding}>
              {adding ? "Adding..." : "Add event"}
            </button>
          </form>

          {similarEvents.length > 0 && (
            <div className="stack similar-stack" style={{ gap: 6 }}>
              {similarEvents.map((evt) => {
                const isBusy = busyEventId === evt.id;
                const showThisHappened = hasBoard && evt.status === "Open";

                return (
                  <div key={evt.id} className="event-row">
                    <span onClick={() => setNewEventText(evt.text)} style={{ cursor: "pointer" }}>
                      {evt.text}
                    </span>
                    {showThisHappened ? (
                      <button
                        className="btn btn-secondary btn-sm"
                        disabled={isBusy}
                        onClick={() => handleAction(evt.id, "propose")}
                      >
                        This happened
                      </button>
                    ) : (
                      badgeForStatus(evt.status)
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      <div className="stack">
        {events.length === 0 && <p className="muted">No events yet. Add the first one above.</p>}
        {events.map((evt) => {
          const isBusy = busyEventId === evt.id;
          const isProposer = evt.proposedByUserId === userId;

          return (
            <div key={evt.id} className="event-row">
              <div>
                <div>{evt.text}</div>
                <div style={{ marginTop: 4 }}>{badgeForStatus(evt.status)}</div>
              </div>
              {gameActive && (
                <div className="row">
                  {evt.status === "Open" && (
                    <button
                      className="btn btn-secondary btn-sm"
                      disabled={isBusy}
                      onClick={() => handleAction(evt.id, "propose")}
                    >
                      This happened
                    </button>
                  )}
                  {evt.status === "PendingConfirmation" &&
                    (isProposer ? (
                      <>
                        <span className="muted">Waiting for someone else to confirm...</span>
                        <button
                          className="btn btn-secondary btn-sm"
                          disabled={isBusy}
                          onClick={() => handleAction(evt.id, "unpropose")}
                        >
                          Undo
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          className="btn btn-sm"
                          disabled={isBusy}
                          onClick={() => handleAction(evt.id, "confirm")}
                        >
                          Confirm
                        </button>
                        <button
                          className="btn btn-secondary btn-sm"
                          disabled={isBusy}
                          onClick={() => handleAction(evt.id, "reject")}
                        >
                          Reject
                        </button>
                      </>
                    ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
