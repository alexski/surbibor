import { create } from "zustand";
import type { UserResponse } from "../api/types";

const STORAGE_KEY = "surbibor.auth";

interface StoredAuth {
  token: string;
  user: UserResponse;
}

function loadStored(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as StoredAuth;
  } catch {
    return null;
  }
}

interface AuthState {
  token: string | null;
  user: UserResponse | null;
  login: (token: string, user: UserResponse) => void;
  logout: () => void;
}

const stored = loadStored();

export const useAuthStore = create<AuthState>((set) => ({
  token: stored?.token ?? null,
  user: stored?.user ?? null,
  login: (token, user) => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ token, user }));
    set({ token, user });
  },
  logout: () => {
    localStorage.removeItem(STORAGE_KEY);
    set({ token: null, user: null });
  },
}));
