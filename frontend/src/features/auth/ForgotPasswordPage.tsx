import { useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router-dom";
import { authApi } from "../../api/endpoints";
import { ApiError } from "../../api/client";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await authApi.forgotPassword(email);
      setSent(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="center-page">
      <div className="card auth-card">
        <h1>Forgot password</h1>
        {sent ? (
          <div className="success-banner">
            If <strong>{email}</strong> belongs to an account with a verified email, we've sent it a link to
            reset your password. The link expires in 1 hour.
          </div>
        ) : (
          <>
            <p className="muted" style={{ marginTop: -8, marginBottom: 16 }}>
              Enter your account email and we'll send you a link to reset your password.
            </p>
            <form onSubmit={handleSubmit} className="stack">
              {error && <div className="error-banner">{error}</div>}
              <div className="field">
                <label htmlFor="email">Email</label>
                <input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
              <button className="btn" type="submit" disabled={loading}>
                {loading ? "Sending..." : "Send reset link"}
              </button>
            </form>
          </>
        )}
        <p className="muted" style={{ marginTop: 16 }}>
          <Link to="/login">Back to log in</Link>
        </p>
      </div>
    </div>
  );
}
