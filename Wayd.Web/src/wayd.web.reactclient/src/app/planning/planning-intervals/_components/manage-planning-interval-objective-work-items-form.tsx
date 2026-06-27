'use client'

import { useConfirmModal, useDebounce } from '@/src/hooks'
import {
  ManagePlanningIntervalObjectiveWorkItemsRequest,
  WorkItemListDto,
} from '@/src/services/wayd-api'
import {
  useGetObjectiveWorkItemsQuery,
  useGetPlanningIntervalObjectiveQuery,
  useManageObjectiveWorkItemsMutation,
} from '@/src/store/features/planning/planning-interval-api'
import { useSearchWorkItemsQuery } from '@/src/store/features/work-management/workspace-api'
import { LoadingOutlined, SearchOutlined } from '@ant-design/icons'
import { Flex, Input, Modal, Typography } from 'antd'
import { ChangeEvent, useState } from 'react'
import { ColDef } from 'ag-grid-community'
import { CustomCellRendererProps } from 'ag-grid-react'
import {
  AgGridTransfer,
  asDeletableColDefs,
  asDraggableColDefs,
} from '@/src/components/common/grid/ag-grid-transfer'
import { useMessage } from '@/src/components/contexts/messaging'
import { workItemKeyComparator, WorkItemTagsCell } from '@/src/components/common/work'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface ManagePlanningIntervalObjectiveWorkItemsFormProps {
  planningIntervalKey: number
  objectiveKey: number
  onFormComplete: () => void
  onFormCancel: () => void
}

const workItemColDefs: ColDef<WorkItemListDto>[] = [
  {
    field: 'key',
    headerName: 'Key',
    width: 125,
  },
  {
    field: 'title',
    headerName: 'Title',
    width: 250,
  },
  {
    field: 'type.name',
    headerName: 'Type',
    width: 100,
  },
  {
    field: 'status',
    headerName: 'Status',
    width: 100,
  },
  {
    field: 'team.name',
    headerName: 'Team',
    width: 150,
  },
  {
    field: 'parent.key',
    headerName: 'Parent Key',
    width: 125,
  },
  {
    field: 'sprint.name',
    headerName: 'Sprint',
    width: 200,
  },
  {
    field: 'project.name',
    headerName: 'Project',
    width: 200,
  },
  {
    field: 'tags',
    headerName: 'Tags',
    width: 200,
    valueGetter: (params) => params.data?.tags?.join(', ') ?? '',
    cellRenderer: (params: CustomCellRendererProps<WorkItemListDto>) => (
      <WorkItemTagsCell tags={params.data?.tags} />
    ),
  },
]

const leftColDefs = [...asDraggableColDefs(workItemColDefs)]

const defaultSort = (a: WorkItemListDto, b: WorkItemListDto) => {
  return workItemKeyComparator(a.key, b.key)
}

const defaultColDef: ColDef = {
  filter: true,
  floatingFilter: true,
}

const ManagePlanningIntervalObjectiveWorkItemsForm = ({
  planningIntervalKey,
  objectiveKey,
  onFormComplete,
  onFormCancel,
}: ManagePlanningIntervalObjectiveWorkItemsFormProps) => {
  // Track user modifications (drag/delete) separately from query data.
  // addedItems: items dragged from source to target by the user.
  // removedIds: ids of items deleted from target by the user.
  const [addedItems, setAddedItems] = useState<WorkItemListDto[]>([])
  const [removedIds, setRemovedIds] = useState<Set<string>>(new Set())
  const messageApi = useMessage()

  const [searchQuery, setSearchQuery] = useState<string>('')

  const { data: objectiveData } = useGetPlanningIntervalObjectiveQuery({
    planningIntervalKey: planningIntervalKey.toString(),
    objectiveKey: objectiveKey.toString(),
  })

  const {
    data: existingWorkItemsData,
    isLoading: existingWorkItemsQueryIsLoading,
    isError: existingWorkItemsQueryIsError,
  } = useGetObjectiveWorkItemsQuery({
    planningIntervalKey: planningIntervalKey.toString(),
    objectiveKey: objectiveKey.toString(),
  })

  const debounceSearchQuery = useDebounce(searchQuery, 500)
  const { data: searchResult, isFetching: isSearching } = useSearchWorkItemsQuery(debounceSearchQuery, {
    skip: debounceSearchQuery === '',
  })

  const [manageObjectiveWorkItems] = useManageObjectiveWorkItemsMutation()

  // Derive target work items: existing items (minus removed) plus user-added items
  const targetWorkItems = (() => {
    const existing =
      existingWorkItemsData?.workItems?.filter(
        (item) => !removedIds.has(item.id),
      ) ?? []
    return [...existing, ...addedItems].sort(defaultSort)
  })()

  // Derive source work items: search results minus items already in target
  const sourceWorkItems = (() => {
    const targetIds = new Set(targetWorkItems.map((item) => item.id))
    return (searchResult ?? [])
      .filter((item) => !targetIds.has(item.id))
      .sort(defaultSort)
  })()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request: ManagePlanningIntervalObjectiveWorkItemsRequest = {
          planningIntervalId: objectiveData?.planningInterval.id ?? '',
          objectiveId: objectiveData?.id ?? '',
          workItemIds: targetWorkItems.map((item) => item.id),
        }
        await manageObjectiveWorkItems({
          request,
          cacheKey: objectiveKey.toString(),
        })
        messageApi.success('Successfully updated objective work items.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          `Failed to update objective work items. Error: ${apiError.detail}`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while managing the work items. Please try again.',
  })

  const onDragStop = (items: WorkItemListDto[]) => {
    if (items.length === 0) return

    // Items dragged from source to target: add them and un-remove if needed
    setAddedItems((prev) => [...prev, ...items])
    setRemovedIds((prev) => {
      const next = new Set(prev)
      for (const item of items) {
        next.delete(item.id)
      }
      return next
    })
  }

  const handleDelete = (item: WorkItemListDto) => {
    if (!item) return

    const isExistingItem = existingWorkItemsData?.workItems?.some(
      (w) => w.id === item.id,
    )
    if (isExistingItem) {
      // Mark existing item as removed
      setRemovedIds((prev) => new Set(prev).add(item.id))
    } else {
      // Remove user-added item
      setAddedItems((prev) => prev.filter((p) => p.id !== item.id))
    }
  }

  const rightColDefs = asDeletableColDefs(workItemColDefs, handleDelete)

  const handleSearch = (e: ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
  }

  return (
    <Modal
      title="Manage PI Objective Work Items"
      open={isOpen}
      width={'80vw'}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {
        <Flex gap="small" vertical>
          <Input
            size="small"
            placeholder="Search for work items by key, title, or parent key"
            allowClear
            onChange={handleSearch}
            suffix={isSearching ? <LoadingOutlined spin /> : <SearchOutlined />}
          />
          <AgGridTransfer
            leftGridData={sourceWorkItems}
            rightGridData={targetWorkItems}
            leftColumnDef={leftColDefs}
            rightColumnDef={rightColDefs}
            onDragStop={onDragStop}
            getRowId={(param) => param.data.id}
            GridProps={{
              defaultColDef,
            }}
          />
          <Text>Search results are limited to 50 records.</Text>
        </Flex>
      }
    </Modal>
  )
}

export default ManagePlanningIntervalObjectiveWorkItemsForm
