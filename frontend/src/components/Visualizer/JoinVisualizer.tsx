import { useState, useEffect, useCallback, useRef, useMemo } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import * as d3 from 'd3'
import type { JoinResult } from '../../types'
import { AnimControls, usePersistedSpeed } from './SortVisualizer'
import './Visualizer.css'

interface Props { result: JoinResult }

// Cap rows per side — keeps total comparisons ≤ MAX_SIDE²
const MAX_SIDE_ROWS = 10
// ms per comparison at 1× speed
const BASE_DELAY = 220

interface Comparison { leftIdx: number; rightIdx: number; isMatch: boolean }

export default function JoinVisualizer({ result }: Props) {
  const leftRows  = result.left_rows.slice(0, MAX_SIDE_ROWS)
  const rightRows = result.right_rows.slice(0, MAX_SIDE_ROWS)
  const L = leftRows.length
  const R = rightRows.length

  // All pairwise comparisons in nested-loop (left outer, right inner) order
  const comparisons = useMemo<Comparison[]>(() => {
    const list: Comparison[] = []
    for (let l = 0; l < L; l++) {
      for (let r = 0; r < R; r++) {
        const isMatch = result.match_pairs.some(
          p => p.left_index === l && p.right_index === r
        )
        list.push({ leftIdx: l, rightIdx: r, isMatch })
      }
    }
    return list
  }, [L, R, result.match_pairs])

  // match_pairs[i] was built in the same nested-loop order → maps directly to merged_rows[i]
  const mergedByKey = useMemo(() => {
    const m = new Map<string, Record<string, unknown>>()
    result.match_pairs.forEach((p, i) => {
      m.set(`${p.left_index}:${p.right_index}`, result.merged_rows[i] as Record<string, unknown>)
    })
    return m
  }, [result.match_pairs, result.merged_rows])

  const maxFrame = comparisons.length
  const [frame, setFrame] = useState(0)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed] = usePersistedSpeed()
  const svgRef       = useRef<SVGSVGElement>(null)
  const leftRef      = useRef<HTMLDivElement>(null)
  const rightRef     = useRef<HTMLDivElement>(null)
  const mergedRef    = useRef<HTMLDivElement>(null)

  const reset = useCallback(() => {
    setFrame(0)
    setPlaying(false)
    leftRef.current?.scrollTo({ top: 0, behavior: 'smooth' })
    rightRef.current?.scrollTo({ top: 0, behavior: 'smooth' })
  }, [])

  // Ticker
  useEffect(() => {
    if (!playing) return
    if (frame >= maxFrame) { setPlaying(false); return }
    const id = setTimeout(() => setFrame(f => f + 1), BASE_DELAY / speed)
    return () => clearTimeout(id)
  }, [playing, frame, speed, maxFrame])

  // Draw SVG connectors
  useEffect(() => {
    const svg = d3.select(svgRef.current)
    svg.selectAll('*').remove()
    if (frame === 0 || !leftRef.current || !rightRef.current) return

    const svgEl  = svgRef.current!
    const svgRect = svgEl.getBoundingClientRect()
    const leftRowEls  = leftRef.current.querySelectorAll<HTMLElement>('tbody tr')
    const rightRowEls = rightRef.current.querySelectorAll<HTMLElement>('tbody tr')

    function drawLine(c: Comparison, isCurrent: boolean) {
      const lEl = leftRowEls[c.leftIdx]
      const rEl = rightRowEls[c.rightIdx]
      if (!lEl || !rEl) return

      const lRect = lEl.getBoundingClientRect()
      const rRect = rEl.getBoundingClientRect()
      const x1 = lRect.right - svgRect.left
      const y1 = lRect.top + lRect.height / 2 - svgRect.top
      const x2 = rRect.left - svgRect.left
      const y2 = rRect.top + rRect.height / 2 - svgRect.top
      const midX = (x1 + x2) / 2
      const midY = (y1 + y2) / 2
      const pathStr = `M${x1},${y1} C${midX},${y1} ${midX},${y2} ${x2},${y2}`

      if (c.isMatch) {
        const p = svg.append('path')
          .attr('d', pathStr)
          .attr('fill', 'none')
          .attr('stroke', 'var(--green)')
          .attr('stroke-width', isCurrent ? 2.5 : 1.5)
          .attr('stroke-opacity', isCurrent ? 0.9 : 0.35)

        if (isCurrent) {
          const len = (p.node() as SVGPathElement).getTotalLength()
          p.attr('stroke-dasharray', len)
            .attr('stroke-dashoffset', len)
            .transition()
            .duration(Math.min(190, (BASE_DELAY / speed) * 0.65))
            .ease(d3.easeLinear)
            .attr('stroke-dashoffset', 0)

          svg.append('text')
            .attr('x', midX).attr('y', midY - 5)
            .attr('text-anchor', 'middle')
            .attr('font-size', 13).attr('fill', 'var(--green)').attr('opacity', 0)
            .text('✓')
            .transition().duration(110).attr('opacity', 1)
        }
      } else if (isCurrent) {
        // Red dashed line for no-match
        svg.append('path')
          .attr('d', pathStr)
          .attr('fill', 'none')
          .attr('stroke', 'var(--red)')
          .attr('stroke-width', 1.5)
          .attr('stroke-opacity', 0.65)
          .attr('stroke-dasharray', '5 4')

        svg.append('text')
          .attr('x', midX).attr('y', midY + 5)
          .attr('text-anchor', 'middle')
          .attr('font-size', 14).attr('fill', 'var(--red)').attr('opacity', 0)
          .text('✕')
          .transition().duration(80).attr('opacity', 0.9)
      }
    }

    // Persistent matched lines from past frames
    comparisons.slice(0, frame - 1).forEach(c => { if (c.isMatch) drawLine(c, false) })

    // Current frame comparison
    const cur = comparisons[frame - 1]
    if (cur) drawLine(cur, true)
  }, [frame, comparisons, speed])

  const cur = frame > 0 && frame <= maxFrame ? comparisons[frame - 1] : null

  // Smoothly scroll the active rows to the centre of each column container
  useEffect(() => {
    if (!cur || !leftRef.current || !rightRef.current) return
    const lRows = leftRef.current.querySelectorAll<HTMLElement>('tbody tr')
    const rRows = rightRef.current.querySelectorAll<HTMLElement>('tbody tr')

    function centerRow(container: HTMLDivElement, rowEl: HTMLElement) {
      const cRect = container.getBoundingClientRect()
      const rRect = rowEl.getBoundingClientRect()
      const delta = rRect.top - cRect.top - container.clientHeight / 2 + rRect.height / 2
      container.scrollBy({ top: delta, behavior: 'smooth' })
    }

    const lEl = lRows[cur.leftIdx]
    const rEl = rRows[cur.rightIdx]
    if (lEl) centerRow(leftRef.current, lEl)
    if (rEl) centerRow(rightRef.current, rEl)
  }, [cur])

  // Accumulated merged rows
  const mergedSoFar = useMemo(() =>
    comparisons
      .slice(0, frame)
      .filter(c => c.isMatch)
      .map(c => mergedByKey.get(`${c.leftIdx}:${c.rightIdx}`))
      .filter((r): r is Record<string, unknown> => r !== undefined),
    [comparisons, frame, mergedByKey]
  )

  // Scroll merged table to show the newest row
  useEffect(() => {
    const el = mergedRef.current
    if (!el || mergedSoFar.length === 0) return
    el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' })
  }, [mergedSoFar.length])

  const joinTypeBadge = result.join_type.toUpperCase().replace(' ', '\u00A0')
  const totalMatches  = result.match_pairs.length

  return (
    <div className="viz-root">
      <div className="viz-header">
        <div className="viz-badge viz-badge-join">{joinTypeBadge} JOIN</div>
        <code className="viz-where-text">{result.on_condition}</code>
        {(L < result.left_rows.length || R < result.right_rows.length) && (
          <span className="viz-cap-note">
            (showing first {L}&thinsp;×&thinsp;{R} rows)
          </span>
        )}
      </div>

      <AnimControls
        playing={playing} frame={frame} maxFrame={maxFrame} speed={speed}
        onPlay={() => { if (frame >= maxFrame) setFrame(0); setPlaying(true) }}
        onPause={() => setPlaying(false)}
        onReset={reset}
        onSpeedChange={setSpeed}
      />

      {/* Side-by-side source tables */}
      <div className="join-side-by-side">
        <svg ref={svgRef} className="join-svg-overlay" style={{ pointerEvents: 'none' }} />

        {/* Left table */}
        <div className="join-table-col" ref={leftRef}>
          <div className="join-table-label">{result.left_alias || result.left_table}</div>
          <table className="data-table">
            <thead>
              <tr>
                {result.left_columns.map(c => (
                  <th key={c.name} className={c.name === result.left_key ? 'sort-key' : ''}>{c.name}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {leftRows.map((row, i) => {
                const isActive = cur?.leftIdx === i
                return (
                  <motion.tr
                    key={i}
                    animate={{
                      opacity: cur && !isActive ? 0.22 : 1,
                      backgroundColor: isActive ? 'rgba(247,201,72,0.13)' : 'transparent',
                    }}
                    transition={{ duration: 0.1 }}
                  >
                    {result.left_columns.map(c => (
                      <td key={c.name}
                        style={isActive && c.name === result.left_key
                          ? { color: 'var(--yellow)', fontWeight: 600 } : {}}
                      >
                        {String(row[c.name] ?? '')}
                      </td>
                    ))}
                  </motion.tr>
                )
              })}
            </tbody>
          </table>
        </div>

        {/* Right table */}
        <div className="join-table-col" ref={rightRef}>
          <div className="join-table-label">{result.right_alias || result.right_table}</div>
          <table className="data-table">
            <thead>
              <tr>
                {result.right_columns.map(c => (
                  <th key={c.name} className={c.name === result.right_key ? 'sort-key' : ''}>{c.name}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rightRows.map((row, i) => {
                const isActive = cur?.rightIdx === i
                const isMatch   = !!(cur?.isMatch   && isActive)
                const isNoMatch = !!(cur && !cur.isMatch && isActive)
                return (
                  <motion.tr
                    key={i}
                    animate={{
                      opacity: cur && !isActive ? 0.22 : 1,
                      backgroundColor: isMatch
                        ? 'rgba(62,207,142,0.16)'
                        : isNoMatch
                          ? 'rgba(247,96,96,0.10)'
                          : 'transparent',
                    }}
                    transition={{ duration: 0.1 }}
                  >
                    {result.right_columns.map(c => (
                      <td key={c.name}
                        style={isActive && c.name === result.right_key
                          ? { color: isMatch ? 'var(--green)' : 'var(--red)', fontWeight: 600 }
                          : {}}
                      >
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

      {/* Growing merged result */}
      {mergedSoFar.length > 0 && (
        <div className="join-merged-wrap">
          <div className="join-table-label">
            Merged — {mergedSoFar.length}&thinsp;/&thinsp;{totalMatches} match{totalMatches !== 1 ? 'es' : ''}
          </div>
          <div className="join-merged-scroll" ref={mergedRef}>
            <table className="data-table">
              <thead>
                <tr>{result.merged_columns.map(c => <th key={c.name}>{c.name}</th>)}</tr>
              </thead>
              <tbody>
                <AnimatePresence>
                  {mergedSoFar.map((row, i) => {
                    const isNewest = i === mergedSoFar.length - 1
                    return (
                      <motion.tr
                        key={i}
                        initial={{ opacity: 0, x: -16, backgroundColor: 'rgba(62,207,142,0.0)' }}
                        animate={isNewest
                          ? {
                              opacity: 1,
                              x: 0,
                              backgroundColor: ['rgba(62,207,142,0.35)', 'rgba(62,207,142,0.35)', 'rgba(62,207,142,0.0)'],
                            }
                          : { opacity: 1, x: 0, backgroundColor: 'rgba(62,207,142,0.0)' }
                        }
                        transition={isNewest
                          ? { duration: 1.2, times: [0, 0.25, 1], ease: 'easeOut' }
                          : { duration: 0.28 }
                        }
                      >
                        {result.merged_columns.map(c => (
                          <td key={c.name}
                            style={isNewest ? { color: 'var(--green)', fontWeight: 600 } : {}}
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
      )}
    </div>
  )
}
