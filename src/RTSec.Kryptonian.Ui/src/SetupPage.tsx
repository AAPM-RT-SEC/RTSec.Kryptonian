import { FormEvent, useState } from 'react';
import { api, AuthResponse, setStoredToken } from './api';
import { BrandMark } from './BrandMark';

interface Props {
  onSetupComplete: (auth: AuthResponse) => void;
}

export function SetupPage({ onSetupComplete }: Props) {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    if (password !== confirm) {
      setError('Passwords do not match.');
      return;
    }
    if (password.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }

    setLoading(true);
    try {
      const auth = await api.setup(username.trim(), email.trim(), password);
      setStoredToken(auth.token);
      onSetupComplete(auth);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Setup failed. Please try again.');
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
            <p className="eyebrow">First Run Setup</p>
            <h1>Kryptonian Gateway</h1>
            <p>
              Establish the first administrator before opening device registration, certificate issuance, and gateway policy controls.
            </p>
          </div>
          <div className="authTrustGrid">
            <span>Admin controlled</span>
            <span>Audit ready</span>
            <span>Certificate lifecycle</span>
          </div>
        </section>

        <div className="authCard">
          <BrandMark variant="compact" />
          <h2>Create administrator account</h2>
          <p className="authSubtitle">
            This is the first time you are running Kryptonian Gateway. Create an admin account to get started.
          </p>
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
                placeholder="admin"
              />
            </label>
            <label>
              Email
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoComplete="email"
                placeholder="admin@example.com"
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="new-password"
                minLength={8}
                placeholder="At least 8 characters"
              />
            </label>
            <label>
              Confirm Password
              <input
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                required
                autoComplete="new-password"
                placeholder="Repeat password"
              />
            </label>
            <button type="submit" className="primary authSubmit" disabled={loading}>
              {loading ? 'Creating account...' : 'Create admin account'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
