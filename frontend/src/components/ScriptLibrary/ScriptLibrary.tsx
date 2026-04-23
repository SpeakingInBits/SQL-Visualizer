import { useState } from 'react'
import { listScripts, uploadScript, deleteScript, runScript } from '../../api/client'
import type { Script, ScriptRunResult, EngineType } from '../../types'
import './ScriptLibrary.css'
import { useEffect } from 'react'

interface Props {
  connected: boolean
  engine?: EngineType
}

export default function ScriptLibrary({ connected, engine }: Props) {
  const [scripts, setScripts] = useState<Script[]>([])
  const [runResults, setRunResults] = useState<Record<string, ScriptRunResult>>({})
  const [running, setRunning] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)

  useEffect(() => {
    listScripts().then(setScripts).catch(() => {})
  }, [])

  async function handleUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    try {
      const entry = await uploadScript(file, engine ?? '')
      setScripts(prev => [...prev, entry])
    } catch (err) {
      alert('Upload failed: ' + String(err))
    } finally {
      setUploading(false)
      e.target.value = ''
    }
  }

  async function handleRun(id: string) {
    setRunning(id)
    try {
      const res = await runScript(id)
      setRunResults(prev => ({ ...prev, [id]: res }))
      setScripts(prev =>
        prev.map(s => s.id === id ? { ...s, last_used: new Date().toISOString() } : s)
      )
    } catch (err) {
      alert('Run failed: ' + String(err))
    } finally {
      setRunning(null)
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Remove this script from the library?')) return
    await deleteScript(id)
    setScripts(prev => prev.filter(s => s.id !== id))
    setRunResults(prev => { const n = { ...prev }; delete n[id]; return n })
  }

  return (
    <div className="sl-root">
      <div className="sl-toolbar">
        <label className={`btn btn-secondary btn-sm ${uploading ? 'disabled' : ''}`}>
          {uploading ? 'Uploading…' : '+ Upload .sql'}
          <input
            type="file"
            accept=".sql"
            style={{ display: 'none' }}
            onChange={handleUpload}
            disabled={uploading}
          />
        </label>
      </div>

      {scripts.length === 0 ? (
        <div className="sl-empty">No scripts saved yet. Upload a .sql file to get started.</div>
      ) : (
        <ul className="sl-list">
          {scripts.map(s => (
            <li key={s.id} className="sl-item">
              <div className="sl-item-header">
                <span className="sl-name">{s.name}</span>
                <div className="sl-actions">
                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => handleRun(s.id)}
                    disabled={!connected || running === s.id}
                  >
                    {running === s.id ? '…' : '▶ Run'}
                  </button>
                  <button
                    className="btn btn-danger btn-sm"
                    onClick={() => handleDelete(s.id)}
                    disabled={running === s.id}
                  >
                    ✕
                  </button>
                </div>
              </div>
              {s.last_used && (
                <span className="sl-meta">Last run: {new Date(s.last_used).toLocaleString()}</span>
              )}
              {runResults[s.id] && (
                <ScriptOutput result={runResults[s.id]} />
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function ScriptOutput({ result }: { result: ScriptRunResult }) {
  const errors = result.results.filter(r => !r.ok)
  return (
    <div className="sl-output">
      {result.results.map((r, i) => (
        <div key={i} className={`sl-stmt ${r.ok ? 'ok' : 'err'}`}>
          <span className="sl-stmt-type">{r.type}</span>
          <span className="sl-stmt-text">{r.statement}{r.statement.length >= 120 ? '…' : ''}</span>
          {r.ok ? (
            <span className="sl-stmt-count">{r.rows_affected} row(s)</span>
          ) : (
            <span className="sl-stmt-error">{r.error}</span>
          )}
        </div>
      ))}
      {errors.length === 0 && (
        <div className="sl-success">✓ All {result.results.length} statement(s) succeeded.</div>
      )}
    </div>
  )
}
