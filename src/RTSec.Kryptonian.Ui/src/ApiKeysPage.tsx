import { FormEvent, useEffect, useState } from 'react';
import { Copy, Plus, Trash2 } from 'lucide-react';
import { api, ApiKeyRecord, GenerateApiKeyResponse } from './api';

type Runner = <T>(op: () => Promise<T>, success: string, opts?: { refresh?: boolean }) => Promise<T | undefined>;

interface Props {
  run: Runner;
  currentUserId: string;
  isSystemAdmin: boolean;
}

export function ApiKeysPage({ run, currentUserId, isSystemAdmin }: Props) {
  const [keys, setKeys] = useState<ApiKeyRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [showGenerate, setShowGenerate] = useState(false);
  const [newKey, setNewKey] = useState<GenerateApiKeyResponse | null>(null);

  const loadKeys = async () => {
    setLoading(true);
    try {
      setKeys(await api.getApiKeys(currentUserId));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void loadKeys(); }, [currentUserId]);

  const handleRevoke = async (key: ApiKeyRecord) => {
    if (!window.confirm(`Revoke key "${key.name}"? This cannot be undone.`)) return;
    await run(
      () => api.revokeApiKey(key.ownerId, key.id),
      `API key "${key.name}" revoked.`,
      { refresh: false },
    );
    void loadKeys();
  };

  const handleGenerated = (generated: GenerateApiKeyResponse) => {
    setShowGenerate(false);
    setNewKey(generated);
    void loadKeys();
  };

  return (
    <section className="panel">
      <div className="panelHeader">
        <h2>API Keys</h2>
        <button className="primary" onClick={() => setShowGenerate(true)}>
          <Plus size={16} /> Generate Key
        </button>
      </div>

      <p className="hint">
        API keys let scripts and devices authenticate to the REST API. Keys carry the role of their owner at time of generation.
      </p>

      {loading ? (
        <p className="loading">Loading API keys…</p>
      ) : (
        <table className="dataTable">
          <thead>
            <tr>
              <th>Name</th>
              {isSystemAdmin && <th>Owner</th>}
              <th>Prefix</th>
              <th>Role</th>
              <th>Expires</th>
              <th>Last Used</th>
              <th>Created</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {keys.length === 0 && (
              <tr><td colSpan={isSystemAdmin ? 8 : 7} className="empty">No API keys. Generate one to use the REST API programmatically.</td></tr>
            )}
            {keys.map((key) => (
              <tr key={key.id}>
                <td>{key.name}</td>
                {isSystemAdmin && <td>{key.ownerUsername}</td>}
                <td><code>{key.prefix}…</code></td>
                <td><span className="badge badge-gray">{key.role}</span></td>
                <td>{key.expiresAt ? new Date(key.expiresAt).toLocaleDateString() : 'Never'}</td>
                <td>{key.lastUsedAt ? new Date(key.lastUsedAt).toLocaleString() : '—'}</td>
                <td>{new Date(key.createdAt).toLocaleDateString()}</td>
                <td className="actions">
                  <button className="danger" onClick={() => void handleRevoke(key)} title="Revoke key">
                    <Trash2 size={14} /> Revoke
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showGenerate && (
        <GenerateKeyModal
          userId={currentUserId}
          onClose={() => setShowGenerate(false)}
          onGenerated={handleGenerated}
          run={run}
        />
      )}

      {newKey && (
        <NewKeyDisplayModal
          apiKey={newKey}
          onClose={() => setNewKey(null)}
        />
      )}
    </section>
  );
}

function GenerateKeyModal({ userId, onClose, onGenerated, run }: {
  userId: string;
  onClose: () => void;
  onGenerated: (key: GenerateApiKeyResponse) => void;
  run: Runner;
}) {
  const [name, setName] = useState('');
  const [expiresAt, setExpiresAt] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const result = await run(
      () => api.generateApiKey(userId, name.trim(), expiresAt || null),
      `API key "${name}" generated.`,
      { refresh: false },
    );
    if (result) onGenerated(result);
  };

  return (
    <div className="modalBackdrop">
      <div className="modal">
        <div className="modalHeader"><h2>Generate API Key</h2><button onClick={onClose}>Close</button></div>
        <form onSubmit={(e) => void handleSubmit(e)}>
          <label>Key Name (label for identification)<input value={name} onChange={(e) => setName(e.target.value)} required placeholder="e.g. CI pipeline, Device scanner" /></label>
          <label>
            Expiry Date (optional — leave blank for no expiry)
            <input type="date" value={expiresAt} onChange={(e) => setExpiresAt(e.target.value)} />
          </label>
          <div className="modalActions">
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className="primary">Generate</button>
          </div>
        </form>
      </div>
    </div>
  );
}

function NewKeyDisplayModal({ apiKey, onClose }: { apiKey: GenerateApiKeyResponse; onClose: () => void }) {
  const [copied, setCopied] = useState(false);

  const copyKey = async () => {
    await navigator.clipboard.writeText(apiKey.rawKey);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="modalBackdrop">
      <div className="modal">
        <div className="modalHeader"><h2>API Key Generated</h2><button onClick={onClose}>Close</button></div>
        <div className="newKeyWarning">
          <strong>Copy this key now — it will not be shown again.</strong>
        </div>
        <p><strong>Name:</strong> {apiKey.name}</p>
        <div className="keyDisplay">
          <code className="keyValue">{apiKey.rawKey}</code>
          <button onClick={() => void copyKey()} title="Copy to clipboard">
            <Copy size={14} /> {copied ? 'Copied!' : 'Copy'}
          </button>
        </div>
        <p className="hint">Use this key in the <code>X-API-Key</code> header when calling the REST API.</p>
        <div className="modalActions">
          <button className="primary" onClick={onClose}>Done</button>
        </div>
      </div>
    </div>
  );
}
