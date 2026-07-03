'use client'

import {
  CreateRoadmapActivityRequest,
  CreateRoadmapTimeboxRequest,
  RoadmapActivityListDto,
  RoadmapItemListDto,
  RoadmapMilestoneListDto,
  RoadmapTimeboxListDto,
} from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { Button, Form } from 'antd'
import dayjs from 'dayjs'
import {
  type DraftItem,
  WaydGrid2,
  type TreeNode,
  type FilterOption,
  type MoveValidator,
  type GridColumnContext,
  defaultMoveValidator,
  findNodeById,
} from '@/src/components/common/wayd-grid2'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useCreateRoadmapItemMutation,
  useGetRoadmapQuery,
  usePatchRoadmapItemMutation,
  useUpdateRoadmapActivityPlacementMutation,
} from '@/src/store/features/planning/roadmaps-api'
import { FC, ReactNode, useCallback, useMemo, useRef, useState } from 'react'
import EditRoadmapActivityForm from './edit-roadmap-activity-form'
import DeleteRoadmapItemForm from './delete-roadmap-item-form'
import EditRoadmapTimeboxForm from './edit-roadmap-timebox-form'
import { getRoadmapItemsGridColumns } from './roadmap-items-grid.columns'
import { RoadmapItemsHelp } from './roadmap-items-grid.keyboard-shortcuts'

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
  refreshRoadmapItems: () => void
  viewSelector?: ReactNode | undefined
  openRoadmapItemDrawer: (itemId: string) => void
  isRoadmapManager: boolean
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
const CREATE_TYPE_OPTIONS = [
  { label: 'Activity', value: 'activity' },
  { label: 'Timebox', value: 'timebox' },
]

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

// The effective [start, end] of a child node: a range for Activity/Timebox, the date for a Milestone.
const childNodeRange = (
  child: RoadmapItemTreeNode,
): [dayjs.Dayjs, dayjs.Dayjs] | null => {
  if (child.type === 'Milestone' && child.date) {
    const d = dayjs(child.date)
    return [d, d]
  }
  if (child.start && child.end) {
    return [dayjs(child.start), dayjs(child.end)]
  }
  return null
}

/**
 * Validates that an item's range contains all of its direct children. Returns the
 * offending field + message when the range would fall inside the children, matching
 * the domain rule that a parent cannot be shrunk behind a child.
 */
