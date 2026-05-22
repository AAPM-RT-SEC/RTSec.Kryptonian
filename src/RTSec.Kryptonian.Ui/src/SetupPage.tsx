import { FormEvent, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { api, AuthResponse, setStoredToken } from './api';

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
      <div className="authCard">
        <div className="authBrand">
          <ShieldCheck size={36} />
          <div>
            <strong>Kryptonian</strong>
            <span>MEDIATE Gateway</span>
          </div>
        </div>
        <h2>Create Administrator Account</h2>
        <p className="authSubtitle">
          This is the first time you're running Kryptonian Gateway. Create an admin account to get started.
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
            {loading ? 'Creating account…' : 'Create Admin Account'}
          </button>
        </form>
      </div>
    </div>
  );
}
