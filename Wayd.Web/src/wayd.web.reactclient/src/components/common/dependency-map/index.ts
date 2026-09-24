// The canvas itself is not exported here: it pulls in React Flow and its stylesheet, so callers load it
// on demand from './dependency-map' rather than through this barrel.
export type { DependencyMapProps } from './dependency-map'
export { default as DependencyMapLegend } from './dependency-map-legend'
export type { DependencyMapLegendItem } from './dependency-map-legend'
export type { DependencyEdgeStyle } from './dependency-map-svg'
export { buildDependencyNeighbourhood } from './dependency-neighbourhood'
export {
  revealOverflow,
  toggleExpansion,
  useDependencyMapExpansions,
} from './use-dependency-map-expansions'
export type { DependencyMapExpansionState } from './use-dependency-map-expansions'
export type {
  DependencyEdge,
  DependencyExpansion,
  DependencyExpansions,
  DependencyFarSide,
  DependencyMapLink,
  DependencyMapLinks,
  DependencyMapRecord,
  DependencyNeighbourhood,
  DependencyNode,
  DependencyNodeSide,
} from './dependency-neighbourhood'
