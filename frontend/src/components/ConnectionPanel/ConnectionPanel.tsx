import { useState, useEffect } from 'react'
import { connectSqlServer, connectSQLite, disconnect, getSamples } from '../../api/client'
import type { ConnectionStatus } from '../../types'
import './ConnectionPanel.css'

interface Props {
  status: ConnectionStatus
  onStatusChange: () => void
}

export default function ConnectionPanel({ status, onStatusChange }: Props) {
  const [engine, setEngine] = useState<'sqlserver' | 'sqlite'>('sqlite')
  const [host, setHost] = useState('localhost')
  const [port, setPort] = useState('1433')
  const [database, setDatabase] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [trusted, setTrusted] = useState(false)
  const [sqlitePath, setSqlitePath] = useState('')
  const [samples, setSamples] = useState<{ name: string; path: string }[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [expanded, setExpanded] = useState(true)

  useEffect(() => {
    getSamples().then(setSamples).catch(() => {})
  }, [])

  async function handleConnect() {
    setLoading(true)
    setError('')
    try {
      if (engine === 'sqlserver') {
        await connectSqlServer({
          host,
          port: parseInt(port, 10) || 1433,
          database,
          username,
          password,
          trusted,
        })
      } else {
        await connectSQLite(sqlitePath)
      }
      onStatusChange()
      setExpanded(false)
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? String(e)
      setError(msg)
    } finally {
      setLoading(false)
    }
  }

  async function handleDisconnect() {
    await disconnect()
    onStatusChange()
    setExpanded(true)
  }

  return (
    <div className="conn-panel">
      <div className="conn-header" onClick={() => setExpanded(v => !v)}>
        <span className="conn-title">Connection</span>
        {status.connected ? (
          <span className="badge badge-green">● Connected</span>
        ) : (
          <span className="badge badge-red">○ Disconnected</span>
        )}
        <span className="conn-chevron">{expanded ? '▲' : '▼'}</span>
      </div>

      {status.connected && !expanded && (
        <div className="conn-active">
          <span className="conn-name">{status.display_name}</span>
          <button className="btn btn-danger btn-sm" onClick={handleDisconnect}>
            Disconnect
          </button>
        </div>
      )}

      {expanded && (
        <div className="conn-form">
          <div className="field">
            <label>Engine</label>
            <select value={engine} onChange={e => setEngine(e.target.value as 'sqlserver' | 'sqlite')}>
              <option value="sqlserver">SQL Server</option>
              <option value="sqlite">SQLite</option>
            </select>
          </div>

          {engine === 'sqlserver' ? (
            <>
              <div className="conn-row">
                <div className="field" style={{ flex: 2 }}>
                  <label>Host</label>
                  <input value={host} onChange={e => setHost(e.target.value)} placeholder="localhost" />
                </div>
                <div className="field" style={{ flex: 1 }}>
                  <label>Port</label>
                  <input value={port} onChange={e => setPort(e.target.value)} placeholder="1433" />
                </div>
              </div>
              <div className="field">
                <label>Database</label>
                <input value={database} onChange={e => setDatabase(e.target.value)} placeholder="master" />
              </div>
              <div className="field trusted-row">
                <label>
                  <input
                    type="checkbox"
                    checked={trusted}
                    onChange={e => setTrusted(e.target.checked)}
                  />
                  {' '}Windows Authentication
                </label>
              </div>
              {!trusted && (
                <>
                  <div className="field">
                    <label>Username</label>
                    <input value={username} onChange={e => setUsername(e.target.value)} placeholder="sa" />
                  </div>
                  <div className="field">
                    <label>Password</label>
                    <input
                      type="password"
                      value={password}
                      onChange={e => setPassword(e.target.value)}
                    />
                  </div>
                </>
              )}
            </>
          ) : (
            <div className="field">
              <label>SQLite file path</label>
              <input
                value={sqlitePath}
                onChange={e => setSqlitePath(e.target.value)}
                placeholder="/path/to/database.db"
              />
              {samples.length > 0 && (
                <div className="conn-samples">
                  <span className="conn-samples-label">Sample databases:</span>
                  {samples.map(s => (
                    <button
                      key={s.path}
                      className={`btn btn-secondary btn-sm conn-sample-btn${sqlitePath === s.path ? ' active' : ''}`}
                      onClick={() => setSqlitePath(s.path)}
                      type="button"
                    >
                      {s.name}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          {error && <div className="conn-error">{error}</div>}

          <button
            className="btn btn-primary"
            style={{ width: '100%', marginTop: 4 }}
            onClick={handleConnect}
            disabled={loading}
          >
            {loading ? 'Connecting…' : 'Connect'}
          </button>

          {status.connected && (
            <button
              className="btn btn-secondary"
              style={{ width: '100%', marginTop: 4 }}
              onClick={handleDisconnect}
            >
              Disconnect
            </button>
          )}
        </div>
      )}
    </div>
  )
}
