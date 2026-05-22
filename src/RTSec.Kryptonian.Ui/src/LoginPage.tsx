import { FormEvent, useState } from 'react';
import { api, AuthResponse, setStoredToken } from './api';
import { BrandMark } from './BrandMark';

interface Props {
  onLoginSuccess: (auth: AuthResponse) => void;
}

export function LoginPage({ onLoginSuccess }: Props) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const auth = await api.login(username.trim(), password);
      setStoredToken(auth.token);
      onLoginSuccess(auth);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Login failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="authPage">
      <div className="authStage">
        <section className="authHero" aria-label="Kryptonian Gateway">
          <BrandMark variant="auth" />
          <div>
            <p className="eyebrow">Device Registration Portal</p>
            <h1>Kryptonian Gateway</h1>
            <p>
              Register, activate, and renew trusted medical devices through a controlled gateway built for certificate-backed operations.
            </p>
          </div>
          <div className="authTrustGrid">
            <span>Device identity</span>
            <span>CA-backed issuance</span>
            <span>Enrollment audit trail</span>
          </div>
        </section>

        <div className="authCard">
          <BrandMark variant="compact" />
          <h2>Sign in</h2>
          <p className="authSubtitle">Access device registration, certificates, EST profiles, and gateway operations.</p>
          {error && <div className="authError">{error}</div>}
          <form onSubmit={(e) => void handleSubmit(e)}>
            <label>
              Username
              <input
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
                autoFocus
                autoComplete="username"
                placeholder="Enter your username"
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="current-password"
                placeholder="Enter your password"
              />
            </label>
            <button type="submit" className="primary authSubmit" disabled={loading}>
              {loading ? 'Signing in...' : 'Sign in'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
