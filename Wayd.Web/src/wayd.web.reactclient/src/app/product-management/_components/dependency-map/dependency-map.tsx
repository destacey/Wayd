'use client'

import useTheme from '@/src/components/contexts/theme/use-theme'
import { DependencyStrength } from '@/src/services/wayd-api'
import {
  Background,
  Controls,
  Handle,
  MarkerType,
  Position,
  ReactFlow,
  type NodeProps,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import { theme } from 'antd'
import Link from 'next/link'
import styles from './dependency-map.module.css'
import type {
  DependencyEdge,
  DependencyGroupData,
  DependencyNode,
  DependencyNodeData,
} from './dependency-neighbourhood'

export interface DependencyMapProps {
  nodes: DependencyNode[]
  edges: DependencyEdge[]
  /** The canvas has no intrinsic height; it fills whatever it is given. */
  height?: number
}

const productLink = (productKey: number) =>
  `/product-management/products/${productKey}`

const ProductNode = ({ data }: NodeProps<DependencyNode>) => {
  const node = data as DependencyNodeData

  return (
    <div
      className={`${styles.node} ${node.isSubject ? styles.subject : ''}`}
      data-testid="dependency-map-node"
    >
      <Handle
        type="target"
        position={Position.Left}
        className={styles.handle}
      />
      <Link
        href={productLink(node.productKey)}
        className={`${styles.label} ${styles.link}`}
        title={node.label}
      >
        {node.label}
      </Link>
      <Handle
        type="source"
        position={Position.Right}
        className={styles.handle}
      />
    </div>
  )
}

/**
 * The box a product's own links and its descendants' links are drawn inside.
 *
 * Containment is not a dependency, so it is a box rather than another edge: an edge would put "is part
 * of" and "relies on" in the same visual language, which is the confusion the tree and this map exist to
 * keep apart.
 */
const ProductGroupNode = ({ data }: NodeProps<DependencyNode>) => {
  const group = data as DependencyGroupData

  return (
    <div className={styles.group} data-testid="dependency-map-group">
      <Link href={productLink(group.productKey)} className={styles.groupLabel}>
        {group.label}
      </Link>
    </div>
  )
}

const nodeTypes = { product: ProductNode, productGroup: ProductGroupNode }

/**
 * Draws a dependency graph: products as nodes, links as edges.
 *
 * It takes the graph rather than the dependencies, so the same canvas serves a product's one-hop
 * neighbourhood and, later, a map of the whole catalog.
 */
const DependencyMap = ({ nodes, edges, height = 320 }: DependencyMapProps) => {
  const { currentMode } = useTheme()
  const { token } = theme.useToken()

  // The arrow marker's colour is written as an SVG attribute, where a CSS variable would not resolve.
  const strokeFor = (strength: DependencyStrength) =>
    strength === DependencyStrength.Hard
      ? token.colorTextSecondary
      : token.colorTextQuaternary

  const styledEdges = edges.map((edge) => ({
    ...edge,
    type: 'smoothstep',
    style: {
      stroke: strokeFor(edge.data!.strength),
      strokeWidth: edge.data!.strength === DependencyStrength.Hard ? 2 : 1.5,
      strokeDasharray:
        edge.data!.strength === DependencyStrength.Soft ? '6 4' : undefined,
    },
    markerEnd: {
      type: MarkerType.ArrowClosed,
      width: 18,
      height: 18,
      color: strokeFor(edge.data!.strength),
    },
  }))

  return (
    <div className={styles.surface} style={{ height }}>
      <ReactFlow
        className={styles.canvas}
        nodes={nodes}
        edges={styledEdges}
        nodeTypes={nodeTypes}
        colorMode={currentMode === 'light' ? 'light' : 'dark'}
        fitView
        // Capped at 1: a two-node map would otherwise be blown up to fill the canvas, which makes the
        // same product read as a different size on every page.
        fitViewOptions={{ padding: 0.2, maxZoom: 1 }}
        // Read-only: the layout is derived from the data, so a moved node would say something the
        // record does not, and nothing persists it.
        nodesDraggable={false}
        nodesConnectable={false}
        edgesFocusable={false}
        elementsSelectable={false}
        minZoom={0.3}
        maxZoom={1.5}
      >
        <Background gap={16} />
        <Controls showInteractive={false} />
      </ReactFlow>
    </div>
  )
}

export default DependencyMap
