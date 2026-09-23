import { useEffect, useMemo, useState } from "react";
import { boardApi, eventsApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import type { Board, EventItem } from "../../api/types";
import "../../components/BingoGrid.css";

const REQUIRED_EVENT_COUNT = 24;
const FREE_POSITION = 12;

interface BoardSetupPanelProps {
  gameId: string;
  onBoardCreated: (board: Board) => void;
}

export function BoardSetupPanel({ gameId, onBoardCreated }: BoardSetupPanelProps) {
  const [events, setEvents] = useState<EventItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [mode, setMode] = useState<"select" | "manual">("select");
  const [placements, setPlacements] = useState<Record<number, string>>({});
  const [armedEventId, setArmedEventId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    eventsApi
      .list(gameId)
      .then(setEvents)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Failed to load events."))
      .finally(() => setLoading(false));
  }, [gameId]);

  const canProceed = selected.length === REQUIRED_EVENT_COUNT;
  const selectedEvents = useMemo(
    () => selected.map((id) => events.find((e) => e.id === id)!).filter(Boolean),
    [selected, events],
  );
  const unplacedEvents = selectedEvents.filter((e) => !Object.values(placements).includes(e.id));
  const gridPositions = Array.from({ length: 25 }, (_, i) => i);

  function toggleSelected(eventId: string) {
    setSelected((prev) => {
      if (prev.includes(eventId)) return prev.filter((id) => id !== eventId);
      if (prev.length >= REQUIRED_EVENT_COUNT) return prev;
      return [...prev, eventId];
    });
  }

  async function handleRandomize() {
    setSubmitting(true);
    setError(null);
    try {
      const board = await boardApi.createRandom(gameId, selected);
      onBoardCreated(board);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create board.");
    } finally {
      setSubmitting(false);
    }
  }

  function placeAt(position: number) {
    if (!armedEventId) return;
    setPlacements((prev) => ({ ...prev, [position]: armedEventId }));
    setArmedEventId(null);
  }

  function clearPosition(position: number) {
    setPlacements((prev) => {
      const next = { ...prev };
      delete next[position];
      return next;
    });
  }

  async function handleSaveManual() {
    setSubmitting(true);
    setError(null);
    try {
      const positions = Object.entries(placements).map(([position, eventId]) => ({
        position: Number(position),
        eventId,
      }));
      const board = await boardApi.createManual(gameId, positions);
      onBoardCreated(board);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create board.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) return <p className="muted">Loading events...</p>;

  if (events.length < REQUIRED_EVENT_COUNT) {
    return (
      <div className="card">
        <p>
          This game needs at least {REQUIRED_EVENT_COUNT} events in its pool before you can build a board. It
          currently has {events.length}. Add more events in the Events tab.
        </p>
      </div>
    );
  }

  return (
    <div className="stack">
      {error && <div className="error-banner">{error}</div>}

      {mode === "select" && (
        <div className="stack">
          <p>
            Choose exactly {REQUIRED_EVENT_COUNT} events for your board ({selected.length}/{REQUIRED_EVENT_COUNT}{" "}
            selected). Your board is locked once created, so choose carefully.
          </p>
          <div className="stack" style={{ maxHeight: 360, overflowY: "auto" }}>
            {events.map((evt) => (
              <div
                key={evt.id}
                className={`event-picker-item ${selected.includes(evt.id) ? "selected" : ""}`}
                onClick={() => toggleSelected(evt.id)}
              >
                <input type="checkbox" readOnly checked={selected.includes(evt.id)} />
                <span>{evt.text}</span>
              </div>
            ))}
          </div>
          <div className="row">
            <button className="btn" disabled={!canProceed || submitting} onClick={handleRandomize}>
              {submitting ? "Creating..." : "Randomize board"}
            </button>
            <button
              className="btn btn-secondary"
              disabled={!canProceed}
              onClick={() => setMode("manual")}
            >
              Arrange manually instead
            </button>
          </div>
        </div>
      )}

      {mode === "manual" && (
        <div className="stack">
          <p>Click an event below, then click a square to place it. Click a filled square to remove it.</p>
          <div className="row" style={{ flexWrap: "wrap" }}>
            {unplacedEvents.map((evt) => (
              <button
                key={evt.id}
                type="button"
                className={`btn btn-sm ${armedEventId === evt.id ? "" : "btn-secondary"}`}
                onClick={() => setArmedEventId((prev) => (prev === evt.id ? null : evt.id))}
              >
                {evt.text}
              </button>
            ))}
            {unplacedEvents.length === 0 && <span className="muted">All events placed.</span>}
          </div>

          <div className="bingo-grid">
            {gridPositions.map((pos) => {
              if (pos === FREE_POSITION) {
                return (
                  <div key={pos} className="bingo-square is-free">
                    <span className="bingo-square-free">FREE</span>
                  </div>
                );
              }

              const placedEventId = placements[pos];
              const placedEvent = placedEventId ? events.find((e) => e.id === placedEventId) : undefined;

              return (
                <button
                  key={pos}
                  type="button"
                  className={`bingo-square ${placedEvent ? "is-confirmed" : ""} ${!placedEvent && armedEventId ? "is-clickable" : ""}`}
                  onClick={() => (placedEvent ? clearPosition(pos) : placeAt(pos))}
                >
                  <span className="bingo-square-text">{placedEvent ? placedEvent.text : "empty"}</span>
                </button>
              );
            })}
          </div>

          <div className="row">
            <button
              className="btn"
              disabled={Object.keys(placements).length !== REQUIRED_EVENT_COUNT || submitting}
              onClick={handleSaveManual}
            >
              {submitting ? "Creating..." : "Save board"}
            </button>
            <button className="btn btn-secondary" onClick={() => setMode("select")}>
              Back to event selection
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
