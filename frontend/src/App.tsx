import { useState, useEffect, useCallback } from 'react'
import ConnectionPanel from './components/ConnectionPanel/ConnectionPanel'
import SchemaBrowser from './components/SchemaBrowser/SchemaBrowser'
import QueryEditor from './components/QueryEditor/QueryEditor'
import ScriptLibrary from './components/ScriptLibrary/ScriptLibrary'
import { getStatus } from './api/client'
import type { ConnectionStatus, VizResult } from './types'
import './App.css'

export default function App() {
  const [status, setStatus] = useState<ConnectionStatus>({ connected: false })
  const [vizResult, setVizResult] = useState<VizResult | null>(null)
  const [sideTab, setSideTab] = useState<'schema' | 'scripts'>('schema')

  const refreshStatus = useCallback(async () => {
    try {
      const s = await getStatus()
      setStatus(s)
    } catch {
      setStatus({ connected: false })
    }
  }, [])

  useEffect(() => {
    refreshStatus()
  }, [refreshStatus])

  return (
    <div className="app-shell">
      {/* ── Left sidebar ── */}
      <aside className="sidebar">
        <ConnectionPanel status={status} onStatusChange={refreshStatus} />

        <div className="side-tabs">
          <button
            className={sideTab === 'schema' ? 'active' : ''}
            onClick={() => setSideTab('schema')}
          >
            Schema
          </button>
          <button
            className={sideTab === 'scripts' ? 'active' : ''}
            onClick={() => setSideTab('scripts')}
          >
            Scripts
          </button>
        </div>

        {sideTab === 'schema' ? (
          <SchemaBrowser
            connected={status.connected}
            engine={status.engine}
          />
        ) : (
          <ScriptLibrary connected={status.connected} engine={status.engine} />
        )}
      </aside>

      {/* ── Main content ── */}
      <main className="main-content">
        <QueryEditor
          connected={status.connected}
          onVizResult={setVizResult}
          vizResult={vizResult}
        />
      </main>
    </div>
  )
}

