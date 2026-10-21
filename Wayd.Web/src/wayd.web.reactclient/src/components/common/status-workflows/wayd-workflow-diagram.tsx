'use client'

import { WaydEmpty, WaydTooltip } from '@/src/components/common'
import useTheme from '@/src/components/contexts/theme/use-theme'
import { isDarkMode } from '@/src/config/theme/theme-registry'
import {
  StatusCategory,
  StatusWorkflowDetailsDto,
} from '@/src/services/wayd-api'
import {
  AuditOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
  LockOutlined,
  ShrinkOutlined,
  ZoomInOutlined,
  ZoomOutOutlined,
} from '@ant-design/icons'
import {
  Background,
  BaseEdge,
  ControlButton,
  Controls,
  EdgeLabelRenderer,
  Handle,
  MarkerType,
  Position,
  ReactFlow,
  getBezierPath,
  useReactFlow,
  useStore,
  type Edge,
  type EdgeProps,
  type Node,
  type NodeProps,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import { Alert, Flex, theme } from 'antd'
import { useEffect, useState } from 'react'
import {
  anyMoveNote,
  buildWorkflowDiagram,
  edgePath,
  type WorkflowDiagramEdge,
  type WorkflowDiagramNode,
  type WorkflowRecordPath,
} from './workflow-diagram-layout'
import styles from './wayd-workflow-diagram.module.css'

export interface WaydWorkflowDiagramProps {
  workflow: StatusWorkflowDetailsDto
  /** Where a record has been, to highlight its current status and the moves it made. */
  path?: WorkflowRecordPath
  /** Draws, faintly, the moves an action makes that the workflow leaves out. */
  showOmittedMoves?: boolean
}

// Every move leaves and enters a status by its sides: the gaps between columns are where the routed
// moves travel, and nothing stands in them. On each side the way out and the way in sit at different
// heights, so a move and the one back never meet at a point.
const HIGH = { top: '35%' }
const LOW = { top: '65%' }

const HANDLES = {
  inLeft: 'in-left',
  inRight: 'in-right',
  outLeft: 'out-left',
  outRight: 'out-right',
} as const

type StatusNodeData = WorkflowDiagramNode & {
  hasPath: boolean
} & Record<string, unknown>
type ColumnNodeData = { label: string } & Record<string, unknown>
type MoveEdgeData = WorkflowDiagramEdge & Record<string, unknown>

const categoryClass: Record<StatusCategory, string> = {
  [StatusCategory.Proposed]: styles.proposed,
  [StatusCategory.Active]: styles.active,
  [StatusCategory.Done]: styles.done,
  [StatusCategory.Removed]: styles.removed,
}

const StatusNode = ({ data }: NodeProps<Node<StatusNodeData>>) => {
  const offPath = data.hasPath && !data.isVisited && !data.isCurrent
  const className = [
    styles.status,
    categoryClass[data.category],
    data.isCurrent ? styles.current : '',
    data.isVisited && !data.isCurrent ? styles.visited : '',
    offPath ? styles.offPath : '',
  ].join(' ')

  return (
    <div
      className={className}
      data-testid="workflow-diagram-status"
      aria-current={data.isCurrent ? 'step' : undefined}
    >
      <Handle
        id={HANDLES.inLeft}
        type="target"
        position={Position.Left}
        className={styles.handle}
        style={HIGH}
      />
      <Handle
        id={HANDLES.outLeft}
        type="source"
        position={Position.Left}
        className={styles.handle}
        style={LOW}
      />
      <Handle
        id={HANDLES.outRight}
        type="source"
        position={Position.Right}
        className={styles.handle}
        style={HIGH}
      />
      <Handle
        id={HANDLES.inRight}
        type="target"
        position={Position.Right}
        className={styles.handle}
        style={LOW}
      />
      {(data.isInitial || data.isCurrent) && (
        <span className={styles.badges}>
          {data.isInitial && <span className={styles.badge}>Start</span>}
          {data.isCurrent && (
            <span className={`${styles.badge} ${styles.currentBadge}`}>
              Current
            </span>
          )}
        </span>
      )}
      <span className={styles.name} title={data.name}>
        {data.name}
      </span>
      {data.aliasName && (
        <span className={styles.alias}>Alias: {data.aliasName}</span>
      )}
    </div>
  )
}

const ColumnNode = ({ data }: NodeProps<Node<ColumnNodeData>>) => (
  <div className={styles.column}>{data.label}</div>
)

/** The side each route leaves its source by and enters its target by, as `edgePath` expects. */
const routeHandles = {
  forward: [HANDLES.outRight, HANDLES.inLeft],
  over: [HANDLES.outRight, HANDLES.inLeft],
  backward: [HANDLES.outLeft, HANDLES.inRight],
  down: [HANDLES.outRight, HANDLES.inRight],
  up: [HANDLES.outLeft, HANDLES.inLeft],
} as const

/** Whether hovering or focusing a move tells the reader more than the two statuses it joins. */
const hasMoreToSay = (move: WorkflowDiagramEdge) => move.summary.length > 1

const MoveEdge = ({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  data,
  style,
  markerEnd,
}: EdgeProps<Edge<MoveEdgeData>>) => {
  const move = data!
  const source = { x: sourceX, y: sourceY }
  const target = { x: targetX, y: targetY }
  const [path, labelX, labelY] =
    edgePath(move, source, target) ??
    getBezierPath({
      sourceX,
      sourceY,
      sourcePosition: Position.Right,
      targetX,
      targetY,
      targetPosition: Position.Left,
      curvature: 0.25,
    })

  const hasText =
    move.actions.length > 0 || move.isRequired || move.conditions.length > 0
  const labelClass = [
    styles.edgeLabel,
    hasText ? '' : styles.edgeDot,
    move.isTaken ? styles.edgeLabelTaken : '',
    move.kind === 'omitted' ? styles.edgeLabelOmitted : '',
  ].join(' ')

  return (
    <>
      <BaseEdge id={id} path={path} style={style} markerEnd={markerEnd} />
      {/* A move with nothing to add past its two statuses needs no target; the list beside the canvas
        still names it. */}
      {(hasText || hasMoreToSay(move)) && (
        <EdgeLabelRenderer>
          <WaydTooltip
            trigger={['hover', 'focus']}
            title={
              <Flex vertical>
                {move.summary.map((line) => (
                  <span key={line}>{line}</span>
                ))}
              </Flex>
            }
          >
            <div
              className={`nodrag nopan ${labelClass}`}
              style={{
                transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
              }}
              role="img"
              tabIndex={0}
              aria-label={move.summary.join('. ')}
            >
              {move.isRequired && <LockOutlined aria-hidden />}
              {move.actions.length > 0 && (
                <span aria-hidden>{move.actions.join(', ')}</span>
              )}
              {move.conditions.length > 0 && (
                <AuditOutlined className={styles.conditionIcon} aria-hidden />
              )}
            </div>
          </WaydTooltip>
        </EdgeLabelRenderer>
      )}
    </>
  )
}

/** Marks how far the routed moves reach, so the fit, which measures nodes only, keeps them in view. */
const BoundNode = () => <div style={{ width: 1, height: 1 }} />

const nodeTypes = { status: StatusNode, column: ColumnNode, bound: BoundNode }
const edgeTypes = { move: MoveEdge }

const FIT = { padding: 0.1, maxZoom: 1 }

/** Refits whenever what is drawn or the canvas it is drawn on changes, as the dependency map does. */
const FitToDiagram = ({ drawn }: { drawn: string }) => {
  const { fitView } = useReactFlow()
  const width = useStore((state) => state.width)
  const height = useStore((state) => state.height)

  useEffect(() => {
    if (width === 0 || height === 0) return
    fitView(FIT)
  }, [fitView, drawn, width, height])

  return null
}

interface DiagramControlsProps {
  isFullScreen: boolean
  onToggleFullScreen: () => void
}

/** Zoom, fit and fullscreen, with Ant Design's icons in place of React Flow's own. */
const DiagramControls = ({
  isFullScreen,
  onToggleFullScreen,
}: DiagramControlsProps) => {
  const { zoomIn, zoomOut, fitView } = useReactFlow()

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
        <ControlButton aria-label="Fit view" onClick={() => fitView(FIT)}>
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
    </Controls>
  )
}

