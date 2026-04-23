import type { VizResult } from '../../types'
import SortVisualizer from './SortVisualizer'
import FilterVisualizer from './FilterVisualizer'
import JoinVisualizer from './JoinVisualizer'

interface Props {
  result: VizResult
}

export default function Visualizer({ result }: Props) {
  switch (result.viz_type) {
    case 'order_by':
      return <SortVisualizer result={result} />
    case 'where':
      return <FilterVisualizer result={result} />
    case 'join':
      return <JoinVisualizer result={result} />
    default:
      // 'plain' — show simple table (already shown in ResultsTable, but Visualizer tab may show this)
      return (
        <div style={{ padding: 16, color: 'var(--text-dim)', fontSize: 13 }}>
          No visualisation available for this query type. Use the Results tab.
        </div>
      )
  }
}
