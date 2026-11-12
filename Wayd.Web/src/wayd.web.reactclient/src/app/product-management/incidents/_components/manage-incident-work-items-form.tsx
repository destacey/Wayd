'use client'

import { useConfirmModal, useDebounce } from '@/src/hooks'
import {
  ManageIncidentWorkItemsRequest,
  WorkItemListDto,
} from '@/src/services/wayd-api'
import {
  useGetIncidentWorkItemsQuery,
  useManageIncidentWorkItemsMutation,
} from '@/src/store/features/product-management/incidents-api'
import { useSearchWorkItemsQuery } from '@/src/store/features/work-management/workspace-api'
import { LoadingOutlined, SearchOutlined } from '@ant-design/icons'
import { Flex, Input, Modal, Typography } from 'antd'
import { ChangeEvent, useState } from 'react'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { createCsvColumn } from '@/src/components/common/wayd-grid'
import WaydGridTransfer from '@/src/components/common/wayd-grid-transfer'
import { useMessage } from '@/src/components/contexts/messaging'
import { workItemKeyComparator } from '@/src/components/common/work'
import { toApiError } from '@/src/utils'

const { Text } = Typography

export interface ManageIncidentWorkItemsFormProps {
  incidentId: string
  idOrKey: string
  onFormComplete: () => void
  onFormCancel: () => void
}

const defaultSort = (a: WorkItemListDto, b: WorkItemListDto) =>
  workItemKeyComparator(a.key, b.key)

const ManageIncidentWorkItemsForm = ({
  incidentId,
  idOrKey,
  onFormComplete,
  onFormCancel,
}: ManageIncidentWorkItemsFormProps) => {
  const [addedItems, setAddedItems] = useState<WorkItemListDto[]>([])
  const [removedIds, setRemovedIds] = useState<Set<string>>(new Set())
  const messageApi = useMessage()
  const [searchQuery, setSearchQuery] = useState<string>('')

  const {
    data: existingWorkItemsData,
    isError: existingWorkItemsQueryIsError,
  } = useGetIncidentWorkItemsQuery({ idOrKey, incidentId })

  const debounceSearchQuery = useDebounce(searchQuery, 500)
  const { data: searchResult, isFetching: isSearching } =
    useSearchWorkItemsQuery(debounceSearchQuery, {
      skip: debounceSearchQuery === '',
    })

  const [manageIncidentWorkItems] = useManageIncidentWorkItemsMutation()

  const targetWorkItems = (() => {
    const existing =
      existingWorkItemsData?.workItems?.filter(
        (item) => !removedIds.has(item.id),
      ) ?? []
    const existingIds = new Set(existing.map((item) => item.id))
    return [
      ...existing,
      ...addedItems.filter((item) => !existingIds.has(item.id)),
    ].sort(defaultSort)
  })()

  const sourceWorkItems = (() => {
    const targetIds = new Set(targetWorkItems.map((item) => item.id))
    return (searchResult ?? [])
      .filter((item) => !targetIds.has(item.id))
      .sort(defaultSort)
  })()

  const workItemColumns: ColumnDef<WorkItemListDto, any>[] = [
    { accessorKey: 'key', header: 'Key', size: 125 },
    { accessorKey: 'title', header: 'Title', size: 250 },
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
    createCsvColumn<WorkItemListDto>({
      id: 'tags',
      header: 'Tags',
      size: 200,
      getValues: (row) => row.tags ?? [],
    }),
  ]

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request: ManageIncidentWorkItemsRequest = {
          incidentId,
          workItemIds: targetWorkItems.map((item) => item.id),
        }
        await manageIncidentWorkItems(request).unwrap()
        messageApi.success('Successfully updated incident work items.')
        return true
      } catch (error) {
        const apiError = toApiError(error)
        messageApi.error(
          `Failed to update incident work items. Error: ${apiError.detail}`,
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

    setAddedItems((prev) => {
      const prevIds = new Set(prev.map((item) => item.id))
      return [...prev, ...items.filter((item) => !prevIds.has(item.id))]
    })
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
      setRemovedIds((prev) => new Set(prev).add(item.id))
    } else {
      setAddedItems((prev) => prev.filter((p) => p.id !== item.id))
    }
  }

  const handleSearch = (e: ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
  }

  return (
    <Modal
      title="Manage work items"
      open={isOpen}
      width="80vw"
      onOk={() => void handleOk()}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {existingWorkItemsQueryIsError && (
        <Text type="danger">Failed to load existing work items.</Text>
      )}
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
    </Modal>
  )
}

export default ManageIncidentWorkItemsForm
