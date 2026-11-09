'use client'

import {
  RoadmapActivityListDto,
  RoadmapItemListDto,
  RoadmapMilestoneListDto,
  RoadmapTimeboxListDto,
} from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import dayjs from 'dayjs'
import { toCalendarDate } from '@/src/utils/calendar-date'
import {
  type DraftItem,
  WaydGrid,
  type TreeNode,
  type FilterOption,
  type MoveValidator,
  type GridColumnContext,
  type WaydGridHandle,
  defaultMoveValidator,
  findNodeById,
  rowAfterRemoving,
} from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useCreateRoadmapItemMutation,
  useGetRoadmapQuery,
  usePatchRoadmapItemMutation,
  useUpdateRoadmapActivityPlacementMutation,
  useUpdateRoadmapItemDatesMutation,
} from '@/src/store/features/planning/roadmaps-api'
import { isApiError, type ApiError } from '@/src/utils'
import { errorDetail } from '@/src/utils/problem-details'
import { FC, ReactNode, useCallback, useMemo, useRef, useState } from 'react'
import EditRoadmapActivityForm from './edit-roadmap-activity-form'
import DeleteRoadmapItemForm from './delete-roadmap-item-form'
import EditRoadmapTimeboxForm from './edit-roadmap-timebox-form'
import { getRoadmapItemsGridColumns } from './roadmap-items-grid.columns'
import {
  toCreateRoadmapItemRequest,
  toRoadmapItemPatch,
} from './roadmap-items-grid.editors'
import {
  useRoadmapGantt,
  computeGanttDomain,
  pxPerMsFor,
} from './roadmap-gantt'
import {
  GanttToolbarActions,
  useBarDrag,
  useGanttVisibility,
  useGanttZoom,
  useChartPaneWidth,
} from '@/src/components/common/timeline'

export interface RoadmapItemTreeNode extends TreeNode {
  id: string
  children: RoadmapItemTreeNode[]
  parentId?: string | null
  name: string
  type: string
  $type: string
  start?: Date | null
  end?: Date | null
  date?: Date | null
  color?: string | null
}

export interface RoadmapItemsGridProps {
  roadmapId: string
  roadmapItemsData: RoadmapItemListDto[]
  roadmapItemsIsLoading: boolean
  /** Refetches the items; resolves once they are in. */
  refreshRoadmapItems: () => Promise<unknown>
  viewSelector?: ReactNode | undefined
  openRoadmapItemDrawer: (itemId: string) => void
  canManage: boolean
}

type RoadmapItemUnion =
  | RoadmapItemListDto
  | RoadmapActivityListDto
  | RoadmapMilestoneListDto
  | RoadmapTimeboxListDto

const TYPE_FILTER_OPTIONS: FilterOption[] = [
  { label: 'Activity', value: 'Activity' },
  //{ label: 'Milestone', value: 'Milestone' },
  { label: 'Timebox', value: 'Timebox' },
]
/**
 * A milestone's `date` errors belong to its Start column, where the single
 * date is edited.
 */
const FIELD_TO_COLUMN = { date: 'start' }

