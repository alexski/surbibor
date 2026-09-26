# Surbibor — multiplayer TV-event BINGO

Multiplayer BINGO where the "numbers" are events from a TV show. Create a game per
show/season, build a shared pool of possible events, and race to complete a row,
column, or diagonal on your 5x5 board as those events actually happen on screen.

- **Marking is peer-confirmed and self-applied.** Any player can say an event
  happened; a *different* player must confirm it. Once confirmed, the square is
  highlighted on every board that has it, but each player taps their own square to
  mark it — you own your board.
- **Boards are locked once created.** Choose 24 events from the pool (any player can
  add new ones), then either randomize their placement or arrange them manually. No
  edits after that — the free center space is marked for you from the start.
- **First bingo wins and ends the game.** Row, column, or diagonal.

## Stack

- **Frontend**: React + TypeScript (Vite), React Router, Zustand, `@microsoft/signalr`.
- **Backend**: ASP.NET Core (.NET 10) Web API, EF Core + Npgsql, SignalR.
- **Database**: PostgreSQL.
- **Redis**: SignalR backplane (scale-out ready; a single instance works fine without it
  configured).

## Project layout

```
backend/
  src/Surbibor.Domain/          entities, enums, pure win-detection & board-layout logic
  src/Surbibor.Infrastructure/  EF Core DbContext, migrations, service layer
  src/Surbibor.Api/             controllers, SignalR hub, JWT auth, Program.cs
  tests/Surbibor.Api.Tests/     xUnit unit + WebApplicationFactory integration tests
frontend/
  src/api/                      fetch client + typed endpoint calls
  src/store/                    Zustand auth store
  src/hooks/useGameHub.ts       SignalR connection hook
  src/components/               BingoGrid, Layout, ProtectedRoute
  src/features/{auth,games,events,board}/
docker-compose.yml              Postgres + Redis for local dev
```

## Running it locally

### 1. Start Postgres and Redis

```bash
docker compose up -d
```

This starts Postgres on `5432` (db/user/password: `surbibor`/`surbibor`/`surbibor`),
Redis on `6379`, and [Mailpit](https://mailpit.axllent.org/) (a local SMTP catcher) on
`1025`, matching the backend's `appsettings.Development.json` defaults. Verification and
password-reset emails show up at `http://localhost:8025` instead of real inboxes.

**No Docker available?** (e.g. WSL without the Docker Desktop integration enabled) Run
both natively instead — on Debian/Ubuntu:

```bash
sudo apt install postgresql redis-server   # if not already installed
sudo service postgresql start
sudo -u postgres psql -c "CREATE USER surbibor WITH PASSWORD 'surbibor' CREATEDB;"
sudo -u postgres psql -c "CREATE DATABASE surbibor OWNER surbibor;"
redis-server --daemonize yes
```

For email, grab the single-binary [Mailpit](https://mailpit.axllent.org/docs/install/)
and run `mailpit`. It listens on the same ports.

### 2. Run the backend

```bash
cd backend
dotnet ef database update \
  --project src/Surbibor.Infrastructure/Surbibor.Infrastructure.csproj \
  --startup-project src/Surbibor.Api/Surbibor.Api.csproj
dotnet run --project src/Surbibor.Api
```

The API listens on `http://localhost:5069` by default (see
`src/Surbibor.Api/Properties/launchSettings.json`). Swagger UI is available at
`/swagger` in the Development environment.

The dev JWT secret and connection strings live in `appsettings.Development.json`
(clearly marked dev-only). For anything beyond local dev, copy `.env.example` at the
repo root, fill in a real `Jwt__Secret`, and export those variables (or wire them into
your deployment's secret store) before running.

### 3. Run the frontend

```bash
cd frontend
cp .env.example .env   # already defaults to http://localhost:5069
npm install
npm run dev
```

Open `http://localhost:5173`. Register two different accounts (e.g. in two browser
profiles/tabs) to try proposing an event as one player and confirming it as the other —
confirmation requires a different account than the one that proposed it.

## Testing

```bash
cd backend
dotnet test
```

This runs:
- Unit tests for the win-detection logic (`BingoWinChecker`), board layout validation,
  and the event propose/confirm/reject state machine (including the "can't confirm your
  own proposal" rule).
- An integration test that drives the full HTTP flow through `WebApplicationFactory`:
  register two users, create a game, join by invite code, add 24 events, build a random
  board, propose/confirm an event, mark a square, and verify the various guard rails
  (can't build a second board, can't confirm your own proposal, can't mark an
  unconfirmed square, unauthenticated requests are rejected).

Frontend: `cd frontend && npm run build` type-checks and builds; there's no automated
UI test suite in this MVP (see Limitations below).

## Design notes / assumptions

A few product decisions were made explicitly to keep this MVP well-scoped — see
`/home/alexski/.claude/plans/federated-sniffing-seal.md` for the full reasoning, or the
summary here:

1. **Winning ends the game.** The first completed line sets the game to `Completed` and
   records the winner; further event/board actions in that game are then blocked.
2. **Boards lock at creation.** No re-randomizing or editing afterward, so a player
   can't rearrange their board after seeing which events are already confirmed.
3. **Confirmed events never revert.** A *pending* proposal can be rejected by the
   confirmer (back to Open); a *confirmed* event is permanent.
4. Any game member can add events to the pool and propose/confirm/reject occurrences.
5. Auth is JWT-only, with no refresh tokens. Registration emails a verification link
   (valid 24h). Unverified users can still log in and play, and a banner prompts them to
   verify. **Password reset only works for verified emails**: "Forgot password" emails a
   single-use link (valid 1h) and always responds the same way whether or not the
   address exists, so it can't be used to discover accounts. Tokens are stored hashed,
   and each kind of email is limited to one per user per minute.

## Known limitations (out of scope for this MVP)

- No CI pipeline. `docker-compose.yml` covers local Postgres/Redis only; the backend has a
  `Dockerfile`, and [`docs/DEPLOYING_RAILWAY.md`](docs/DEPLOYING_RAILWAY.md) walks through
  a full Railway deployment.
- No automated frontend tests; the UI flow (register → create/join game → add events →
  build a board → propose/confirm → mark → win) should be exercised manually in a
  browser before considering a change fully verified.
- No spectator mode, event editing/removal, or admin moderation tools.
