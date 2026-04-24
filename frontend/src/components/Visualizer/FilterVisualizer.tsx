import { useState, useEffect, useCallback, useMemo } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import type { WhereResult } from '../../types'
import { AnimControls, usePersistedSpeed } from './SortVisualizer'
import './Visualizer.css'

interface Props { result: WhereResult }

// ms per phase at 1× speed
const SCAN_DELAY   = 420   // time spent highlighting a row before first condition
const COND_DELAY   = 520   // time per condition badge
const SETTLE_DELAY = 300   // brief pause after last condition before moving on

export default function FilterVisualizer({ result }: Props) {
  const N = result.all_rows.length
  const conditions = result.conditions?.length > 0 ? result.conditions : [result.where_text]
  const C = conditions.length

  // Frame encoding:
  //   Each row uses (1 + C) sub-frames:
  //     sub 0       → row highlighted (scanning)
  //     sub 1..C    → condition[sub-1] badge shown
  //   maxFrame = N * (1 + C)  → all done, non-matches can exit
  const FPR = 1 + C
  const maxFrame = N * FPR

  const [frame, setFrame]   = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed]   = usePersistedSpeed()

  const reset = useCallback(() => { setFrame(0); setPlaying(false) }, [])

  // Derive current position from frame
  const isDone   = frame >= maxFrame
  const rowIdx   = isDone ? N : frame === 0 ? -1 : Math.floor((frame - 1) / FPR)
  const subIdx   = isDone ? -1 : frame === 0 ? -1 : (frame - 1) % FPR
  // subIdx 0 = scan highlight; subIdx 1..C = condition[subIdx-1] visible

  // Variable-delay ticker
  useEffect(() => {
    if (!playing) return
    if (frame >= maxFrame) { setPlaying(false); return }
    const sub = frame === 0 ? -1 : (frame - 1) % FPR
    let delay: number
    if (sub === 0)         delay = SCAN_DELAY / speed
    else if (sub === C)    delay = SETTLE_DELAY / speed   // last condition → next row
    else                   delay = COND_DELAY / speed
    const id = setTimeout(() => setFrame(f => f + 1), delay)
    return () => clearTimeout(id)
  }, [playing, frame, speed, maxFrame, FPR, C])

  // Rows that have been fully evaluated (all conditions shown for them)
  // A row at index i is "settled" once rowIdx > i (we've moved past it)
  function getRowState(i: number): 'pending' | 'scanning' | 'evaluating' | 'matched' | 'rejected' {
    if (isDone)      return result.match_mask[i] ? 'matched' : 'rejected'
    if (i > rowIdx)  return 'pending'
    if (i < rowIdx)  return result.match_mask[i] ? 'matched' : 'rejected'
    // i === rowIdx: currently being evaluated
    return subIdx === 0 ? 'scanning' : 'evaluating'
  }

  // Results so far (rows that passed and are settled)
  const matchedSoFar = useMemo(() => {
    const cutoff = isDone ? N : rowIdx
    const rows: { row: Record<string, unknown>; origIdx: number }[] = []
    for (let i = 0; i < cutoff; i++) {
      if (result.match_mask[i]) rows.push({ row: result.all_rows[i], origIdx: i })
    }
    return rows
  }, [frame, isDone, rowIdx, N, result])

  const matchCount = result.match_mask.filter(Boolean).length

  return (
    <div className="viz-root">
      <div className="viz-header">
        <div className="viz-badge viz-badge-where">WHERE</div>
        <code className="viz-where-text">{result.where_text}</code>
      </div>

      <AnimControls
        playing={playing} frame={frame} maxFrame={maxFrame} speed={speed}
        onPlay={() => { if (frame >= maxFrame) setFrame(0); setPlaying(true) }}
        onPause={() => setPlaying(false)}
        onReset={reset}
        onSpeedChange={setSpeed}
      />

      {/* Condition evaluation card */}
      <AnimatePresence>
        {rowIdx >= 0 && rowIdx < N && (
          <motion.div
            className="filter-eval-card"
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.18 }}
          >
            <span className="filter-eval-label">Row {rowIdx + 1} of {N}</span>
            {subIdx === 0 ? (
              <span className="filter-eval-scanning">scanning…</span>
            ) : (
              <div className="filter-eval-conditions">
                {conditions.slice(0, subIdx).map((cond, ci) => {
                  const passed = result.condition_results?.[rowIdx]?.[ci] ?? false
                  return (
                    <motion.div
                      key={ci}
                      className="filter-eval-row"
                      initial={{ opacity: 0, x: -10 }}
                      animate={{ opacity: 1, x: 0 }}
                      transition={{ duration: 0.16 }}
                    >
                      <code className="filter-cond-text">{cond}</code>
                      <span className={`filter-cond-badge ${passed ? 'badge-true' : 'badge-false'}`}>
                        {passed ? '✓ TRUE' : '✗ FALSE'}
                      </span>
                    </motion.div>
                  )
                })}
              </div>
            )}
          </motion.div>
        )}
      </AnimatePresence>

      <div className="filter-layout">
        {/* Left: all rows */}
        <div className="filter-table-col">
          <div className="join-table-label">All Rows ({N})</div>
          <div className="viz-table-wrap">
            <table className="data-table">
              <thead>
                <tr>{result.columns.map(c => <th key={c.name}>{c.name}</th>)}</tr>
              </thead>
              <tbody>
                {result.all_rows.map((row, i) => {
                  const state = getRowState(i)
                  const isActive = state === 'scanning' || state === 'evaluating'
                  return (
                    <motion.tr
                      key={i}
                      animate={{
                        opacity: state === 'rejected' ? 0.18 : 1,
                        backgroundColor:
                          isActive      ? 'rgba(247,201,72,0.16)' :
                          state === 'matched'  ? 'rgba(62,207,142,0.07)' :
                          'transparent',
                      }}
                      transition={{ duration: 0.2 }}
                    >
                      {result.columns.map(c => (
                        <td key={c.name} style={
                          isActive             ? { color: 'var(--yellow)' } :
                          state === 'matched'  ? { color: 'var(--green)'  } :
                          {}
                        }>
                          {String(row[c.name] ?? '')}
                        </td>
                      ))}
                    </motion.tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right: results */}
        {(matchedSoFar.length > 0 || isDone) && (
          <div className="filter-table-col">
            <div className="join-table-label">
              Results — {matchedSoFar.length}{isDone ? ` / ${matchCount}` : ''}
            </div>
            <div className="viz-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>{result.columns.map(c => <th key={c.name}>{c.name}</th>)}</tr>
                </thead>
                <tbody>
                  <AnimatePresence>
                    {matchedSoFar.map(({ row, origIdx }, i) => {
                      const isNewest = i === matchedSoFar.length - 1 && !isDone
                      return (
                        <motion.tr
                          key={origIdx}
                          initial={{ opacity: 0, x: 18 }}
                          animate={{
                            opacity: 1,
                            x: 0,
                            backgroundColor: isNewest
                              ? ['rgba(62,207,142,0.30)', 'rgba(62,207,142,0.30)', 'rgba(62,207,142,0.0)']
                              : 'rgba(62,207,142,0.0)',
                          }}
                          transition={isNewest
                            ? { duration: 1.1, times: [0, 0.25, 1] }
                            : { duration: 0.3 }
                          }
                        >
                          {result.columns.map(c => (
                            <td key={c.name} style={isNewest ? { color: 'var(--green)', fontWeight: 600 } : {}}>
                              {String(row[c.name] ?? '')}
                            </td>
                          ))}
                        </motion.tr>
                      )
                    })}
                  </AnimatePresence>
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {isDone && (
        <div className="viz-stats">
          <span className="badge badge-green">{matchCount} / {N} rows matched</span>
        </div>
      )}
    </div>
  )
}