const childrenContainmentError = (
  item: RoadmapItemTreeNode,
  start: dayjs.Dayjs,
  end: dayjs.Dayjs,
): { field: 'start' | 'end'; message: string } | null => {
  for (const child of item.children ?? []) {
    const range = childNodeRange(child)
    if (!range) continue
    const [childStart, childEnd] = range
    if (childStart.isBefore(start, 'day')) {
      return {
        field: 'start',
        message: `Start must be on or before child “${child.name}” (${childStart.format('MMM D, YYYY')}).`,
      }
    }
    if (childEnd.isAfter(end, 'day')) {
      return {
        field: 'end',
        message: `End must be on or after child “${child.name}” (${childEnd.format('MMM D, YYYY')}).`,
      }
    }
  }
  return null
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

const RoadmapItemsGrid: FC<RoadmapItemsGridProps> = ({
  roadmapId,
  roadmapItemsData,
  roadmapItemsIsLoading,
  refreshRoadmapItems,
  viewSelector,
  openRoadmapItemDrawer,
  isRoadmapManager,
}) => {
  const [form] = Form.useForm()
  const selectedDraftItemType = Form.useWatch('itemType', form)
  const messageApi = useMessage()

  const { data: roadmap } = useGetRoadmapQuery(roadmapId)
  const roadmapColors = useMemo(() => roadmap?.colors ?? [], [roadmap?.colors])

  const [openUpdateRoadmapActivityForm, setOpenUpdateRoadmapActivityForm] =
    useState(false)
  const [openUpdateRoadmapTimeboxForm, setOpenUpdateRoadmapTimeboxForm] =
    useState(false)
  const [openDeleteRoadmapItemForm, setOpenDeleteRoadmapItemForm] =
    useState(false)
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const draftsRef = useRef<DraftItem[]>([])

  const [createRoadmapItem] = useCreateRoadmapItemMutation()
  const [patchRoadmapItem] = usePatchRoadmapItemMutation()
  const [updateActivityPlacement] = useUpdateRoadmapActivityPlacementMutation()

  const enableDragAndDrop = isRoadmapManager

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
      try {
        await updateActivityPlacement({
          request: {
            roadmapId,
            itemId: nodeId,
            parentId: parentId ?? undefined,
            order,
          },
        }).unwrap()
        refreshRoadmapItems()
      } catch (error: any) {
        messageApi.error(
          error?.data?.detail ||
            'Failed to reorganize activity. Please try again.',
        )
      }
    },
    [roadmapId, updateActivityPlacement, refreshRoadmapItems, messageApi],
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

  const handleSaveRoadmapItem = useCallback(
    async (itemId: string, updates: Record<string, any>): Promise<boolean> => {
      const isDraft = itemId.startsWith('draft-')

      try {
        if (isDraft) {
          const draft = draftsRef.current.find((d) => d.id === itemId)
          if (!draft) return false

          const itemType =
            updates.itemType === 'timebox' ? 'timebox' : 'activity'
          const request:
            | CreateRoadmapActivityRequest
            | CreateRoadmapTimeboxRequest = {
            $type: itemType,
            roadmapId,
            parentId: draft.parentId,
            name: updates.name || '',
            start: updates.start,
            end: updates.end,
            ...(itemType === 'activity' ? { color: updates.color } : {}),
          } as CreateRoadmapActivityRequest | CreateRoadmapTimeboxRequest

          const response = await createRoadmapItem(request)
          if (response.error) throw response.error
        } else {
          const item = findNodeById(
            treeData,
            itemId,
          ) as RoadmapItemTreeNode | null
          if (!item) return false

          const patchOperations: Array<{
            op: 'replace' | 'add' | 'remove'
            path: string
            value?: any
          }> = []

          if (updates.name !== undefined) {
            patchOperations.push({
              op: 'replace',
              path: '/name',
              value: updates.name,
            })
          }
          if (updates.start !== undefined) {
            patchOperations.push({
              op: 'replace',
              path: item.type === 'Milestone' ? '/date' : '/start',
              value: updates.start,
            })
          }
          if (updates.end !== undefined && item.type !== 'Milestone') {
            patchOperations.push({
              op: 'replace',
              path: '/end',
              value: updates.end,
            })
          }
          if (updates.color !== undefined) {
            patchOperations.push({
              op: 'replace',
              path: '/color',
              value: updates.color,
            })
          }

          if (patchOperations.length === 0) return true

          await patchRoadmapItem({
            roadmapId,
            itemId,
            patchOperations,
          }).unwrap()
        }

        setFieldErrors({})
        messageApi.success(
          isDraft
            ? updates.itemType === 'timebox'
              ? 'Roadmap timebox created successfully.'
              : 'Roadmap activity created successfully.'
            : 'Roadmap item updated successfully.',
        )
        refreshRoadmapItems()
        return true
      } catch (error: any) {
        const status = error?.status ?? error?.data?.status
        const errors = error?.errors ?? error?.data?.errors
        const detail = error?.detail ?? error?.data?.detail

        if (status === 422 && errors) {
          const errorMap: Record<string, string> = {}
          const errorFields: string[] = []
          Object.entries(errors).forEach(([key, messages]) => {
            const apiField = key.charAt(0).toLowerCase() + key.slice(1)
            const fieldName =
              apiField === 'type' || apiField === '$type'
                ? 'itemType'
                : apiField === 'date'
                  ? 'start'
                  : apiField
            errorMap[fieldName] = Array.isArray(messages)
              ? String(messages[0] ?? '')
              : String(messages)
            errorFields.push(fieldName)
          })
          setFieldErrors(errorMap)

          setTimeout(() => {
            const fieldToColumn: Record<string, string> = {
              itemType: 'type',
              type: 'type',
            }

            let focused = false
            for (const errorField of errorFields) {
              const columnId = fieldToColumn[errorField] ?? errorField
              const cellElement = document.querySelector(
                `[data-cell-id="${itemId}-${columnId}"]`,
              )
              if (!cellElement) continue

              const input = cellElement.querySelector(
                'input, .ant-select, .ant-picker',
              ) as HTMLElement | null
              if (input) {
                input.focus({ preventScroll: true })
                focused = true
                break
              }
            }

            if (!focused) {
              const cellElement = document.querySelector(
                `[data-cell-id="${itemId}-name"]`,
              )
              const input = cellElement?.querySelector(
                'input',
              ) as HTMLElement | null
              input?.focus({ preventScroll: true })
            }
          }, 0)

          messageApi.error('Correct the validation error(s) to continue.')
          return false
        }

        messageApi.error(
          detail ??
            `An error occurred while ${isDraft ? 'creating' : 'updating'} the roadmap item. Please try again.`,
        )
        return false
      }
    },
    [
      createRoadmapItem,
      messageApi,
      patchRoadmapItem,
      refreshRoadmapItems,
      roadmapId,
      treeData,
    ],
  )

  const getFormValues = useCallback(
    (rowId: string, data: RoadmapItemTreeNode[]) => {
      const item = findNodeById(data, rowId) as RoadmapItemTreeNode | null
      const isDraft = rowId.startsWith('draft-')

      if (isDraft || !item) {
        return {
          name: '',
          itemType: 'activity',
          start: null,
          end: null,
          color: null,
        }
      }

      return {
        name: item.name,
        itemType: item.$type,
        start:
          item.type === 'Milestone'
            ? item.date
              ? dayjs(item.date)
              : null
            : item.start
              ? dayjs(item.start)
              : null,
        end: item.end ? dayjs(item.end) : null,
        color: item.color ?? null,
      }
    },
    [],
  )

  const computeChanges = useCallback(
    (
      rowId: string,
      formValues: Record<string, any>,
      _data: RoadmapItemTreeNode[],
    ) => {
      if (!rowId.startsWith('draft-')) return null

      const values = formValues as any
      return {
        name: values.name || '',
        itemType: values.itemType || 'activity',
        start: values.start ? values.start.format('YYYY-MM-DD') : null,
        end: values.end ? values.end.format('YYYY-MM-DD') : null,
        color: values.color ?? null,
      }
    },
    [],
  )

  const computeRoadmapItemChanges = useCallback(
    (
      rowId: string,
      formValues: Record<string, any>,
      data: RoadmapItemTreeNode[],
    ) => {
      if (rowId.startsWith('draft-')) {
        return computeChanges(rowId, formValues, data)
      }

      const item = findNodeById(data, rowId) as RoadmapItemTreeNode | null
      if (!item) return null

      const values = formValues as any
      const updates: Record<string, any> = {}
      let hasChanges = false

      if (values.name !== item.name) {
        updates.name = values.name
        hasChanges = true
      }

      const currentStart =
        item.type === 'Milestone'
          ? item.date
            ? dayjs(item.date).format('YYYY-MM-DD')
            : null
          : item.start
            ? dayjs(item.start).format('YYYY-MM-DD')
            : null
      const nextStart = values.start ? values.start.format('YYYY-MM-DD') : null
      if (nextStart !== currentStart) {
        updates.start = nextStart
        hasChanges = true
      }

      if (item.type !== 'Milestone') {
        const currentEnd = item.end
          ? dayjs(item.end).format('YYYY-MM-DD')
          : null
        const nextEnd = values.end ? values.end.format('YYYY-MM-DD') : null
        if (nextEnd !== currentEnd) {
          updates.end = nextEnd
          hasChanges = true
        }
      }

      const currentColor = item.color ?? null
      const nextColor = values.color ?? null
      if (nextColor !== currentColor) {
        updates.color = nextColor
        hasChanges = true
      }

      return hasChanges ? updates : null
    },
    [computeChanges],
  )

  const validateFields = useCallback(
    (rowId: string, formValues: Record<string, any>) => {
      if (!rowId.startsWith('draft-')) return {}

      const errors: Record<string, string> = {}
      const name = String(formValues.name ?? '').trim()
      const start = formValues.start
      const end = formValues.end

      if (!name) {
        errors.name = 'Name is required.'
      }

      if (!start || !end) {
        const message = 'Start and end dates are required.'
        errors.start = message
        errors.end = message
      } else if (dayjs(end).isBefore(dayjs(start), 'day')) {
        errors.end = 'End date must be on or after start date.'
      }

      return errors
    },
    [],
  )

  const validateRoadmapItemFields = useCallback(
    (rowId: string, formValues: Record<string, any>) => {
      if (rowId.startsWith('draft-')) {
        return validateFields(rowId, formValues)
      }

      const item = findNodeById(treeData, rowId) as RoadmapItemTreeNode | null
      if (!item) return {}

      const errors: Record<string, string> = {}
      const name = String(formValues.name ?? '').trim()
      const start = formValues.start
      const end = formValues.end

      if (!name) {
        errors.name = 'Name is required.'
      }

      if (item.type === 'Milestone') {
        if (!start) {
          errors.start = 'Date is required.'
        }
        return errors
      }

      if (!start || !end) {
        const message = 'Start and end dates are required.'
        errors.start = message
        errors.end = message
      } else if (dayjs(end).isBefore(dayjs(start), 'day')) {
        errors.end = 'End date must be on or after start date.'
      } else {
        // An activity's range must contain all of its children (matches the domain
        // rule that a parent cannot be shrunk behind a child).
        const originalStart = item.start ? dayjs(item.start) : null
        const originalEnd = item.end ? dayjs(item.end) : null
        const newStart = dayjs(start)
        const newEnd = dayjs(end)

        let isShift = false
        if (originalStart && originalEnd && originalStart.isValid() && originalEnd.isValid()) {
          const startDelta = newStart.diff(originalStart, 'day')
          const endDelta = newEnd.diff(originalEnd, 'day')
          isShift = startDelta === endDelta && startDelta !== 0
        }

        if (!isShift) {
          const containment = childrenContainmentError(item, newStart, newEnd)
          if (containment) {
            errors[containment.field] = containment.message
          }
        }
      }

      return errors
    },
    [treeData, validateFields],
  )

  const columns = useCallback(
    (ctx: GridColumnContext) =>
      getRoadmapItemsGridColumns({
        isRoadmapManager,
        selectedRowId: ctx.selectedRowId,
        onEditItem,
        onDeleteItem,
        handleSaveRoadmapItem,
        getFieldError: ctx.getFieldError,
        handleKeyDown: ctx.handleKeyDown,
        openRoadmapItemDrawer,
        typeFilterOptions: TYPE_FILTER_OPTIONS,
        isDragEnabled: ctx.isDragEnabled,
        enableDragAndDrop,
        addDraftItemAsChild: ctx.addDraftAsChild,
        canCreateItems: ctx.canCreateDraft,
        createTypeOptions: CREATE_TYPE_OPTIONS,
        isSelectedDraftActivity: selectedDraftItemType !== 'timebox',
        roadmapColors,
      }),
    [
      isRoadmapManager,
      openRoadmapItemDrawer,
      onEditItem,
      onDeleteItem,
      handleSaveRoadmapItem,
      enableDragAndDrop,
      selectedDraftItemType,
      roadmapColors,
    ],
  )

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
    setOpenDeleteRoadmapItemForm(false)
    setSelectedItemId(null)
    if (wasSaved) {
      refreshRoadmapItems()
    }
  }

  return (
    <>
      <Form form={form} component={false}>
        <WaydGrid2<RoadmapItemTreeNode>
          data={treeData}
          getSubRows={(row) => row.children}
          isLoading={roadmapItemsIsLoading}
          columns={columns}
          rightSlot={viewSelector}
          onRefresh={async () => refreshRoadmapItems()}
          emptyMessage="No Roadmap Items"
          csvFileName="roadmap-items"
          enableDragAndDrop={enableDragAndDrop}
          onNodeMove={handleNodeMove}
          onMoveRejected={(reason) =>
            messageApi.warning(reason || 'Cannot move item to this location')
          }
          moveValidator={roadmapActivityMoveValidator}
          editingConfig={{
            canEdit: isRoadmapManager,
            form,
            editableColumnIds: (rowId) => {
              if (rowId?.startsWith('draft-')) {
                return selectedDraftItemType === 'timebox'
                  ? ['name', 'type', 'start', 'end']
                  : ['name', 'type', 'start', 'end', 'color']
              }

              const item = rowId
                ? (findNodeById(treeData, rowId) as RoadmapItemTreeNode | null)
                : null
              if (item?.type === 'Timebox') {
                return ['name', 'start', 'end']
              }

              return ['name', 'start', 'end', 'color']
            },
            onSave: handleSaveRoadmapItem,
            getFormValues,
            computeChanges: computeRoadmapItemChanges,
            validateFields: validateRoadmapItemFields,
            cellIdColumnMatchOrder: ['start', 'type', 'name', 'color', 'end'],
          }}
          fieldErrors={fieldErrors}
          onFieldErrorsChange={setFieldErrors}
          createDraftNode={createDraftRoadmapItem}
          onDraftsChange={(drafts) => {
            draftsRef.current = drafts
          }}
          leftSlot={(ctx) =>
            isRoadmapManager && (
              <Button
                icon={<PlusOutlined />}
                onClick={() => ctx.addDraftAtRoot()}
                disabled={!ctx.canCreateDraft}
              >
                Create Item
              </Button>
            )
          }
          helpContent={<RoadmapItemsHelp />}
        />
      </Form>
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

