'use client'

import { WaydTooltip } from '@/src/components/common'
// The timeline's, because saving a drawn SVG is the same job whatever drew it: rasterising the vector
// beats screenshotting the DOM, which is what dropped this map's edges when it did.
import {
  downloadSvg,
  downloadSvgAsPng,
} from '@/src/components/common/timeline/render/svg/export-svg'
import { useMessage } from '@/src/components/contexts/messaging'
import useTheme from '@/src/components/contexts/theme/use-theme'
import { DependencyStrength } from '@/src/services/wayd-api'
import {
  FileImageOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
  ShrinkOutlined,
  ZoomInOutlined,
  ZoomOutOutlined,
} from '@ant-design/icons'
import {
  Background,
  ControlButton,
  Controls,
  Handle,
  MarkerType,
  Position,
  ReactFlow,
  useReactFlow,
  type NodeProps,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import { Dropdown, theme } from 'antd'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
import {
  renderDependencyMapSvg,
  type DependencySvgTheme,
} from './dependency-map-svg'
import styles from './dependency-map.module.css'
import type {
  DependencyEdge,
  DependencyEdgeData,
  DependencyGroupData,
  DependencyNode,
  DependencyNodeData,
} from './dependency-neighbourhood'

export interface DependencyMapProps {
  nodes: DependencyNode[]
  edges: DependencyEdge[]
  /** The canvas has no intrinsic height; it fills whatever it is given. */
  height?: number
  /** What a downloaded image is called, without the extension the chosen format supplies. */
  fileStem?: string
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

interface MapControlsProps {
  isFullScreen: boolean
  onToggleFullScreen: () => void
  /** Named after the record the map is about, since a downloads folder has no other context. */
  fileStem: string
  theme: DependencySvgTheme
}

/**
 * The control cluster, with React Flow's own buttons turned off and Ant Design icons in their place.
 *
 * React Flow ships its own SVGs, which sit beside antd's everywhere else in the app at a different
 * weight. Driving zoom and fit ourselves costs one hook — the buttons are the library's, only the
 * glyphs change. It must be a child of <ReactFlow>, which is what puts it inside the flow's context.
 */
const MapControls = ({
  isFullScreen,
  onToggleFullScreen,
  fileStem,
  theme: svgTheme,
}: MapControlsProps) => {
  const { zoomIn, zoomOut, fitView, getNodes, getEdges } =
    useReactFlow<DependencyNode>()
  const [isSaving, setIsSaving] = useState(false)
  const messageApi = useMessage()

  // Drawn from the graph rather than captured from the screen, so what is exported does not depend on
  // where the viewer had panned to, and nothing has to be fitted or restored around the download.
  const saveAs = async (format: 'png' | 'svg') => {
    // A node inside a box carries a position relative to it, and boxes nest, so the whole chain of
    // enclosing boxes is added back to place a node on the page.
    const byId = new Map(getNodes().map((node) => [node.id, node]))
    const absolute = (node: DependencyNode): { x: number; y: number } => {
      const parent = node.parentId ? byId.get(node.parentId) : undefined
      if (!parent) return node.position

      const origin = absolute(parent)

      return { x: origin.x + node.position.x, y: origin.y + node.position.y }
    }

    const svgNodes = getNodes().map((node) => {
      const { x, y } = absolute(node)

      return {
        id: node.id,
        label: (node.data as { label: string }).label,
        x,
        y,
        // Measured by React Flow from the rendered node, so a label that wrapped exports at its real
        // height; the declared size is only a fallback for a node it has not measured yet.
        width: node.measured?.width ?? node.width ?? 180,
        height: node.measured?.height ?? node.height ?? 48,
        isGroup: node.type === 'productGroup',
        isSubject: (node.data as { isSubject?: boolean }).isSubject === true,
      }
    })

    const svgEdges = getEdges().map((edge) => ({
      source: edge.source,
      target: edge.target,
      strength: (edge.data as unknown as DependencyEdgeData).strength,
    }))

    setIsSaving(true)
    try {
      const { svg, width, height } = renderDependencyMapSvg({
        nodes: svgNodes,
        edges: svgEdges,
        theme: svgTheme,
      })

      if (format === 'svg') {
        downloadSvg(svg, `${fileStem}.svg`)
      } else {
        await downloadSvgAsPng(svg, `${fileStem}.png`, { width, height })
      }
    } catch (error) {
      messageApi.error(
        error instanceof Error ? error.message : 'Could not save the map.',
      )
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Controls showZoom={false} showFitView={false} showInteractive={false}>
      <WaydTooltip title="Zoom In" placement="right">
        <ControlButton aria-label="Zoom in" onClick={() => zoomIn()}>
          <ZoomInOutlined />
        </ControlButton>
      </WaydTooltip>
      <WaydTooltip title="Zoom Out" placement="right">
        <ControlButton aria-label="Zoom out" onClick={() => zoomOut()}>
          <ZoomOutOutlined />
        </ControlButton>
      </WaydTooltip>
      <WaydTooltip title="Fit View" placement="right">
        <ControlButton
          aria-label="Fit view"
          onClick={() => fitView({ padding: 0.2, maxZoom: 1 })}
        >
          <ShrinkOutlined />
        </ControlButton>
      </WaydTooltip>
      <WaydTooltip
        title={isFullScreen ? 'Exit Fullscreen' : 'Fullscreen'}
        placement="right"
      >
        <ControlButton
          aria-label={isFullScreen ? 'Exit fullscreen' : 'Fullscreen'}
          onClick={onToggleFullScreen}
        >
          {isFullScreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
        </ControlButton>
      </WaydTooltip>
      <Dropdown
        trigger={['click']}
        menu={{
          items: [
            // PNG first as the safe default: SVG will not insert into Google Slides or Docs.
            { key: 'png', label: 'PNG image', onClick: () => saveAs('png') },
            { key: 'svg', label: 'SVG (vector)', onClick: () => saveAs('svg') },
          ],
        }}
      >
        <WaydTooltip title="Save as Image" placement="right">
          <ControlButton aria-label="Save as image" disabled={isSaving}>
            <FileImageOutlined />
          </ControlButton>
        </WaydTooltip>
      </Dropdown>
    </Controls>
  )
}

/**
 * Draws a dependency graph: products as nodes, links as edges.
 *
 * It takes the graph rather than the dependencies, so the same canvas serves a product's one-hop
 * neighbourhood and, later, a map of the whole catalog.
 */
const DependencyMap = ({
  nodes,
  edges,
  height = 320,
  fileStem = 'dependency-map',
}: DependencyMapProps) => {
  const { currentMode } = useTheme()
  const { token } = theme.useToken()
  const router = useRouter()
  const [isFullScreen, setIsFullScreen] = useState(false)
  const refit = useRef<(() => void) | null>(null)

  // Resolved from the tokens at export time: a downloaded file cannot read a CSS variable, and it
  // should look like the theme it was taken from.
  const svgTheme: DependencySvgTheme = {
    background: token.colorBgContainer,
    nodeFill: token.colorBgElevated,
    nodeStroke: token.colorBorder,
    nodeText: token.colorText,
    subjectFill: token.colorPrimaryBg,
    subjectStroke: token.colorPrimary,
    groupFill: token.colorFillQuaternary,
    groupStroke: token.colorBorder,
    groupText: token.colorTextSecondary,
    hardStroke: token.colorTextSecondary,
    softStroke: token.colorTextQuaternary,
    fontFamily: token.fontFamily,
    fontSize: token.fontSizeSM,
    borderRadius: token.borderRadius,
  }

  // Fullscreen here fills the browser viewport through a fixed overlay, as the timeline's does.
  // Native requestFullscreen takes over the whole monitor, which is not what a card on a page wants.
  useEffect(() => {
    if (!isFullScreen) return

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsFullScreen(false)
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [isFullScreen])

  // The canvas keeps its zoom across the resize, so a graph fitted to a card sits in the corner of the
  // overlay until it is refitted. Deferred a frame, because the new size is not laid out yet.
  useEffect(() => {
    const frame = requestAnimationFrame(() => refit.current?.())
    return () => cancelAnimationFrame(frame)
  }, [isFullScreen])

  // The arrow marker's colour is written as an SVG attribute, where a CSS variable would not resolve.
  const strokeFor = (strength: DependencyStrength) =>
    strength === DependencyStrength.Hard
      ? token.colorTextSecondary
      : token.colorTextQuaternary

  const styledEdges = edges.map((edge) => ({
    ...edge,
    // Curves rather than right angles: orthogonal routing sends every edge into the same corridor, so
    // six links arriving at one product merge into a single trunk and you cannot tell which line is
    // hard and which is soft. Each curve keeps its own path.
    type: 'default',
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
    <div
      className={`${styles.surface} ${isFullScreen ? styles.fullscreen : ''}`}
      style={isFullScreen ? undefined : { height }}
    >
      <ReactFlow
        className={styles.canvas}
        nodes={nodes}
        edges={styledEdges}
        nodeTypes={nodeTypes}
        onInit={(instance) => {
          refit.current = () => instance.fitView({ padding: 0.2, maxZoom: 1 })
        }}
        // React Flow turns pointer events off for a node that is not selectable, draggable or
        // connectable and has no click handler — which left the links inside the nodes dead to the
        // mouse. Handling the click is also what makes the whole node clickable, not just its anchor.
        onNodeClick={(event, node) => {
          if ((event.target as HTMLElement).closest('a')) return

          const { productKey } = node.data as DependencyNodeData
          router.push(productLink(productKey))
        }}
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
        {/* Fullscreen joins zoom and fit in the same stack rather than floating in its own corner:
            they are all "how I am looking at this", and one control cluster is one thing to find. */}
        <MapControls
          isFullScreen={isFullScreen}
          onToggleFullScreen={() => setIsFullScreen((open) => !open)}
          fileStem={fileStem}
          theme={svgTheme}
        />
      </ReactFlow>
    </div>
  )
}

export default DependencyMap
