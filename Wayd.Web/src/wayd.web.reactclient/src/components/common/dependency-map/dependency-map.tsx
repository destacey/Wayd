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
import {
  FileImageOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
  MinusOutlined,
  PlusOutlined,
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
  Panel,
  Position,
  ReactFlow,
  useReactFlow,
  useStore,
  type NodeProps,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import { Button, Dropdown, theme } from 'antd'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react'
import {
  renderDependencyMapSvg,
  type DependencyEdgeStyle,
  type DependencySvgTheme,
} from './dependency-map-svg'
import styles from './dependency-map.module.css'
import {
  DEPENDENCY_HANDLES,
  type DependencyEdge,
  type DependencyEdgeData,
  type DependencyExpansionStatus,
  type DependencyFarSide,
  type DependencyGroupData,
  type DependencyNode,
  type DependencyNodeData,
  type DependencyOverflowData,
} from './dependency-neighbourhood'

export interface DependencyMapProps {
  nodes: DependencyNode[]
  edges: DependencyEdge[]
  /**
   * How each edge variant is drawn, with colours already resolved from the theme: the arrow marker and
   * the exported file both need a real colour, where a CSS variable would not resolve.
   */
  edgeStyles: Record<string, DependencyEdgeStyle>
  /**
   * What the expand button on a collapsed record says, per side, since only the caller knows what
   * following a link outward means.
   */
  expandTooltips?: Record<DependencyFarSide, (label: string) => string>
  /** The canvas has no intrinsic height; it fills whatever it is given. */
  height?: number
  /** What a downloaded image is called, without the extension the chosen format supplies. */
  fileStem?: string
  /**
   * Controls that change what is drawn, in the canvas's top-right corner. On the canvas rather than
   * around it, so they stay in reach in fullscreen.
   */
  filters?: ReactNode
  /** Shown in the canvas when a filter leaves nothing to draw. */
  emptyText?: ReactNode
  /** Expands or collapses a record off to one side. Without it, nothing on the map can be expanded. */
  onToggleExpansion?: (side: DependencyFarSide, recordId: string) => void
  /** Draws the records a count left off: an expansion's, or the subject's own when `ownerId` is null. */
  onShowAll?: (side: DependencyFarSide, ownerId: string | null) => void
  /**
   * Opens a record in place, typically in a drawer, instead of navigating to it. Without it, clicking a
   * record goes to its page. Never called for the subject, whose page the reader is already on.
   */
  onOpenRecord?: (node: DependencyNodeData) => void
}

interface DependencyMapActions {
  onToggleExpansion?: DependencyMapProps['onToggleExpansion']
  onShowAll?: DependencyMapProps['onShowAll']
  onOpenRecord?: DependencyMapProps['onOpenRecord']
  expandTooltips?: DependencyMapProps['expandTooltips']
}

// Nodes are rendered by React Flow from a type map defined once, outside the component, so they reach
// the map's handlers through context rather than props.
const DependencyMapActionsContext = createContext<DependencyMapActions>({})

const expansionTooltip = (
  status: DependencyExpansionStatus,
  side: DependencyFarSide,
  label: string,
  expandTooltips: DependencyMapProps['expandTooltips'],
) => {
  switch (status) {
    case 'collapsed':
      return expandTooltips?.[side](label) ?? `Expand ${label}`
    case 'loading':
      return 'Loading'
    case 'expanded':
      return 'Collapse'
    case 'empty':
      return 'Nothing further out. Click to collapse.'
    case 'error':
      return 'Could not load its dependencies. Click to collapse.'
  }
}

/**
 * Expands a record outward, on the edge facing away from the subject: the side the new column appears
 * on. The button, not the node, because the node already navigates to the record.
 */
const ExpandButton = ({ node }: { node: DependencyNodeData }) => {
  const { onToggleExpansion, expandTooltips } = useContext(
    DependencyMapActionsContext,
  )
  if (!node.expansion || !onToggleExpansion || node.side === 'center') {
    return null
  }

  const side = node.side
  const status = node.expansion
  const isOpen = status !== 'collapsed'

  return (
    <WaydTooltip
      title={expansionTooltip(status, side, node.label, expandTooltips)}
    >
      <Button
        size="small"
        shape="circle"
        // nodrag/nopan: React Flow would otherwise start a pan from the press.
        className={`nodrag nopan ${styles.expand} ${side === 'left' ? styles.expandLeft : styles.expandRight}`}
        aria-label={isOpen ? `Collapse ${node.label}` : `Expand ${node.label}`}
        aria-expanded={isOpen}
        // Below antd's smallest size: at 24px the button covers the edge's start and competes with the
        // node's own name. Inline, because antd's own size rules outrank a module class.
        styles={{
          root: {
            width: 'var(--ant-control-height-xs)',
            minWidth: 'var(--ant-control-height-xs)',
            height: 'var(--ant-control-height-xs)',
          },
          icon: { fontSize: 'calc(var(--ant-font-size-sm) - 2px)' },
        }}
        icon={isOpen ? <MinusOutlined /> : <PlusOutlined />}
        loading={status === 'loading'}
        onClick={(event) => {
          event.stopPropagation()
          onToggleExpansion(side, node.recordId)
        }}
      />
    </WaydTooltip>
  )
}

/** A click the browser gives its own meaning: a new tab or window, or a download. */
const isModifiedClick = (event: React.MouseEvent) =>
  event.button !== 0 ||
  event.metaKey ||
  event.ctrlKey ||
  event.shiftKey ||
  event.altKey

const RecordNode = ({ data }: NodeProps<DependencyNode>) => {
  const node = data as DependencyNodeData
  const { onOpenRecord } = useContext(DependencyMapActionsContext)
  const opensInPlace = !!onOpenRecord && !node.isSubject
  const isExpanded =
    node.expansion !== undefined && node.expansion !== 'collapsed'

  return (
    <div
      className={`${styles.node} ${node.isSubject ? styles.subject : ''} ${isExpanded ? styles.expanded : ''}`}
      data-testid="dependency-map-node"
    >
      {/* A source and a target on each side: which pair an edge uses depends on which way it runs. */}
      <Handle
        id={DEPENDENCY_HANDLES.inLeft}
        type="target"
        position={Position.Left}
        className={styles.handle}
      />
      <Handle
        id={DEPENDENCY_HANDLES.outLeft}
        type="source"
        position={Position.Left}
        className={styles.handle}
      />
      {node.href ? (
        // Still a real link when it opens in place: a modified click opens the page in a new tab, and the
        // address shows on hover.
        <Link
          href={node.href}
          className={styles.link}
          title={node.label}
          onClick={(event) => {
            if (!opensInPlace || isModifiedClick(event)) return
            event.preventDefault()
            onOpenRecord?.(node)
          }}
        >
          <span className={styles.label}>{node.label}</span>
        </Link>
      ) : opensInPlace ? (
        <button
          type="button"
          className={`${styles.plain} ${styles.opens}`}
          title={node.label}
          onClick={() => onOpenRecord?.(node)}
        >
          <span className={styles.label}>{node.label}</span>
        </button>
      ) : (
        <span className={styles.plain} title={node.label}>
          <span className={styles.label}>{node.label}</span>
        </span>
      )}
      <ExpandButton node={node} />
      <Handle
        id={DEPENDENCY_HANDLES.outRight}
        type="source"
        position={Position.Right}
        className={styles.handle}
      />
      <Handle
        id={DEPENDENCY_HANDLES.inRight}
        type="target"
        position={Position.Right}
        className={styles.handle}
      />
    </div>
  )
}

/** The records a column left off, drawn in place when clicked rather than sending the reader away. */
const OverflowNode = ({ data }: NodeProps<DependencyNode>) => {
  const overflow = data as DependencyOverflowData
  const { onShowAll } = useContext(DependencyMapActionsContext)

  return (
    <button
      type="button"
      className={`nodrag nopan ${styles.overflow}`}
      title={overflow.label}
      disabled={!onShowAll}
      onClick={() => onShowAll?.(overflow.side, overflow.ownerId)}
    >
      {overflow.label}
    </button>
  )
}

/**
 * The box drawn around the records that sit inside another.
 *
 * Containment is not a link, so it is a box rather than another edge: an edge would put "is part of" and
 * "is linked to" in the same visual language, which is the confusion this map exists to keep apart.
 */
const RecordGroupNode = ({ data }: NodeProps<DependencyNode>) => {
  const group = data as DependencyGroupData

  return (
    <div className={styles.group} data-testid="dependency-map-group">
      {group.href ? (
        <Link href={group.href} className={styles.groupLabel}>
          {group.label}
        </Link>
      ) : (
        <span className={styles.groupLabel}>{group.label}</span>
      )}
    </div>
  )
}

const nodeTypes = {
  record: RecordNode,
  recordGroup: RecordGroupNode,
  overflow: OverflowNode,
}

interface MapControlsProps {
  isFullScreen: boolean
  onToggleFullScreen: () => void
  /** Named after the record the map is about, since a downloads folder has no other context. */
  fileStem: string
  theme: DependencySvgTheme
  edgeStyles: Record<string, DependencyEdgeStyle>
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
  edgeStyles,
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
        isGroup: node.type === 'recordGroup',
        isSubject: (node.data as { isSubject?: boolean }).isSubject === true,
        isOverflow: node.type === 'overflow',
        isExpanded: ['expanded', 'empty', 'loading', 'error'].includes(
          (node.data as { expansion?: string }).expansion ?? '',
        ),
      }
    })

    const svgEdges = getEdges().map((edge) => ({
      source: edge.source,
      target: edge.target,
      leavesLeft: edge.sourceHandle === DEPENDENCY_HANDLES.outLeft,
      entersRight: edge.targetHandle === DEPENDENCY_HANDLES.inRight,
      style: edgeStyles[(edge.data as unknown as DependencyEdgeData).variant],
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
 * Refits the view whenever what is drawn, or the canvas it is drawn on, changes.
 *
 * The fitView prop applies once, on init, and the canvas keeps its zoom otherwise: a filtered graph
 * would sit where the unfiltered one was, and a graph fitted to the card would sit in a corner of the
 * fullscreen overlay. Keyed on the canvas size React Flow has measured rather than fitted on a timer,
 * because a filter changes the canvas's height too, and a fit that ran before the resize landed fitted
 * the graph to the old height. React Flow itself defers a fit until new nodes are measured.
 */
const FitToGraph = ({ drawn }: { drawn: string }) => {
  const { fitView } = useReactFlow()
  const width = useStore((state) => state.width)
  const height = useStore((state) => state.height)

  useEffect(() => {
    if (width === 0 || height === 0) return
    fitView({ padding: 0.2, maxZoom: 1 })
  }, [fitView, drawn, width, height])

  return null
}

/**
 * Draws a dependency graph: records as nodes, links as edges.
 *
 * It takes the graph rather than the dependencies, so the same canvas serves any record whose links the
 * caller turns into one, with the caller deciding how each kind of link is drawn.
 */
const DependencyMap = ({
  nodes,
  edges,
  edgeStyles,
  expandTooltips,
  height = 320,
  fileStem = 'dependency-map',
  filters,
  emptyText,
  onToggleExpansion,
  onShowAll,
  onOpenRecord,
}: DependencyMapProps) => {
  const { currentMode } = useTheme()
  const { token } = theme.useToken()
  const router = useRouter()
  const [isFullScreen, setIsFullScreen] = useState(false)

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

  const styledEdges = edges.map((edge) => {
    const style = edgeStyles[edge.data!.variant]

    return {
      ...edge,
      // Curves rather than right angles: orthogonal routing sends every edge into the same corridor, so
      // six links arriving at one record merge into a single trunk and you cannot tell one kind of line
      // from another. Each curve keeps its own path.
      type: 'default',
      style: {
        stroke: style.stroke,
        strokeWidth: style.width,
        strokeDasharray: style.dashed ? '6 4' : undefined,
      },
      markerEnd: {
        type: MarkerType.ArrowClosed,
        width: 18,
        height: 18,
        color: style.stroke,
      },
    }
  })

  return (
    <DependencyMapActionsContext.Provider
      value={{ onToggleExpansion, onShowAll, onOpenRecord, expandTooltips }}
    >
      <div
        className={`${styles.surface} ${isFullScreen ? styles.fullscreen : ''}`}
        style={isFullScreen ? undefined : { height }}
      >
        <ReactFlow
          className={styles.canvas}
          nodes={nodes}
          edges={styledEdges}
          nodeTypes={nodeTypes}
          // React Flow turns pointer events off for a node that is not selectable, draggable or
          // connectable and has no click handler — which left the links inside the nodes dead to the
          // mouse. Handling the click is also what makes the whole node clickable, not just its anchor.
          onNodeClick={(event, node) => {
            // Links navigate themselves, and a button inside a node (expand, show more) does its own job.
            if ((event.target as HTMLElement).closest('a, button')) return
            if (node.type === 'overflow') return

            const record = node.data as DependencyNodeData
            if (onOpenRecord && !record.isSubject) onOpenRecord(record)
            else if (record.href) router.push(record.href)
          }}
          colorMode={currentMode === 'light' ? 'light' : 'dark'}
          fitView
          // Capped at 1: a two-node map would otherwise be blown up to fill the canvas, which makes the
          // same record read as a different size on every page.
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
          {filters && <Panel position="top-right">{filters}</Panel>}
          <FitToGraph drawn={nodes.map((node) => node.id).join('|')} />
          {/* Fullscreen joins zoom and fit in the same stack rather than floating in its own corner:
            they are all "how I am looking at this", and one control cluster is one thing to find. */}
          <MapControls
            isFullScreen={isFullScreen}
            onToggleFullScreen={() => setIsFullScreen((open) => !open)}
            fileStem={fileStem}
            theme={svgTheme}
            edgeStyles={edgeStyles}
          />
        </ReactFlow>
        {nodes.length === 0 && emptyText && (
          <div className={styles.empty}>{emptyText}</div>
        )}
      </div>
    </DependencyMapActionsContext.Provider>
  )
}

export default DependencyMap
