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
import type { ColumnDef } from '@tanstack/react-table'
import {
  caseInsensitiveCompare,
  createMultiValueSetFilter,
} from '@/src/components/common/wayd-grid'
import WaydGridTransfer from '@/src/components/common/wayd-grid-transfer'
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

/** A work item's tags (source for the cell, the Tags set filter, and CSV
 *  export). */
const workItemTags = (item: WorkItemListDto): string[] => item.tags ?? []

const tagsFilter = createMultiValueSetFilter<WorkItemListDto>(workItemTags)

const defaultSort = (a: WorkItemListDto, b: WorkItemListDto) => {
  return workItemKeyComparator(a.key, b.key)
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

  // Distinct individual tags across both grids, for the Tags set filter's
  // checkbox list (individual tags, not whole combinations).
  const tagFilterOptions = (() => {
    const names = new Set<string>()
    for (const item of [...sourceWorkItems, ...targetWorkItems]) {
      for (const tag of workItemTags(item)) {
        names.add(tag)
      }
    }
    return Array.from(names)
      .sort(caseInsensitiveCompare)
      .map((name) => ({ label: name, value: name }))
  })()

  const workItemColumns: ColumnDef<WorkItemListDto, any>[] = [
    {
      accessorKey: 'key',
      header: 'Key',
      size: 125,
    },
    {
      accessorKey: 'title',
      header: 'Title',
      size: 250,
    },
    {
      accessorKey: 'type.name',
      header: 'Type',
      size: 100,
      meta: { filterType: 'set' },
    },
    {
      accessorKey: 'status',
      header: 'Status',
      size: 100,
      meta: { filterType: 'set' },
    },
    {
      accessorKey: 'team.name',
      header: 'Team',
      size: 150,
      meta: { filterType: 'set' },
    },
    {
      accessorKey: 'parent.key',
      header: 'Parent Key',
      size: 125,
    },
    {
      accessorKey: 'sprint.name',
      header: 'Sprint',
      size: 200,
      meta: { filterType: 'set' },
    },
    {
      accessorKey: 'project.name',
      header: 'Project',
      size: 200,
      meta: { filterType: 'set' },
    },
    {
      id: 'tags',
      header: 'Tags',
      size: 200,
      accessorFn: (row) => workItemTags(row).join(', '),
      filterFn: tagsFilter,
      meta: { filterType: 'set', filterOptions: tagFilterOptions },
      cell: ({ row }) => <WorkItemTagsCell tags={row.original.tags} />,
    },
  ]

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

  const handleMove = (items: WorkItemListDto[]) => {
    if (items.length === 0) return

    // Items moved from source to target: add them and un-remove if needed
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
          <WaydGridTransfer
            leftData={sourceWorkItems}
            rightData={targetWorkItems}
            columns={workItemColumns}
            getRowId={(item) => item.id}
            getDragLabel={(item) => item.key}
            onMove={handleMove}
            onRemove={handleDelete}
          />
          <Text>Search results are limited to 50 records.</Text>
        </Flex>
      }
    </Modal>
  )
}

export default ManagePlanningIntervalObjectiveWorkItemsForm
