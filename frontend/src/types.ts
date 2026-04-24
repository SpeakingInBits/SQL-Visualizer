// Shared TypeScript types mirroring backend response shapes

export type EngineType = 'sqlserver' | 'sqlite'

export interface ConnectionStatus {
  connected: boolean
  engine?: EngineType
  display_name?: string
}

export interface TableRef {
  schema: string
  name: string
}

export interface ColumnMeta {
  name: string
  data_type?: string
  is_nullable?: boolean
  is_primary_key?: boolean
  is_foreign_key?: boolean
  default?: string | null
}

export interface ForeignKey {
  column: string
  ref_table: string
  ref_column: string
}

export interface ColumnsResponse {
  columns: ColumnMeta[]
  foreign_keys: ForeignKey[]
}

// Query result types
export interface PlainResult {
  viz_type: 'plain'
  type: 'SELECT' | 'DML'
  columns: { name: string }[]
  rows: Record<string, unknown>[]
  rows_affected: number | null
  truncated: boolean
}

export interface OrderByResult {
  viz_type: 'order_by'
  columns: { name: string }[]
  unsorted_rows: Record<string, unknown>[]
  sorted_rows: Record<string, unknown>[]
  sort_key_indices: number[]
  sort_keys: string[]
}

export interface WhereResult {
  viz_type: 'where'
  columns: { name: string }[]
  all_rows: Record<string, unknown>[]
  match_mask: boolean[]
  where_text: string
  conditions: string[]
  condition_results: boolean[][]  // [row_idx][cond_idx]
}

export interface WhereOrderByResult {
  viz_type: 'where_order_by'
  columns: { name: string }[]
  all_rows: Record<string, unknown>[]
  match_mask: boolean[]
  where_text: string
  conditions: string[]
  condition_results: boolean[][]
  // Sort phase data (matched rows only)
  unsorted_rows: Record<string, unknown>[]
  sorted_rows: Record<string, unknown>[]
  sort_key_indices: number[]
  sort_keys: string[]
}

export interface JoinResult {
  viz_type: 'join'
  join_type: string
  left_table: string
  right_table: string
  left_alias: string
  right_alias: string
  left_columns: { name: string }[]
  right_columns: { name: string }[]
  left_rows: Record<string, unknown>[]
  right_rows: Record<string, unknown>[]
  on_condition: string
  left_key: string
  right_key: string
  match_pairs: { left_index: number; right_index: number }[]
  merged_columns: { name: string }[]
  merged_rows: Record<string, unknown>[]
}

export type VizResult = PlainResult | OrderByResult | WhereResult | WhereOrderByResult | JoinResult

// Script types
export interface Script {
  id: string
  name: string
  engine_hint: string
  path: string
  created_at: string
  last_used: string | null
}

export interface ScriptRunResult {
  script_id: string
  results: {
    statement: string
    type: 'SELECT' | 'DML' | 'ERROR'
    rows_affected?: number
    error?: string
    ok: boolean
  }[]
}
