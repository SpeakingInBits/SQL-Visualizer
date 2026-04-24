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
    case 'where_order_by':
      return <FilterVisualizer result={result} />
    case 'join':
      return <JoinVisualizer result={result} />
    default:
      return (
        <div style={{ padding: 16, color: 'var(--text-dim)', fontSize: 13 }}>
          No visualisation available for this query type. Use the Results tab.
        </div>
      )
  }
}
