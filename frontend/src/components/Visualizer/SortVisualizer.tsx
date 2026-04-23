import { useState, useEffect, useCallback } from 'react'
import { motion, AnimatePresence, LayoutGroup } from 'framer-motion'
import type { OrderByResult } from '../../types'
import './Visualizer.css'

interface Props { result: OrderByResult }

// ms per row at 1× speed
const BASE_DELAY = 700

export default function SortVisualizer({ result }: Props) {
  const maxFrame = result.sorted_rows.length
  const [frame, setFrame] = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed] = useState(1)

  const reset = useCallback(() => { setFrame(0); setPlaying(false) }, [])

  // Auto-advance ticker
  useEffect(() => {
    if (!playing) return
    if (frame >= maxFrame) { setPlaying(false); return }
    const id = setTimeout(() => setFrame(f => f + 1), BASE_DELAY / speed)
    return () => clearTimeout(id)
  }, [playing, frame, speed, maxFrame])

  // Placed rows = sorted_rows[0..frame-1]
  const placedRows = result.sorted_rows.slice(0, frame)

  // Remaining unsorted rows (with stable original index for keys, duplicate-safe)
  const remaining: { row: typeof result.unsorted_rows[0]; origIdx: number }[] = []
  {
    const consumed = new Map<string, number>()
    placedRows.forEach(r => {
      const k = JSON.stringify(r)
      consumed.set(k, (consumed.get(k) ?? 0) + 1)
    })
    result.unsorted_rows.forEach((r, idx) => {
      const k = JSON.stringify(r)
      const c = consumed.get(k) ?? 0
      if (c > 0) consumed.set(k, c - 1)
      else remaining.push({ row: r, origIdx: idx })
    })
  }

  const isSortKey = (ci: number) => result.sort_key_indices.includes(ci)

  return (
    <div className="viz-root">
      <div className="viz-header">
        <div className="viz-badge">ORDER BY</div>
        <div className="viz-sort-keys">
          Sort key{result.sort_keys.length > 1 ? 's' : ''}:{' '}
          {result.sort_keys.map(k => <code key={k}>{k}</code>)}
        </div>
      </div>

      <AnimControls
        playing={playing} frame={frame} maxFrame={maxFrame} speed={speed}
        onPlay={() => { if (frame >= maxFrame) setFrame(0); setPlaying(true) }}
        onPause={() => setPlaying(false)}
        onReset={reset}
        onSpeedChange={setSpeed}
      />

      <div className="viz-table-wrap">
        <LayoutGroup>
          <table className="data-table">
            <thead>
              <tr>
                {result.columns.map((c, i) => (
                  <th key={c.name} className={isSortKey(i) ? 'sort-key' : ''}>
                    {c.name}{isSortKey(i) ? ' ↕' : ''}
                  </th>
                ))}
              </tr>
            </thead>
            <motion.tbody layout>
              <AnimatePresence>
                {/* Sorted (placed) rows — slide in green */}
                {placedRows.map((row, i) => (
                  <motion.tr
                    key={`p-${i}`}
                    layout
                    initial={{ opacity: 0, x: -24 }}
                    animate={{
                      opacity: 1,
                      x: 0,
                      backgroundColor:
                        i === placedRows.length - 1
                          ? 'rgba(62,207,142,0.14)'
                          : 'rgba(62,207,142,0.04)',
                    }}
                    transition={{ duration: 0.35 }}
                  >
                    {result.columns.map((c, ci) => (
                      <td key={c.name} style={isSortKey(ci) ? { color: 'var(--green)', fontWeight: 600 } : {}}>
                        {String(row[c.name] ?? '')}
                      </td>
                    ))}
                  </motion.tr>
                ))}

                {/* Unsorted remaining rows — dim when sorting has started */}
                {remaining.map(({ row, origIdx }) => (
                  <motion.tr
                    key={`u-${origIdx}`}
                    layout
                    animate={{ opacity: frame > 0 ? 0.28 : 1 }}
                    exit={{ opacity: 0, x: 20, transition: { duration: 0.25 } }}
                    transition={{ duration: 0.2 }}
                  >
                    {result.columns.map((c, ci) => (
                      <td key={c.name} style={isSortKey(ci) && frame === 0 ? { color: 'var(--accent)' } : {}}>
                        {String(row[c.name] ?? '')}
                      </td>
                    ))}
                  </motion.tr>
                ))}
              </AnimatePresence>
            </motion.tbody>
          </table>
        </LayoutGroup>
      </div>
    </div>
  )
}

// ── Shared animation controls (used by all three visualizers) ─────────────────

export function AnimControls({
  playing, frame, maxFrame, speed,
  onPlay, onPause, onReset, onSpeedChange,
}: {
  playing: boolean
  frame: number
  maxFrame: number
  speed: number
  onPlay: () => void
  onPause: () => void
  onReset: () => void
  onSpeedChange: (s: number) => void
}) {
  const done = frame >= maxFrame
  return (
    <div className="viz-controls">
      <button className="btn btn-secondary btn-sm" onClick={onReset}>↺ Reset</button>
      <button
        className={`btn btn-sm ${playing ? 'btn-secondary' : 'btn-primary'}`}
        onClick={playing ? onPause : onPlay}
      >
        {playing ? '⏸ Pause' : done ? '↺ Replay' : '▶ Play'}
      </button>

      <div className="viz-progress" title={`${frame} / ${maxFrame} rows`}>
        <div
          className="viz-progress-bar"
          style={{ width: maxFrame > 0 ? `${(frame / maxFrame) * 100}%` : '0%' }}
        />
      </div>

      <div className="viz-speed-wrap">
        <span className="viz-speed-label">Speed</span>
        <input
          type="range" min="0.25" max="4" step="0.25" value={speed}
          className="viz-speed-slider"
          onChange={e => onSpeedChange(parseFloat(e.target.value))}
        />
        <span className="viz-speed-value">{speed}×</span>
      </div>
    </div>
  )
}
