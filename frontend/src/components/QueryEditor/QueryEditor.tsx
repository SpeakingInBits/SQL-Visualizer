import { useState, useEffect } from 'react'
import Editor from '@monaco-editor/react'
import { runQuery, visualizeQuery } from '../../api/client'
import type { VizResult, PlainResult } from '../../types'
import Visualizer from '../Visualizer/Visualizer'
import './QueryEditor.css'

interface Props {
  connected: boolean
  selectedTable: { name: string; schema: string } | null
  onVizResult: (r: VizResult | null) => void
  vizResult: VizResult | null
}

type ActiveTab = 'results' | 'visualizer'

export default function QueryEditor({ connected, selectedTable, onVizResult, vizResult }: Props) {
  const [sql, setSql] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [plainResult, setPlainResult] = useState<PlainResult | null>(null)
  const [activeTab, setActiveTab] = useState<ActiveTab>('results')

  // When user clicks a table in schema browser, pre-fill a SELECT
  useEffect(() => {
    if (!selectedTable) return
    const qualifier = selectedTable.schema && selectedTable.schema !== 'dbo'
      ? `[${selectedTable.schema}].[${selectedTable.name}]`
      : `[${selectedTable.name}]`
    setSql(`SELECT TOP 100 * FROM ${qualifier}`)
  }, [selectedTable])

  async function handleRun() {
    if (!sql.trim()) return
    setLoading(true)
    setError('')
    setPlainResult(null)
    onVizResult(null)
    try {
      const res = await runQuery(sql)
      setPlainResult(res)
      setActiveTab('results')
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? String(e)
      setError(msg)
    } finally {
      setLoading(false)
    }
  }

  async function handleVisualize() {
    if (!sql.trim()) return
    setLoading(true)
    setError('')
    setPlainResult(null)
    onVizResult(null)
    try {
      const res = await visualizeQuery(sql)
      onVizResult(res)
      setActiveTab('visualizer')
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? String(e)
      setError(msg)
    } finally {
      setLoading(false)
    }
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      e.preventDefault()
      handleRun()
    }
  }

  const rowCount = plainResult?.rows?.length ?? 0

  return (
    <div className="qe-root">
      {/* Editor area */}
      <div className="qe-editor-wrap" onKeyDown={handleKeyDown}>
        <Editor
          height="200px"
          defaultLanguage="sql"
          value={sql}
          onChange={v => setSql(v ?? '')}
          theme="vs-dark"
          options={{
            fontSize: 14,
            minimap: { enabled: false },
            lineNumbers: 'on',
            scrollBeyondLastLine: false,
            wordWrap: 'on',
            fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
          }}
        />
      </div>

      {/* Toolbar */}
      <div className="qe-toolbar">
        <button
          className="btn btn-primary"
          onClick={handleRun}
          disabled={!connected || loading || !sql.trim()}
        >
          ▶ Run
        </button>
        <button
          className="btn btn-secondary"
          onClick={handleVisualize}
          disabled={!connected || loading || !sql.trim()}
        >
          ◈ Visualize
        </button>
        <span className="qe-hint">Ctrl+Enter to run</span>
        {loading && <span className="qe-loading">Running…</span>}
        {!connected && <span className="qe-hint" style={{ color: 'var(--red)' }}>Not connected</span>}
      </div>

      {/* Error banner */}
      {error && (
        <div className="qe-error">
          <strong>Error:</strong> {error}
        </div>
      )}

      {/* Results / Visualizer tabs */}
      <div className="qe-tabs">
        <button
          className={activeTab === 'results' ? 'active' : ''}
          onClick={() => setActiveTab('results')}
        >
          Results {plainResult && `(${rowCount} row${rowCount !== 1 ? 's' : ''})`}
        </button>
        <button
          className={activeTab === 'visualizer' ? 'active' : ''}
          onClick={() => setActiveTab('visualizer')}
          disabled={!vizResult}
        >
          Visualizer
        </button>
      </div>

      {/* Tab content */}
      <div className="qe-output">
        {activeTab === 'results' && plainResult && (
          <ResultsTable result={plainResult} />
        )}
        {activeTab === 'visualizer' && vizResult && (
          <Visualizer result={vizResult} />
        )}
      </div>
    </div>
  )
}

function ResultsTable({ result }: { result: PlainResult }) {
  if (result.type === 'DML') {
    return (
      <div className="qe-dml-banner">
        ✓ Query executed — {result.rows_affected ?? 0} row(s) affected.
      </div>
    )
  }
  if (!result.rows.length) {
    return <div className="qe-empty">Query returned no rows.</div>
  }
  return (
    <div className="data-table-wrap">
      {result.truncated && (
        <div className="qe-truncated">⚠ Results capped at 200 rows for display.</div>
      )}
      <table className="data-table">
        <thead>
          <tr>{result.columns.map(c => <th key={c.name}>{c.name}</th>)}</tr>
        </thead>
        <tbody>
          {result.rows.map((row, i) => (
            <tr key={i}>
              {result.columns.map(c => (
                <td key={c.name}>{String(row[c.name] ?? '')}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
