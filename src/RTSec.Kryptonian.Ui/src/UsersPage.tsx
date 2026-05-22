import { FormEvent, useEffect, useState } from 'react';
import { Plus, Trash2, UserCog } from 'lucide-react';
import { api, UserProfile } from './api';

type Runner = <T>(op: () => Promise<T>, success: string, opts?: { refresh?: boolean }) => Promise<T | undefined>;

interface Props {
  run: Runner;
  currentUserId: string;
}

const ROLES = ['Standard', 'DeviceAdmin', 'SystemAdmin'];

const roleTone: Record<string, string> = {
  SystemAdmin: 'badge-teal',
  DeviceAdmin: 'badge-blue',
  Standard: 'badge-gray',
};

export function UsersPage({ run, currentUserId }: Props) {
  const [users, setUsers] = useState<UserProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [editTarget, setEditTarget] = useState<UserProfile | null>(null);

  const loadUsers = async () => {
    setLoading(true);
    try {
      setUsers(await api.getUsers());
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void loadUsers(); }, []);

  const handleDelete = async (user: UserProfile) => {
    if (!window.confirm(`Delete user "${user.username}"? This cannot be undone.`)) return;
    await run(() => api.deleteUser(user.id), `User "${user.username}" deleted.`);
    void loadUsers();
  };

  const handleToggleActive = async (user: UserProfile) => {
    const action = user.isActive ? 'deactivate' : 'activate';
    await run(
      () => api.updateUser(user.id, { isActive: !user.isActive }),
      `User "${user.username}" ${action}d.`,
      { refresh: false },
    );
    void loadUsers();
  };

  return (
    <section className="panel">
      <div className="panelHeader">
        <h2>Users</h2>
        <button className="primary" onClick={() => setShowAdd(true)}>
          <Plus size={16} /> Add User
        </button>
      </div>

      {loading ? (
        <p className="loading">Loading users…</p>
      ) : (
        <table className="dataTable">
          <thead>
            <tr>
              <th>Username</th>
              <th>Email</th>
              <th>Role</th>
              <th>Status</th>
              <th>Created</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 && (
              <tr><td colSpan={6} className="empty">No users</td></tr>
            )}
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.username}{user.id === currentUserId && <span className="selfTag"> (you)</span>}</td>
                <td>{user.email}</td>
                <td><span className={`badge ${roleTone[user.role] ?? 'badge-gray'}`}>{user.role}</span></td>
                <td>
                  <span className={`badge ${user.isActive ? 'success' : 'warn'}`}>
                    {user.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>{new Date(user.createdAt).toLocaleDateString()}</td>
                <td className="actions">
                  <button onClick={() => setEditTarget(user)} title="Edit user">
                    <UserCog size={14} />
                  </button>
                  <button onClick={() => void handleToggleActive(user)} title={user.isActive ? 'Deactivate' : 'Activate'}>
                    {user.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  {user.id !== currentUserId && (
                    <button className="danger" onClick={() => void handleDelete(user)} title="Delete user">
                      <Trash2 size={14} />
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showAdd && (
        <AddUserModal
          onClose={() => setShowAdd(false)}
          onSaved={() => { setShowAdd(false); void loadUsers(); }}
          run={run}
        />
      )}

      {editTarget && (
        <EditUserModal
          user={editTarget}
          onClose={() => setEditTarget(null)}
          onSaved={() => { setEditTarget(null); void loadUsers(); }}
          run={run}
        />
      )}
    </section>
  );
}

function AddUserModal({ onClose, onSaved, run }: {
  onClose: () => void;
  onSaved: () => void;
  run: Runner;
}) {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('Standard');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const result = await run(
      () => api.createUser(username.trim(), email.trim(), password, role),
      `User "${username}" created.`,
      { refresh: false },
    );
    if (result) onSaved();
  };

  return (
    <div className="modalBackdrop">
      <div className="modal">
        <div className="modalHeader"><h2>Add User</h2><button onClick={onClose}>Close</button></div>
        <form onSubmit={(e) => void handleSubmit(e)}>
          <label>Username<input value={username} onChange={(e) => setUsername(e.target.value)} required /></label>
          <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required /></label>
          <label>
            Role
            <select value={role} onChange={(e) => setRole(e.target.value)}>
              {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
          </label>
          <label>Password (min 8 chars)
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
          </label>
          <div className="modalActions">
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className="primary">Create User</button>
          </div>
        </form>
      </div>
    </div>
  );
}

function EditUserModal({ user, onClose, onSaved, run }: {
  user: UserProfile;
  onClose: () => void;
  onSaved: () => void;
  run: Runner;
}) {
  const [role, setRole] = useState(user.role);
  const [newPassword, setNewPassword] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const update: { role?: string; newPassword?: string } = { role };
    if (newPassword) update.newPassword = newPassword;
    const result = await run(
      () => api.updateUser(user.id, update),
      `User "${user.username}" updated.`,
      { refresh: false },
    );
    if (result) onSaved();
  };

  return (
    <div className="modalBackdrop">
      <div className="modal">
        <div className="modalHeader"><h2>Edit {user.username}</h2><button onClick={onClose}>Close</button></div>
        <form onSubmit={(e) => void handleSubmit(e)}>
          <label>
            Role
            <select value={role} onChange={(e) => setRole(e.target.value)}>
              {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
          </label>
          <label>
            New Password (leave blank to keep current)
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} minLength={8} />
          </label>
          <div className="modalActions">
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className="primary">Save Changes</button>
          </div>
        </form>
      </div>
    </div>
  );
}
