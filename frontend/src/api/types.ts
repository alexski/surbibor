export interface UserResponse {
  id: string;
  email: string;
  username: string;
  emailVerified: boolean;
}

export interface AuthResponse {
  token: string;
  user: UserResponse;
}

export interface GameSummary {
  id: string;
  name: string;
  description: string;
  inviteCode: string;
  status: "Active" | "Completed";
  createdAt: string;
}

export interface GameMember {
  userId: string;
  username: string;
  joinedAt: string;
  points: number;
}

export interface GameDetail {
  id: string;
  name: string;
  description: string;
  inviteCode: string;
  status: "Active" | "Completed";
  winnerId: string | null;
  createdAt: string;
  completedAt: string | null;
  members: GameMember[];
}

export type EventStatus = "Open" | "PendingConfirmation" | "Confirmed";

export interface EventItem {
  id: string;
  text: string;
  status: EventStatus;
  createdByUserId: string;
  proposedByUserId: string | null;
  confirmedByUserId: string | null;
  createdAt: string;
}

export interface BoardSquare {
  position: number;
  eventId: string | null;
  eventText: string | null;
  eventStatus: EventStatus | null;
  eventProposedByUserId: string | null;
  isFree: boolean;
  isMarked: boolean;
  markedAt: string | null;
}

export interface Board {
  id: string;
  gameId: string;
  userId: string;
  createdAt: string;
  squares: BoardSquare[];
}

export type SideBetStatus = "Open" | "PendingConfirmation" | "Resolved";

export interface SideBetWager {
  userId: string;
  prediction: boolean;
  stake: number;
  placedAt: string;
}

export interface SideBet {
  id: string;
  text: string;
  createdByUserId: string;
  createdAt: string;
  placingClosesAt: string;
  expectedResolutionAt: string;
  status: SideBetStatus;
  proposedOutcome: boolean | null;
  proposedByUserId: string | null;
  proposedAt: string | null;
  outcome: boolean | null;
  resolvedByUserId: string | null;
  resolvedAt: string | null;
  wagers: SideBetWager[];
}
