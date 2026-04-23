import axios from 'axios'
import type {
  ConnectionStatus,
  TableRef,
  ColumnsResponse,
  PlainResult,
  VizResult,
  Script,
  ScriptRunResult,
} from '../types'

const api = axios.create({ baseURL: '/api' })

// ── Connection ────────────────────────────────────────────────────────────────

export async function connectSqlServer(params: {
  host: string
  port: number
  database: string
  username: string
  password: string
  trusted: boolean
}) {
  const { data } = await api.post('/connect/sqlserver', params)
  return data as { ok: boolean; display_name: string; engine: string }
}

export async function connectSQLite(path: string) {
  const { data } = await api.post('/connect/sqlite', { path })
  return data as { ok: boolean; display_name: string; engine: string }
}

export async function disconnect() {
  await api.delete('/disconnect')
}

export async function getStatus() {
  const { data } = await api.get('/status')
  return data as ConnectionStatus
}

export async function getSamples(): Promise<{ name: string; path: string }[]> {
  const { data } = await api.get('/samples')
  return data
}

// ── Schema ────────────────────────────────────────────────────────────────────

export async function getDatabases(): Promise<string[]> {
  const { data } = await api.get('/databases')
  return data
}

export async function getTables(database?: string): Promise<TableRef[]> {
  const { data } = await api.get('/tables', { params: { database } })
  return data
}

export async function getColumns(
  table: string,
  schema = 'dbo',
  database?: string,
): Promise<ColumnsResponse> {
  const { data } = await api.get('/columns', { params: { table, schema, database } })
  return data
}

// ── Query ─────────────────────────────────────────────────────────────────────

export async function runQuery(sql: string): Promise<PlainResult> {
  const { data } = await api.post('/query/run', { sql })
  return data
}

export async function visualizeQuery(sql: string): Promise<VizResult> {
  const { data } = await api.post('/query/visualize', { sql })
  return data
}

// ── Scripts ───────────────────────────────────────────────────────────────────

export async function listScripts(): Promise<Script[]> {
  const { data } = await api.get('/scripts')
  return data
}

export async function uploadScript(file: File, engineHint = ''): Promise<Script> {
  const form = new FormData()
  form.append('file', file)
  form.append('engine_hint', engineHint)
  const { data } = await api.post('/scripts/upload', form)
  return data
}

export async function deleteScript(id: string) {
  await api.delete(`/scripts/${id}`)
}

export async function runScript(id: string): Promise<ScriptRunResult> {
  const { data } = await api.post(`/scripts/${id}/run`)
  return data
}
