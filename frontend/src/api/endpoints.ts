import { api } from "./client";
import type {
  AuthResponse,
  Board,
  EventItem,
  GameDetail,
  GameSummary,
  PlayerBoardProgress,
  SideBet,
  UserResponse,
} from "./types";

export const authApi = {
  register: (email: string, username: string, password: string) =>
    api.post<AuthResponse>("/api/auth/register", { email, username, password }),
  login: (email: string, password: string) =>
    api.post<AuthResponse>("/api/auth/login", { email, password }),
  me: () => api.get<UserResponse>("/api/auth/me"),
  verifyEmail: (token: string) => api.post<UserResponse>("/api/auth/verify-email", { token }),
  resendVerification: () => api.post<void>("/api/auth/resend-verification"),
  forgotPassword: (email: string) => api.post<void>("/api/auth/forgot-password", { email }),
  resetPassword: (token: string, newPassword: string, confirmPassword: string) =>
    api.post<AuthResponse>("/api/auth/reset-password", { token, newPassword, confirmPassword }),
};

export const gamesApi = {
  list: () => api.get<GameSummary[]>("/api/games"),
  create: (name: string, description: string) =>
    api.post<GameSummary>("/api/games", { name, description }),
  get: (gameId: string) => api.get<GameDetail>(`/api/games/${gameId}`),
  join: (inviteCode: string) => api.post<GameSummary>("/api/games/join", { inviteCode }),
};

export const eventsApi = {
  list: (gameId: string) => api.get<EventItem[]>(`/api/games/${gameId}/events`),
  add: (gameId: string, text: string) => api.post<EventItem>(`/api/games/${gameId}/events`, { text }),
  propose: (gameId: string, eventId: string) =>
    api.post<EventItem>(`/api/games/${gameId}/events/${eventId}/propose`),
  unpropose: (gameId: string, eventId: string) =>
    api.post<EventItem>(`/api/games/${gameId}/events/${eventId}/unpropose`),
  confirm: (gameId: string, eventId: string) =>
    api.post<EventItem>(`/api/games/${gameId}/events/${eventId}/confirm`),
  reject: (gameId: string, eventId: string) =>
    api.post<EventItem>(`/api/games/${gameId}/events/${eventId}/reject`),
};

export const boardApi = {
  get: (gameId: string) => api.get<Board>(`/api/games/${gameId}/board`),
  others: (gameId: string) => api.get<PlayerBoardProgress[]>(`/api/games/${gameId}/board/others`),
  createRandom: (gameId: string, eventIds: string[]) =>
    api.post<Board>(`/api/games/${gameId}/board/random`, { eventIds }),
  createManual: (gameId: string, positions: { position: number; eventId: string }[]) =>
    api.post<Board>(`/api/games/${gameId}/board/manual`, { positions }),
  mark: (gameId: string, position: number) =>
    api.post<Board>(`/api/games/${gameId}/board/squares/${position}/mark`),
};

export const sideBetsApi = {
  list: (gameId: string) => api.get<SideBet[]>(`/api/games/${gameId}/sidebets`),
  create: (gameId: string, text: string, placingClosesAt: string, expectedResolutionAt: string) =>
    api.post<SideBet>(`/api/games/${gameId}/sidebets`, { text, placingClosesAt, expectedResolutionAt }),
  placeWager: (gameId: string, sideBetId: string, prediction: boolean, stake: number) =>
    api.post<SideBet>(`/api/games/${gameId}/sidebets/${sideBetId}/wagers`, { prediction, stake }),
  propose: (gameId: string, sideBetId: string, outcome: boolean) =>
    api.post<SideBet>(`/api/games/${gameId}/sidebets/${sideBetId}/propose`, { outcome }),
  confirm: (gameId: string, sideBetId: string) =>
    api.post<SideBet>(`/api/games/${gameId}/sidebets/${sideBetId}/confirm`),
  reject: (gameId: string, sideBetId: string) =>
    api.post<SideBet>(`/api/games/${gameId}/sidebets/${sideBetId}/reject`),
};

export type { UserResponse };
