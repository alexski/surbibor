import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { authApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";
import { useAuthStore } from "../../store/authStore";

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const login = useAuthStore((s) => s.login);
  const navigate = useNavigate();

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (newPassword !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setLoading(true);
    try {
      const auth = await authApi.resetPassword(token!, newPassword, confirmPassword);
      login(auth.token, auth.user);
      navigate("/games");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong.");
    } finally {
      setLoading(false);
    }
  }

  if (!token) {
    return (
      <div className="center-page">
        <div className="card auth-card">
          <h1>Reset password</h1>
          <div className="error-banner">This reset link is missing its token. Request a new one below.</div>
          <p className="muted" style={{ marginTop: 16 }}>
            <Link to="/forgot-password">Send a new reset link</Link>
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="center-page">
      <div className="card auth-card">
        <h1>Reset password</h1>
        <p className="muted" style={{ marginTop: -8, marginBottom: 16 }}>
          Choose a new password for your account.
        </p>
        <form onSubmit={handleSubmit} className="stack">
          {error && <div className="error-banner">{error}</div>}
          <div className="field">
            <label htmlFor="newPassword">New password</label>
            <input
              id="newPassword"
              type="password"
              required
              minLength={8}
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="confirmPassword">Confirm new password</label>
            <input
              id="confirmPassword"
              type="password"
              required
              minLength={8}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
            />
          </div>
          <button className="btn" type="submit" disabled={loading}>
            {loading ? "Resetting..." : "Reset password"}
          </button>
        </form>
        <p className="muted" style={{ marginTop: 16 }}>
          Link expired? <Link to="/forgot-password">Send a new one</Link>
        </p>
      </div>
    </div>
  );
}
