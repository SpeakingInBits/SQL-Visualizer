import { useState, useEffect, useCallback, useMemo } from 'react'
import { motion, LayoutGroup } from 'framer-motion'
import type { OrderByResult } from '../../types'
import './Visualizer.css'

interface Props { result: OrderByResult }

// ms per sort step at 1× speed
const BASE_DELAY = 800

// Build stable mapping: sortedToOrig[i] = original (unsorted) index for sorted row i
function buildSortedToOrigMap(
  unsorted: Record<string, unknown>[],
  sorted: Record<string, unknown>[]
): number[] {
  const available = unsorted.map((row, idx) => ({ row, idx, used: false }))
  return sorted.map(sr => {
    const key = JSON.stringify(sr)
    const found = available.find(a => !a.used && JSON.stringify(a.row) === key)
    if (found) { found.used = true; return found.idx }
    return 0
  })
}

// Generate selection-sort step states: steps[f] = array of origIdx in display order after f sorts
function buildSortSteps(n: number, sortedToOrig: number[]): number[][] {
  const current = Array.from({ length: n }, (_, i) => i)
  const steps: number[][] = [[...current]]
  for (let i = 0; i < n; i++) {
    const target = sortedToOrig[i]
    const pos = current.indexOf(target)
    if (pos !== i) {
      ;[current[i], current[pos]] = [current[pos], current[i]]
    }
    steps.push([...current])
  }
  return steps
}

export default function SortVisualizer({ result }: Props) {
  const N = result.unsorted_rows.length
  const maxFrame = N

  const [frame, setFrame]   = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed]   = useState(1)

  const reset = useCallback(() => { setFrame(0); setPlaying(false) }, [])

  useEffect(() => {
    if (!playing) return
    if (frame >= maxFrame) { setPlaying(false); return }
    const id = setTimeout(() => setFrame(f => f + 1), BASE_DELAY / speed)
    return () => clearTimeout(id)
  }, [playing, frame, speed, maxFrame])

  // Build sort steps once
  const sortedToOrig = useMemo(
    () => buildSortedToOrigMap(result.unsorted_rows, result.sorted_rows),
    [result]
  )
  const steps = useMemo(
    () => buildSortSteps(N, sortedToOrig),
    [N, sortedToOrig]
  )

  const displayOrder = steps[Math.min(frame, steps.length - 1)]
  const isSortKey = (ci: number) => result.sort_key_indices.includes(ci)

  // The original index of the row that just landed in its sorted position
  const newestOrigIdx = frame > 0 ? sortedToOrig[frame - 1] : -1

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

      {/* Status banner */}
      {frame === 0 && (
        <div className="sort-status-banner">
          Rows shown in original order — press <strong>Play</strong> to sort
        </div>
      )}
      {frame > 0 && frame < maxFrame && (
        <div className="sort-status-banner sort-status-active">
          Sorting… {frame} of {N} rows placed
        </div>
      )}
      {frame >= maxFrame && (
        <div className="sort-status-banner sort-status-done">
          ✓ Sorted — {N} rows in order
        </div>
      )}

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
            <tbody>
              {displayOrder.map((origIdx, position) => {
                const isSorted  = position < frame
                const isNewest  = origIdx === newestOrigIdx && frame > 0
                const row       = result.unsorted_rows[origIdx]
                return (
                  <motion.tr
                    key={origIdx}
                    layout
                    animate={isNewest
                      ? {
                          opacity: 1,
                          backgroundColor: [
                            'rgba(62,207,142,0.45)',
                            'rgba(62,207,142,0.45)',
                            'rgba(62,207,142,0.07)',
                          ],
                        }
                      : {
                          opacity: isSorted || frame === 0 ? 1 : 0.32,
                          backgroundColor: isSorted
                            ? 'rgba(62,207,142,0.07)'
                            : 'transparent',
                        }
                    }
                    transition={isNewest
                      ? { layout: { duration: BASE_DELAY * 0.75 / 1000 / speed }, duration: 1.1, times: [0, 0.3, 1] }
                      : { layout: { duration: BASE_DELAY * 0.75 / 1000 / speed }, duration: 0.25 }
                    }
                  >
                    {result.columns.map((c, ci) => (
                      <td
                        key={c.name}
                        style={
                          isSorted
                            ? { color: 'var(--green)', fontWeight: isSortKey(ci) ? 700 : 400 }
                            : isSortKey(ci) && frame === 0
                              ? { color: 'var(--accent)' }
                              : {}
                        }
                      >
                        {String(row[c.name] ?? '')}
                      </td>
                    ))}
                  </motion.tr>
                )
              })}
            </tbody>
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
