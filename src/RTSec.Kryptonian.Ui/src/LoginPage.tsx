import { FormEvent, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { api, AuthResponse, setStoredToken } from './api';

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
      <div className="authCard">
        <div className="authBrand">
          <ShieldCheck size={36} />
          <div>
            <strong>Kryptonian</strong>
            <span>MEDIATE Gateway</span>
          </div>
        </div>
        <h2>Sign In</h2>
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
            {loading ? 'Signing in…' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
}
