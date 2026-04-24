import { useState, useEffect, useCallback, useMemo } from 'react'
import { motion, AnimatePresence, LayoutGroup } from 'framer-motion'
import type { WhereResult, WhereOrderByResult } from '../../types'
import { AnimControls, usePersistedSpeed, buildSortedToOrigMap, buildSortSteps } from './SortVisualizer'
import './Visualizer.css'

type Props = { result: WhereResult | WhereOrderByResult }

// ms per phase at 1× speed
const SCAN_DELAY   = 420
const COND_DELAY   = 520
const SETTLE_DELAY = 300
const SORT_DELAY   = 800

export default function FilterVisualizer({ result }: Props) {
  const N = result.all_rows.length
  const conditions = result.conditions?.length > 0 ? result.conditions : [result.where_text]
  const C = conditions.length
  const FPR = 1 + C
  const whereMaxFrame = N * FPR

  // Sort phase data (only when viz_type === 'where_order_by')
  const sortData = result.viz_type === 'where_order_by' ? result : null
  const sortN = sortData ? sortData.unsorted_rows.length : 0
  // +1 because sortFrame goes 0..sortN (sortN+1 states, like standalone SortVisualizer)
  const totalMaxFrame = whereMaxFrame + sortN + (sortData ? 1 : 0)

  const [frame, setFrame]     = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed]     = usePersistedSpeed()

  const reset = useCallback(() => { setFrame(0); setPlaying(false) }, [])

  // WHERE phase position
  const whereIsDone = frame >= whereMaxFrame
  const rowIdx = whereIsDone ? N : frame === 0 ? -1 : Math.floor((frame - 1) / FPR)
  const subIdx = whereIsDone ? -1 : frame === 0 ? -1 : (frame - 1) % FPR

  // Sort phase position — sortFrame is 0-based: 0=initial unsorted, sortN=all placed
  const inSortPhase = sortData !== null && frame > whereMaxFrame
  const sortFrame   = inSortPhase ? frame - whereMaxFrame - 1 : 0
  const allDone     = frame >= totalMaxFrame

  // Combined ticker with variable delay
  useEffect(() => {
    if (!playing) return
    if (frame >= totalMaxFrame) { setPlaying(false); return }

    // Auto-pause once between phases so the user sees the filter results
    // before the sort animation begins. The ref prevents re-pausing when
    // the user explicitly clicks Play from this frame.
    let delay: number
    if (frame < whereMaxFrame) {
      const sub = frame === 0 ? -1 : (frame - 1) % FPR
      if (sub === 0)       delay = SCAN_DELAY / speed
      else if (sub === C)  delay = SETTLE_DELAY / speed
      else                 delay = COND_DELAY / speed
    } else if (frame === whereMaxFrame) {
      // Brief pause at phase boundary so the user sees the filter result
      // before the sort starts — then it continues automatically
      delay = (SORT_DELAY * 2) / speed
    } else {
      delay = SORT_DELAY / speed
    }
    const id = setTimeout(() => setFrame(f => f + 1), delay)
    return () => clearTimeout(id)
  }, [playing, frame, speed, totalMaxFrame, whereMaxFrame, FPR, C])

  function getRowState(i: number): 'pending' | 'scanning' | 'evaluating' | 'matched' | 'rejected' {
    if (whereIsDone) return result.match_mask[i] ? 'matched' : 'rejected'
    if (i > rowIdx)  return 'pending'
    if (i < rowIdx)  return result.match_mask[i] ? 'matched' : 'rejected'
    return subIdx === 0 ? 'scanning' : 'evaluating'
  }

  const matchedSoFar = useMemo(() => {
    const cutoff = whereIsDone ? N : rowIdx
    const rows: { row: Record<string, unknown>; origIdx: number }[] = []
    for (let i = 0; i < cutoff; i++) {
      if (result.match_mask[i]) rows.push({ row: result.all_rows[i], origIdx: i })
    }
    return rows
  }, [frame, whereIsDone, rowIdx, N, result])

  const matchCount = result.match_mask.filter(Boolean).length

  // Sort phase helpers
  const sortedToOrig = useMemo(
    () => sortData ? buildSortedToOrigMap(sortData.unsorted_rows, sortData.sorted_rows) : [],
    [sortData]
  )
  const sortSteps = useMemo(
    () => sortData ? buildSortSteps(sortN, sortedToOrig) : [],
    [sortData, sortN, sortedToOrig]
  )
  const sortDisplayOrder = inSortPhase
    ? sortSteps[Math.min(sortFrame, sortSteps.length - 1)]
    : []
  // newestSortOrigIdx: the row most recently placed into its sorted position
  const newestSortOrigIdx = sortFrame > 0 ? sortedToOrig[sortFrame - 1] : -1
  const isSortKey = (ci: number) => sortData?.sort_key_indices.includes(ci) ?? false

  return (
    <div className="viz-root">
      <div className="viz-header">
        <div className="viz-badge viz-badge-where">WHERE</div>
        <code className="viz-where-text">{result.where_text}</code>
        {sortData && (
          <>
            {inSortPhase && <div className="viz-badge" style={{ marginLeft: 4 }}>ORDER BY</div>}
            <div className="viz-sort-keys" style={{ marginLeft: 4 }}>
              {sortData.sort_keys.map(k => <code key={k}>{k}</code>)}
            </div>
          </>
        )}
      </div>

      <AnimControls
        playing={playing} frame={frame} maxFrame={totalMaxFrame} speed={speed}
        onPlay={() => { if (frame >= totalMaxFrame) setFrame(0); setPlaying(true) }}
        onPause={() => setPlaying(false)}
        onReset={reset}
        onSpeedChange={setSpeed}
        onStepBack={() => { setPlaying(false); setFrame(f => Math.max(0, f - 1)) }}
        onStepForward={() => { setPlaying(false); setFrame(f => Math.min(totalMaxFrame, f + 1)) }}
      />

      {/* Condition evaluation card — only during WHERE phase */}
      <AnimatePresence>
        {rowIdx >= 0 && rowIdx < N && !whereIsDone && (
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

      {/* Sort phase status banner */}
      {sortData && whereIsDone && (
        <div className={`sort-status-banner ${allDone ? 'sort-status-done' : inSortPhase ? 'sort-status-active' : 'sort-status-ready'}`}>
          {allDone
            ? `✓ Sorted — ${matchCount} rows in order`
            : inSortPhase
            ? `Sorting matched rows… ${sortFrame} of ${sortN} placed`
            : `Filter complete — ${matchCount} row${matchCount !== 1 ? 's' : ''} matched. Sorting next…`}
        </div>
      )}

      <div className="filter-layout">
        {/* Left: all rows (WHERE phase) */}
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
                          isActive             ? 'rgba(247,201,72,0.16)' :
                          state === 'matched'  ? 'rgba(62,207,142,0.07)' :
                          'transparent',
                      }}
                      transition={{ duration: 0.2 }}
                    >
                      {result.columns.map(c => (
                        <td key={c.name} style={
                          isActive            ? { color: 'var(--yellow)' } :
                          state === 'matched' ? { color: 'var(--green)'  } :
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

        {/* Right: Results (WHERE phase) → Sort animation (sort phase) */}
        {(matchedSoFar.length > 0 || whereIsDone) && (
          <div className="filter-table-col">
            <div className="join-table-label">
              {inSortPhase || (whereIsDone && sortData)
                ? `Sorted Results${allDone ? ` — ${matchCount} rows` : ''}`
                : `Results — ${matchedSoFar.length}${whereIsDone ? ` / ${matchCount}` : ''}`}
            </div>
            <div className="viz-table-wrap">
              {/* WHERE phase: growing results table */}
              {!inSortPhase && (
                <table className="data-table">
                  <thead>
                    <tr>{result.columns.map(c => <th key={c.name}>{c.name}</th>)}</tr>
                  </thead>
                  <tbody>
                    <AnimatePresence>
                      {matchedSoFar.map(({ row, origIdx }, i) => {
                        const isNewest = i === matchedSoFar.length - 1 && !whereIsDone
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
              )}

              {/* Sort phase: matched rows being physically sorted */}
              {inSortPhase && sortData && (
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
                      {sortDisplayOrder.map((origIdx, position) => {
                        const isSorted  = position < sortFrame
                        const isNewest  = origIdx === newestSortOrigIdx && sortFrame > 0
                        const row       = sortData.unsorted_rows[origIdx]
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
                                  opacity: isSorted || sortFrame === 0 ? 1 : 0.32,
                                  backgroundColor: isSorted
                                    ? 'rgba(62,207,142,0.07)'
                                    : 'transparent',
                                }
                            }
                            transition={isNewest
                              ? { layout: { duration: SORT_DELAY * 0.75 / 1000 / speed }, duration: 1.1, times: [0, 0.3, 1] }
                              : { layout: { duration: SORT_DELAY * 0.75 / 1000 / speed }, duration: 0.25 }
                            }
                          >
                            {result.columns.map((c, ci) => (
                              <td
                                key={c.name}
                                style={isSorted
                                  ? { color: 'var(--green)', fontWeight: isSortKey(ci) ? 700 : 400 }
                                  : isSortKey(ci) ? { color: 'var(--accent)' }
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
              )}
            </div>
          </div>
        )}
      </div>

      {whereIsDone && !sortData && (
        <div className="viz-stats">
          <span className="badge badge-green">{matchCount} / {N} rows matched</span>
        </div>
      )}
    </div>
  )
}
