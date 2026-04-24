import { useState, useEffect } from 'react'
import { getDatabases, getTables, getColumns } from '../../api/client'
import type { ColumnMeta, EngineType, ForeignKey } from '../../types'
import './SchemaBrowser.css'

interface Props {
  connected: boolean
  engine?: EngineType
}

interface TableNode {
  schema: string
  name: string
}

interface ColumnNode extends ColumnMeta {
  is_foreign_key?: boolean
}

export default function SchemaBrowser({ connected, engine }: Props) {
  const [databases, setDatabases] = useState<string[]>([])
  const [selectedDb, setSelectedDb] = useState<string>('')
  const [tables, setTables] = useState<TableNode[]>([])
  const [expandedTable, setExpandedTable] = useState<string | null>(null)
  const [columns, setColumns] = useState<Record<string, { cols: ColumnNode[]; fks: ForeignKey[] }>>({})
  const [loadingDbs, setLoadingDbs] = useState(false)
  const [loadingTables, setLoadingTables] = useState(false)

  useEffect(() => {
    if (!connected) {
      setDatabases([])
      setTables([])
      setSelectedDb('')
      setExpandedTable(null)
      return
    }
    setLoadingDbs(true)
    getDatabases()
      .then(dbs => {
        setDatabases(dbs)
        if (dbs.length > 0) setSelectedDb(dbs[0])
      })
      .catch(() => {})
      .finally(() => setLoadingDbs(false))
  }, [connected])

  useEffect(() => {
    if (!selectedDb) return
    setLoadingTables(true)
    setTables([])
    setExpandedTable(null)
    getTables(selectedDb)
      .then(setTables)
      .catch(() => {})
      .finally(() => setLoadingTables(false))
  }, [selectedDb])

  async function toggleTable(t: TableNode) {
    const key = `${t.schema}.${t.name}`
    if (expandedTable === key) {
      setExpandedTable(null)
      return
    }
    setExpandedTable(key)
    if (!columns[key]) {
      try {
        const res = await getColumns(t.name, t.schema || 'dbo', selectedDb || undefined)
        setColumns(prev => ({ ...prev, [key]: { cols: res.columns, fks: res.foreign_keys } }))
      } catch {}
    }
  }

  if (!connected) {
    return <div className="schema-empty">Connect to a database to browse its schema.</div>
  }

  return (
    <div className="schema-browser">
      {engine === 'sqlserver' && (
        <div className="schema-db-select">
          <select value={selectedDb} onChange={e => setSelectedDb(e.target.value)} disabled={loadingDbs}>
            {databases.map(db => <option key={db} value={db}>{db}</option>)}
          </select>
        </div>
      )}

      {loadingTables ? (
        <div className="schema-loading">Loading tables…</div>
      ) : (
        <ul className="schema-tree">
          {tables.map(t => {
            const key = `${t.schema}.${t.name}`
            const isExpanded = expandedTable === key
            const colData = columns[key]
            return (
              <li key={key}>
                <button
                  className={`schema-table-row ${isExpanded ? 'expanded' : ''}`}
                  onClick={() => toggleTable(t)}
                >
                  <span className="schema-icon">⊞</span>
                  <span className="schema-table-name">{t.name}</span>
                  {t.schema && t.schema !== 'dbo' && (
                    <span className="schema-schema">{t.schema}</span>
                  )}
                </button>
                {isExpanded && colData && (
                  <ul className="schema-cols">
                    {colData.cols.map(col => (
                      <li key={col.name} className="schema-col">
                        <span className="schema-col-icon">
                          {col.is_primary_key ? '🔑' : col.is_foreign_key ? '🔗' : '·'}
                        </span>
                        <span className="schema-col-name">{col.name}</span>
                        <span className="schema-col-type">{col.data_type}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