const parseRoadmapCalendarDate = (value: unknown): Date | null => {
  if (!value) return null

  const raw = String(value)
  // Roadmap dates are calendar dates; parse using the date portion only.
  // This avoids timezone shifts for values like 2026-02-17T00:00:00Z.
  const datePartMatch = raw.match(/^(\d{4}-\d{2}-\d{2})/)
  if (datePartMatch) {
    const [year, month, day] = datePartMatch[1].split('-').map(Number)
    return new Date(year, month - 1, day)
  }

  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

function mapToTreeNode(item: RoadmapItemUnion): RoadmapItemTreeNode {
  const node: RoadmapItemTreeNode = {
    id: item.id,
    name: item.name,
    type: item.type.name,
    $type: item.$type,
    parentId: item.parent?.id ?? null,
    start:
      'start' in item ? parseRoadmapCalendarDate((item as any).start) : null,
    end: 'end' in item ? parseRoadmapCalendarDate((item as any).end) : null,
    date: 'date' in item ? parseRoadmapCalendarDate((item as any).date) : null,
    color: item.color ?? null,
    children: [],
  }

  if (
    'children' in item &&
    Array.isArray(item.children) &&
    item.children.length > 0
  ) {
    node.children = item.children.map((child) => mapToTreeNode(child))
  }

  return node
}

/** Activities and the root take children; timeboxes and milestones do not. */
const canHoldItems = (parent: RoadmapItemTreeNode | null) =>
  !parent || parent.type === 'Activity'

/**
 * Where a new activity goes among its sibling activities. The grid counts
 * every sibling, but timeboxes and milestones always follow the activities,
 * so a place after one of them is the end of the activities.
 */
const activityOrderAt = (
  nodes: RoadmapItemTreeNode[],
  parentId: string | null,
  order: number,
) => {
  const siblings = parentId
    ? (findNodeById(nodes, parentId)?.children ?? [])
    : nodes
  const activities = siblings.filter((node) => node.type === 'Activity')
  return Math.min(order, activities.length + 1)
}

const RoadmapItemsGrid: FC<RoadmapItemsGridProps> = ({
  roadmapId,
  roadmapItemsData,
  roadmapItemsIsLoading,
  refreshRoadmapItems,
  viewSelector,
  openRoadmapItemDrawer,
  canManage,
}) => {
  const messageApi = useMessage()
  const gridRef = useRef<WaydGridHandle>(null)

  const { data: roadmap } = useGetRoadmapQuery(roadmapId)
  const roadmapColors = useMemo(() => roadmap?.colors ?? [], [roadmap?.colors])

  const [openUpdateRoadmapActivityForm, setOpenUpdateRoadmapActivityForm] =
    useState(false)
  const [openUpdateRoadmapTimeboxForm, setOpenUpdateRoadmapTimeboxForm] =
    useState(false)
  const [openDeleteRoadmapItemForm, setOpenDeleteRoadmapItemForm] =
    useState(false)
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null)
  // Show/hide the chart. Persisted across roadmaps (see useGanttVisibility).
  const ganttVisibility = useGanttVisibility('roadmap')
  const showGantt = ganttVisibility.visible

  const [createRoadmapItem] = useCreateRoadmapItemMutation()
  const [patchRoadmapItem] = usePatchRoadmapItemMutation()
  const [updateActivityPlacement] = useUpdateRoadmapActivityPlacementMutation()

  const enableDragAndDrop = canManage

  const onEditItem = useCallback((item: RoadmapItemTreeNode) => {
    setSelectedItemId(item.id)
    if (item.type === 'Activity') {
      setOpenUpdateRoadmapActivityForm(true)
    } else if (item.type === 'Timebox') {
      setOpenUpdateRoadmapTimeboxForm(true)
    }
  }, [])

  const onDeleteItem = useCallback((item: RoadmapItemTreeNode) => {
    setSelectedItemId(item.id)
    setOpenDeleteRoadmapItemForm(true)
  }, [])

  const treeData = useMemo(() => {
    if (roadmapItemsIsLoading) return []
    return roadmapItemsData.map((item) => mapToTreeNode(item))
  }, [roadmapItemsData, roadmapItemsIsLoading])

  // ── Gantt bar drag/resize (reuses the timeline's interaction core) ─────────
  // Commit a bar's new dates via the SAME mutation the timeline view uses.
  const [updateRoadmapItemDates] = useUpdateRoadmapItemDatesMutation()
  const commitBarDates = useCallback(
    async (change: { id: string; start: number; end: number }) => {
      const node = findNodeById(
        treeData,
        change.id,
      ) as RoadmapItemTreeNode | null
      if (!node) return
      try {
        // A milestone is a single instant, so its request carries `date` rather
        // than a start/end pair (both drag bounds hold the same value).
        const item = { $type: node.$type, roadmapId, itemId: change.id }
        const response = await updateRoadmapItemDates(
          node.type === 'Milestone'
            ? { ...item, date: toCalendarDate(dayjs(change.start)) }
            : {
                ...item,
                start: toCalendarDate(dayjs(change.start)),
                end: toCalendarDate(dayjs(change.end)),
              },
        )
        if ('error' in response && response.error) throw response.error
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while updating the roadmap item. Please try again.',
        )
      }
    },
    [treeData, roadmapId, updateRoadmapItemDates, messageApi],
  )

  // Domain drives the drag clamp range; pxPerMs comes from the zoom level. Both
  // are computed the same way the chart's scale is (no duplication).
  const { domainStart, domainEnd } = useMemo(
    () =>
      computeGanttDomain(
        roadmap?.start ?? new Date(),
        roadmap?.end ?? new Date(),
        treeData,
      ),
    [roadmap?.start, roadmap?.end, treeData],
  )
  // Gantt zoom level (pixels per day), adjusted via toolbar +/- and
  // Ctrl/Cmd+wheel over the chart. Floored so the chart always fills the pane —
  // zooming out past the fit would leave the axis short of the right edge.
  // Needs the domain and the live pane width, so it follows both.
  const chartPaneWidth = useChartPaneWidth(showGantt)
  const zoom = useGanttZoom(undefined, {
    domain: [domainStart, domainEnd],
    viewportWidth: chartPaneWidth,
  })

  const barDrag = useBarDrag({
    pxPerMs: pxPerMsFor(zoom.pxPerDay),
    min: domainStart,
    max: domainEnd,
    onCommit: commitBarDates,
  })
  // Pointer offset from the bar's left edge at move-drag start, so the live date
  // label can follow the cursor rather than centering on the whole bar. Captured
  // in the wrapper below (outside the memoized gantt hook) and surfaced only
  // while a move drag is active.
  const [grabOffset, setGrabOffset] = useState(0)
  const onBarPointerDown = useCallback(
    (
      e: React.PointerEvent,
      item: { id: string; start: number; end: number; kind: 'range' },
      mode: 'move' | 'resize-start' | 'resize-end',
    ) => {
      if (mode === 'move') setGrabOffset(e.nativeEvent.offsetX)
      barDrag.start(e, item, mode)
    },
    [barDrag],
  )

  // Gantt chart pane (attached to the grid's right when toggled on). The hook is
  // called unconditionally (rules of hooks); the pane is only wired when shown.
  const gantt = useRoadmapGantt(
    roadmap?.start ?? new Date(),
    roadmap?.end ?? new Date(),
    treeData,
    {
      pxPerDay: zoom.pxPerDay,
      editable: canManage,
      activeDrag: barDrag.active,
      onBarPointerDown,
      moveGrabOffset: barDrag.active?.mode === 'move' ? grabOffset : undefined,
    },
  )

  const roadmapActivityMoveValidator: MoveValidator<RoadmapItemTreeNode> =
    useCallback((activeNode, targetParentNode, targetParentId) => {
      // Only activities can be dragged
      if (activeNode.node.type !== 'Activity') {
        return { canMove: false, reason: 'Only activities can be reordered' }
      }
      const result = defaultMoveValidator(
        activeNode,
        targetParentNode,
        targetParentId,
      )
      if (!result.canMove) return result
      // Activities can only be dropped into other activities or root
      if (targetParentNode && targetParentNode.type !== 'Activity') {
        return {
          canMove: false,
          reason: 'Activities can only be placed under other activities',
        }
      }
      return { canMove: true }
    }, [])

  const handleNodeMove = useCallback(
    async (nodeId: string, parentId: string | null, order: number) => {
      // Failures reject, so the grid reports them once (onMoveRejected) and
      // a keyboard move knows the row stayed where it was.
      try {
        await updateActivityPlacement({
          request: {
            roadmapId,
            itemId: nodeId,
            parentId: parentId ?? undefined,
            order,
          },
        }).unwrap()
      } catch (error) {
        throw new Error(
          errorDetail(error) ||
            'Failed to reorganize activity. Please try again.',
        )
      }
      await refreshRoadmapItems()
    },
    [roadmapId, updateActivityPlacement, refreshRoadmapItems],
  )

  const createDraftRoadmapItem = useCallback(
    (draft: DraftItem): RoadmapItemTreeNode => ({
      id: draft.id,
      name: '',
      type: 'Activity',
      $type: 'activity',
      parentId: draft.parentId ?? null,
      start: null,
      end: null,
      date: null,
      color: null,
      children: [],
    }),
    [],
  )

  /** Saves one edited cell (or a start/end pair) of an existing item. */
  const commitRoadmapItem = async (
    itemId: string,
    patch: Record<string, unknown>,
  ) => {
    await patchRoadmapItem({
      roadmapId,
      itemId,
      patchOperations: toRoadmapItemPatch(patch),
    }).unwrap()
  }

  /**
   * Creates a new row once it has a name and dates; returns its id. An
   * activity added below a row is created in that place.
   */
  const createRoadmapItemFromDraft = (
    parentId: string | null,
    values: Record<string, unknown>,
    position?: { order: number },
  ) => {
    const request = toCreateRoadmapItemRequest(roadmapId, parentId, values)
    return createRoadmapItem(
      position && request.$type === 'activity'
        ? {
            ...request,
            order: activityOrderAt(treeData, parentId, position.order),
          }
        : request,
    ).unwrap()
  }

  const columns = (ctx: GridColumnContext) =>
    getRoadmapItemsGridColumns({
      canManage,
      onEditItem,
      onDeleteItem,
      openRoadmapItemDrawer,
      typeFilterOptions: TYPE_FILTER_OPTIONS,
      isDragEnabled: ctx.isDragEnabled,
      enableDragAndDrop,
      addDraftItemAsChild: ctx.addDraftAsChild,
      canCreateItems: ctx.canCreateDraft,
      roadmapColors,
    })

  const onUpdateRoadmapActivityFormClosed = (wasSaved: boolean) => {
    setOpenUpdateRoadmapActivityForm(false)
    setSelectedItemId(null)
    if (wasSaved) {
      refreshRoadmapItems()
    }
  }

  const onUpdateRoadmapTimeboxFormClosed = (wasSaved: boolean) => {
    setOpenUpdateRoadmapTimeboxForm(false)
    setSelectedItemId(null)
    if (wasSaved) {
      refreshRoadmapItems()
    }
  }

  const onDeleteRoadmapItemFormClosed = (wasSaved: boolean) => {
    // The deleted row's cell held focus; it moves to the row taking its place.
    const deleting = selectedItemId
    const next = deleting ? rowAfterRemoving(treeData, deleting) : null
    setOpenDeleteRoadmapItemForm(false)
    setSelectedItemId(null)
    // The form unmounts without handing focus back, so a cancel returns it.
    if (!wasSaved) {
      if (deleting) gridRef.current?.focusRow(deleting, 'name')
      return
    }
    void refreshRoadmapItems().then(() => {
      if (next) gridRef.current?.focusRow(next, 'name')
    })
  }

  return (
    <>
      <WaydGrid<RoadmapItemTreeNode>
        ref={gridRef}
        data={treeData}
        getSubRows={(row) => row.children}
        isLoading={roadmapItemsIsLoading}
        columns={columns}
        actionsSlot={
          <GanttToolbarActions
            visible={showGantt}
            onToggle={ganttVisibility.toggle}
            zoom={zoom}
          />
        }
        rightSlot={viewSelector}
        rightPane={
          showGantt
            ? {
                header: gantt.header,
                defaultWidth: gantt.defaultWidth,
                renderRow: gantt.renderRow,
                renderBackground: gantt.renderBackground,
                // Ctrl/Cmd+wheel zooms the chart (matches the timeline).
                onWheel: zoom.onWheel,
              }
            : undefined
        }
        onRefresh={refreshRoadmapItems}
        emptyMessage="No Roadmap Items"
        csvFileName="roadmap-items"
        enableDragAndDrop={enableDragAndDrop}
        onNodeMove={handleNodeMove}
        onMoveRejected={(reason) =>
          messageApi.warning(reason || 'Cannot move item to this location')
        }
        moveValidator={roadmapActivityMoveValidator}
        onCommit={canManage ? commitRoadmapItem : undefined}
        onCreate={canManage ? createRoadmapItemFromDraft : undefined}
        fieldToColumn={FIELD_TO_COLUMN}
        createDraftNode={canManage ? createDraftRoadmapItem : undefined}
        rowActions={{
          ...(canManage
            ? {
                canInsertUnder: canHoldItems,
                onDelete: onDeleteItem,
              }
            : {}),
          onOpen: (row) => openRoadmapItemDrawer(row.id),
        }}
        leftSlot={(ctx) =>
          canManage && (
            <Button
              icon={<PlusOutlined />}
              onClick={() => ctx.addDraftAtRoot()}
              disabled={!ctx.canCreateDraft}
            >
              Create Item
            </Button>
          )
        }
      />
      {openUpdateRoadmapActivityForm && selectedItemId !== null && (
        <EditRoadmapActivityForm
          activityId={selectedItemId}
          roadmapId={roadmapId}
          onFormComplete={() => onUpdateRoadmapActivityFormClosed(true)}
          onFormCancel={() => onUpdateRoadmapActivityFormClosed(false)}
        />
      )}
      {openUpdateRoadmapTimeboxForm && selectedItemId !== null && (
        <EditRoadmapTimeboxForm
          timeboxId={selectedItemId}
          roadmapId={roadmapId}
          onFormComplete={() => onUpdateRoadmapTimeboxFormClosed(true)}
          onFormCancel={() => onUpdateRoadmapTimeboxFormClosed(false)}
        />
      )}
      {openDeleteRoadmapItemForm && selectedItemId !== null && (
        <DeleteRoadmapItemForm
          roadmapId={roadmapId}
          roadmapItemId={selectedItemId}
          onFormComplete={() => onDeleteRoadmapItemFormClosed(true)}
          onFormCancel={() => onDeleteRoadmapItemFormClosed(false)}
        />
      )}
    </>
  )
}

export default RoadmapItemsGrid
