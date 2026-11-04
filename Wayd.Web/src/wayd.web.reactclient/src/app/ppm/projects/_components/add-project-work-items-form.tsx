'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDebounce, useModalForm } from '@/src/hooks'
import { WorkItemListDto, WorkTypeTier } from '@/src/services/wayd-api'
import { useAddProjectWorkItemsMutation } from '@/src/store/features/ppm/projects-api'
import { useSearchWorkItemsQuery } from '@/src/store/features/work-management/workspace-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Flex, Form, Modal, Select, Typography } from 'antd'
import { useState } from 'react'

const { Item } = Form

/** The most work items the API links in one request. */
const MAX_WORK_ITEMS = 100
const { Paragraph, Text } = Typography

export interface AddProjectWorkItemsFormProps {
  projectId: string
  /** Ids of the work items already in the project's list, left out of the search results. */
  existingWorkItemIds: ReadonlySet<string>
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddProjectWorkItemsFormValues {
  workItemIds: string[]
}

const AddProjectWorkItemsForm = ({
  projectId,
  existingWorkItemIds,
  onFormComplete,
  onFormCancel,
}: AddProjectWorkItemsFormProps) => {
  const messageApi = useMessage()
  const [addProjectWorkItems] = useAddProjectWorkItemsMutation()

  const [search, setSearch] = useState('')
  const typedTerm = search.trim()
  const searchTerm = useDebounce(typedTerm, 500)
  const {
    data: searchResults,
    isFetching: isSearching,
    isError: searchFailed,
  } = useSearchWorkItemsQuery(
    { query: searchTerm, tier: WorkTypeTier.Portfolio },
    { skip: searchTerm === '' },
  )

  // Chosen work items outlive the search that found them, so their project can still be shown.
  const [chosen, setChosen] = useState<WorkItemListDto[]>([])

  const options = (searchResults ?? [])
    .filter((w) => !existingWorkItemIds.has(w.id))
    .map((w) => ({ label: `${w.key} · ${w.title}`, value: w.id, item: w }))

  const handleChange = (ids: string[]) => {
    const known = new Map(
      [...chosen, ...(searchResults ?? [])].map((w) => [w.id, w]),
    )
    setChosen(
      ids.map((id) => known.get(id)).filter((w): w is WorkItemListDto => !!w),
    )
  }

  const moving = chosen.filter((w) => w.project && w.project.id !== projectId)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddProjectWorkItemsFormValues>({
      onSubmit: async (values, form) => {
        try {
          await addProjectWorkItems({
            projectId,
            workItemIds: values.workItemIds,
          }).unwrap()
          messageApi.success(
            values.workItemIds.length === 1
              ? 'Work item added to the project.'
              : `${values.workItemIds.length} work items added to the project.`,
          )
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to add the work items to the project.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to add the work items to the project.',
      permission: 'Permissions.Projects.ManageProjectWorkItems',
    })

  return (
    <Modal
      title="Add Work Items"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        Only portfolio-level work items, such as epics and features, can be
        added. The work items beneath them join the project too, unless they
        have a project of their own.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-project-work-items-form"
      >
        <Item
          name="workItemIds"
          label="Work Items"
          rules={[
            { required: true, message: 'Choose at least one work item.' },
          ]}
          validateStatus={searchFailed ? 'error' : undefined}
          help={
            searchFailed ? 'The work items could not be loaded.' : undefined
          }
        >
          <Select
            mode="multiple"
            maxCount={MAX_WORK_ITEMS}
            // Keeps the typed term on screen after a pick, so it matches the results still listed.
            autoClearSearchValue={false}
            showSearch={{ filterOption: false, onSearch: setSearch }}
            placeholder="Search by key or title"
            loading={isSearching || typedTerm !== searchTerm}
            options={options}
            onChange={handleChange}
            notFoundContent={
              typedTerm === '' ? 'Type to search for work items.' : undefined
            }
            optionRender={(option) => {
              const item = option.data.item
              return (
                <Flex vertical>
                  <Text>{option.label}</Text>
                  <Text type="secondary">
                    {item.type.name}
                    {item.project ? ` · ${item.project.name}` : ''}
                  </Text>
                </Flex>
              )
            }}
          />
        </Item>
      </Form>
      {moving.length > 0 && (
        <Alert
          type="warning"
          showIcon
          title="These work items will move from their current project:"
          description={
            <ul>
              {moving.map((w) => (
                <li key={w.id}>
                  {w.key} · {w.project?.name}
                </li>
              ))}
            </ul>
          }
        />
      )}
    </Modal>
  )
}

export default AddProjectWorkItemsForm
