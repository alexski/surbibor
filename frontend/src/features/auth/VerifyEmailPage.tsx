import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { authApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import { useAuthStore } from "../../store/authStore";

export function VerifyEmailPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const [status, setStatus] = useState<"verifying" | "verified" | "error">(token ? "verifying" : "error");
  const [error, setError] = useState<string | null>(token ? null : "This verification link is missing its token.");
  const currentUser = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);
  // Tokens are single-use, so guard against StrictMode's double effect run consuming it twice.
  const started = useRef(false);

  useEffect(() => {
    if (!token || started.current) return;
    started.current = true;

    authApi
      .verifyEmail(token)
      .then((user) => {
        if (useAuthStore.getState().user?.id === user.id) setUser(user);
        setStatus("verified");
      })
      .catch((err) => {
        setError(err instanceof ApiError ? err.message : "Something went wrong.");
        setStatus("error");
      });
  }, [token, setUser]);

  return (
    <div className="center-page">
      <div className="card auth-card">
        <h1>Verify email</h1>
        {status === "verifying" && <p className="muted">Verifying your email...</p>}
        {status === "verified" && <div className="success-banner">Your email is verified. Thanks!</div>}
        {status === "error" && <div className="error-banner">{error}</div>}
        <p className="muted" style={{ marginTop: 16 }}>
          {currentUser ? <Link to="/games">Go to your games</Link> : <Link to="/login">Go to log in</Link>}
        </p>
      </div>
    </div>
  );
}
