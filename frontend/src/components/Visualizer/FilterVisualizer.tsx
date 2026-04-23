import { useState, useEffect, useCallback } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import type { WhereResult } from '../../types'
import { AnimControls } from './SortVisualizer'
import './Visualizer.css'

interface Props { result: WhereResult }

// ms per row at 1× speed
const BASE_DELAY = 500

export default function FilterVisualizer({ result }: Props) {
  const N = result.all_rows.length
  // frame 0..N-1: scanning cursor on row `frame`
  // frame N: all evaluated — non-matches exit via AnimatePresence
  const maxFrame = N
  const [frame, setFrame] = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed] = useState(1)

  const reset = useCallback(() => { setFrame(0); setPlaying(false) }, [])

  useEffect(() => {
    if (!playing) return
    if (frame >= maxFrame) { setPlaying(false); return }
    const id = setTimeout(() => setFrame(f => f + 1), BASE_DELAY / speed)
    return () => clearTimeout(id)
  }, [playing, frame, speed, maxFrame])

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

      <div className="viz-stats">
        {frame >= maxFrame && (
          <span className="badge badge-green">
            {matchCount} / {N} rows match
          </span>
        )}
      </div>

      <div className="viz-table-wrap">
        <table className="data-table">
          <thead>
            <tr>{result.columns.map(c => <th key={c.name}>{c.name}</th>)}</tr>
          </thead>
          <tbody>
            <AnimatePresence>
              {result.all_rows.map((row, i) => {
                const matched = result.match_mask[i]
                const isScanning = i === frame && frame < maxFrame
                const isEvaluated = i < frame

                // Once all rows evaluated, non-matches exit
                if (frame >= maxFrame && !matched) return null

                return (
                  <motion.tr
                    key={i}
                    initial={false}
                    animate={{
                      opacity: isEvaluated && !matched && frame < maxFrame ? 0.18 : 1,
                      backgroundColor: isScanning
                        ? 'rgba(247,201,72,0.13)'
                        : isEvaluated && matched
                          ? 'rgba(62,207,142,0.08)'
                          : 'transparent',
                    }}
                    exit={{ opacity: 0, x: -20, transition: { duration: 0.28 } }}
                    transition={{ duration: 0.22 }}
                  >
                    {result.columns.map(c => (
                      <td
                        key={c.name}
                        style={
                          isScanning
                            ? { color: 'var(--yellow)' }
                            : isEvaluated && matched
                              ? { color: 'var(--green)' }
                              : {}
                        }
                      >
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
  )
}
