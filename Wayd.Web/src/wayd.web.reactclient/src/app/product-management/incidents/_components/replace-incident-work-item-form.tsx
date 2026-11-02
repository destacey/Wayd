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
import { Flex, Input, Modal, Radio, Typography } from 'antd'
import { ChangeEvent, useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface ReplaceIncidentWorkItemFormProps {
  incidentId: string
  idOrKey: string
  workItem: WorkItemListDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const ReplaceIncidentWorkItemForm = ({
  incidentId,
  idOrKey,
  workItem,
  onFormComplete,
  onFormCancel,
}: ReplaceIncidentWorkItemFormProps) => {
  const messageApi = useMessage()
  const [searchQuery, setSearchQuery] = useState('')
  const [replacementId, setReplacementId] = useState<string | null>(null)

  const { data: existingWorkItemsData } = useGetIncidentWorkItemsQuery({
    idOrKey,
    incidentId,
  })

  const debouncedSearch = useDebounce(searchQuery, 500)
  const { data: searchResult, isFetching: isSearching } =
    useSearchWorkItemsQuery(debouncedSearch, {
      skip: debouncedSearch === '',
    })

  const [manageIncidentWorkItems] = useManageIncidentWorkItemsMutation()

  const linkedIds = new Set(
    (existingWorkItemsData?.workItems ?? []).map((item) => item.id),
  )

  const candidates = (searchResult ?? []).filter(
    (item) => item.id !== workItem.id && !linkedIds.has(item.id),
  )

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      if (!replacementId || replacementId === workItem.id) {
        messageApi.error('Choose a different work item.')
        return false
      }

      const currentIds =
        existingWorkItemsData?.workItems?.map((item) => item.id) ?? []
      const workItemIds = currentIds.map((id) =>
        id === workItem.id ? replacementId : id,
      )

      try {
        const request: ManageIncidentWorkItemsRequest = {
          incidentId,
          workItemIds,
        }
        await manageIncidentWorkItems(request).unwrap()
        messageApi.success('Work item link updated.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          `Failed to update work item link. Error: ${apiError.detail ?? ''}`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while updating the work item link. Please try again.',
  })

  const handleSearch = (e: ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
    setReplacementId(null)
  }

  return (
    <Modal
      title="Change work item link"
      open={isOpen}
      width={520}
      onOk={() => void handleOk()}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      okButtonProps={{ disabled: !replacementId }}
      keyboard={false}
      destroyOnHidden
    >
      <Flex gap="small" vertical>
        <Text type="secondary">
          Current: {workItem.key} — {workItem.title}
        </Text>
        <Input
          size="small"
          placeholder="Search for a work item by key, title, or parent key"
          allowClear
          onChange={handleSearch}
          suffix={isSearching ? <LoadingOutlined spin /> : <SearchOutlined />}
        />
        {debouncedSearch === '' ? (
          <Text type="secondary">Search to choose a replacement.</Text>
        ) : candidates.length === 0 ? (
          <Text type="secondary">No matching work items.</Text>
        ) : (
          <Radio.Group
            className="w-full"
            value={replacementId}
            onChange={(event) => setReplacementId(event.target.value)}
          >
            <Flex gap={4} vertical>
              {candidates.map((item) => (
                <Radio key={item.id} value={item.id}>
                  {item.key} — {item.title}
                </Radio>
              ))}
            </Flex>
          </Radio.Group>
        )}
        <Text type="secondary">Search results are limited to 50 records.</Text>
      </Flex>
    </Modal>
  )
}

export default ReplaceIncidentWorkItemForm
