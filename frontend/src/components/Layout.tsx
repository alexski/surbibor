import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useAuthStore } from "../store/authStore";

export function Layout({ children }: { children: ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  return (
    <div>
      <header className="app-header">
        <div className="app-header-inner">
          <Link to="/games" className="brand">
            Surbibor Bingo
          </Link>
          {user && (
            <div className="row">
              <span className="muted">{user.username}</span>
              <button className="btn btn-secondary btn-sm" onClick={logout}>
                Log out
              </button>
            </div>
          )}
        </div>
      </header>
      <main>{children}</main>
    </div>
  );
}