/**
 * A status workflow drawn as a diagram: statuses as nodes in category columns, allowed moves as arrows.
 * Read-only; the transitions grid is the editor.
 *
 * Given a record's path, it rings the record's current status and draws the moves it made in the
 * primary colour, with everything off the path set back.
 */
const WaydWorkflowDiagram = ({
  workflow,
  path,
  showOmittedMoves = false,
}: WaydWorkflowDiagramProps) => {
  const { currentMode } = useTheme()
  const { token } = theme.useToken()
  const diagram = buildWorkflowDiagram(workflow, { path, showOmittedMoves })
  const hasPath = path !== undefined
  const [isFullScreen, setIsFullScreen] = useState(false)

  useEffect(() => {
    if (!isFullScreen) return

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsFullScreen(false)
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [isFullScreen])

  const nodes: Node[] = [
    ...diagram.columns.map((column) => ({
      id: `column:${column.category}`,
      type: 'column',
      position: { x: column.x, y: 0 },
      data: { label: column.label },
      width: column.width,
      height: column.height,
      style: { width: column.width, height: column.height },
      selectable: false,
      zIndex: -1,
    })),
    ...diagram.nodes.map((node) => ({
      id: node.id,
      type: 'status',
      position: { x: node.x, y: node.y },
      data: { ...node, hasPath },
    })),
  ]

  if (diagram.top < 0) {
    nodes.push({
      id: 'bound:over',
      type: 'bound',
      position: { x: 0, y: diagram.top },
      data: {},
      selectable: false,
    })
  }
  if (diagram.bottom > diagram.height) {
    nodes.push({
      id: 'bound:under',
      type: 'bound',
      position: { x: 0, y: diagram.bottom },
      data: {},
      selectable: false,
    })
  }

  // Resolved from tokens because the arrow markers need a real colour, where a CSS variable would not
  // resolve inside the marker definition.
  const strokeFor = (edge: WorkflowDiagramEdge) => {
    if (edge.isTaken) return token.colorPrimary
    if (edge.kind === 'omitted') return token.colorBorder
    return hasPath ? token.colorBorder : token.colorTextTertiary
  }

  const edges: Edge[] = diagram.edges.map((edge) => {
    const stroke = strokeFor(edge)
    const handles = routeHandles[edge.route]

    return {
      id: edge.id,
      source: edge.source,
      target: edge.target,
      sourceHandle: handles[0],
      targetHandle: handles[1],
      type: 'move',
      data: { ...edge },
      // Above the column backdrops, which sit at -1.
      zIndex: edge.isTaken ? 2 : 1,
      style: {
        stroke,
        strokeWidth: edge.isTaken ? 2.5 : 1.5,
        strokeDasharray: edge.kind === 'omitted' ? '6 4' : undefined,
      },
      markerEnd: {
        type: MarkerType.ArrowClosed,
        width: 16,
        height: 16,
        color: stroke,
      },
    }
  })

  const current = diagram.nodes.find((n) => n.isCurrent)
  const hasRequired = diagram.edges.some((e) => e.isRequired)
  const hasConditions = diagram.edges.some((e) => e.conditions.length > 0)
  const hasOmitted = diagram.edges.some((e) => e.kind === 'omitted')
  // Room for the moves routed above and below the columns, and the controls, so the fit stays near full
  // size rather than shrinking the names out of reading.
  const height = Math.min(
    Math.max(diagram.bottom - diagram.top + 160, 380),
    680,
  )

  if (diagram.nodes.length === 0) {
    return <WaydEmpty message="This workflow has no statuses yet." />
  }

  return (
    <Flex vertical gap={8}>
      {diagram.allowsAnyMove && (
        <Alert type="info" showIcon title={anyMoveNote(workflow)} />
      )}
      <div
        className={`${styles.surface} ${isFullScreen ? styles.fullscreen : ''}`}
        style={isFullScreen ? undefined : { height }}
      >
        <ReactFlow
          className={styles.canvas}
          nodes={nodes}
          edges={edges}
          nodeTypes={nodeTypes}
          edgeTypes={edgeTypes}
          colorMode={isDarkMode(currentMode) ? 'dark' : 'light'}
          fitView
          fitViewOptions={FIT}
          // Read-only: the layout is derived from the workflow, so a moved node would say something the
          // workflow does not, and nothing persists it.
          nodesDraggable={false}
          nodesConnectable={false}
          edgesFocusable={false}
          elementsSelectable={false}
          minZoom={0.3}
          maxZoom={1.5}
        >
          <Background gap={16} />
          <FitToDiagram drawn={nodes.map((n) => n.id).join('|')} />
          <DiagramControls
            isFullScreen={isFullScreen}
            onToggleFullScreen={() => setIsFullScreen((open) => !open)}
          />
        </ReactFlow>
      </div>
      {(hasRequired || hasConditions || hasOmitted || hasPath) && (
        <div className={styles.legend}>
          {hasRequired && (
            <span>
              <LockOutlined /> Required by an action
            </span>
          )}
          {hasConditions && (
            <span>
              <AuditOutlined className={styles.conditionIcon} /> Has conditions
              (hover for details)
            </span>
          )}
          {hasOmitted && (
            <span>Dashed: an action&apos;s move this workflow refuses</span>
          )}
          {hasPath && <span>Highlighted: the path this record has taken</span>}
        </div>
      )}
      {/* The canvas is a picture; this is the same workflow for a screen reader. */}
      <div className={styles.srOnly}>
        <p>
          {workflow.name}: {diagram.nodes.length} statuses.
          {diagram.allowsAnyMove ? ` ${anyMoveNote(workflow)}` : ''}
          {current ? ` Current status: ${current.name}.` : ''}
        </p>
        {diagram.edges.length > 0 && (
          <ul aria-label="Moves">
            {diagram.edges.map((edge) => (
              <li key={edge.id}>{edge.summary.join('. ')}</li>
            ))}
          </ul>
        )}
      </div>
    </Flex>
  )
}

export default WaydWorkflowDiagram
