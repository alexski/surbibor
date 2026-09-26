import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { authApi } from "../api/endpoints";
import { ApiError } from "../api/client";
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
      {user && !user.emailVerified && <VerifyEmailBanner />}
      <main>{children}</main>
    </div>
  );
}

function VerifyEmailBanner() {
  const user = useAuthStore((s) => s.user)!;
  const setUser = useAuthStore((s) => s.setUser);
  const { pathname } = useLocation();
  const [status, setStatus] = useState<"idle" | "sending" | "sent">("idle");
  const [error, setError] = useState<string | null>(null);

  // The link may have been opened in another tab or device, so re-check on mount.
  useEffect(() => {
    authApi
      .me()
      .then(setUser)
      .catch(() => {
        // Keep showing the banner; a 401 already logs the user out via the API client.
      });
  }, [setUser]);

  if (pathname === "/verify-email") return null;

  async function resend() {
    setError(null);
    setStatus("sending");
    try {
      await authApi.resendVerification();
      setStatus("sent");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong.");
      setStatus("idle");
    }
  }

  return (
    <div className="page" style={{ paddingBottom: 0 }}>
      <div className={error ? "error-banner" : "success-banner"}>
        {status === "sent" ? (
          <>We sent a new verification link to {user.email}.</>
        ) : (
          <>
            {error ?? <>Please verify your email ({user.email}) so you can reset your password if you forget it.</>}{" "}
            <button className="btn btn-secondary btn-sm" onClick={resend} disabled={status === "sending"}>
              {status === "sending" ? "Sending..." : "Resend link"}
            </button>
          </>
        )}
      </div>
    </div>
  );
}
