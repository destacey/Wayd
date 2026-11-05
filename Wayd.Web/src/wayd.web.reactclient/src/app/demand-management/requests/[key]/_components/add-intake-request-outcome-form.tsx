'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDebounce, useModalForm } from '@/src/hooks'
import {
  IntakeRequestDetailsDto,
  IntakeRequestOutcomeKind,
} from '@/src/services/wayd-api'
import { useAddIntakeRequestOutcomeMutation } from '@/src/store/features/demand-management/demand-management-api'
import { useGetProjectOptionsQuery } from '@/src/store/features/ppm/projects-api'
import { useSearchWorkItemsQuery } from '@/src/store/features/work-management/workspace-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal, Radio, Select, Typography } from 'antd'
import { FC, useState } from 'react'

const { Item } = Form
const { Paragraph } = Typography
const { TextArea } = Input

export interface AddIntakeRequestOutcomeFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddOutcomeFormValues {
  kind: IntakeRequestOutcomeKind
  projectId?: string
  workItemId?: string
  note?: string
}

/**
 * Records a project or work item the request became. Offered only where the server's `canChangeOutcomes`
 * says so. The project list is the reader's own view of PPM, so a project they cannot see is not offered;
 * the work item search needs permission to view work items, and reports a failure without it.
 */
const AddIntakeRequestOutcomeForm: FC<AddIntakeRequestOutcomeFormProps> = ({
  request,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [addOutcome] = useAddIntakeRequestOutcomeMutation()
  const {
    data: projects,
    isLoading: projectsLoading,
    isError: projectsFailed,
  } = useGetProjectOptionsQuery()

  const [workItemSearch, setWorkItemSearch] = useState('')
  const typedTerm = workItemSearch.trim()
  const searchTerm = useDebounce(typedTerm, 500)
  const {
    data: workItems,
    isFetching: workItemsSearching,
    isError: workItemsFailed,
  } = useSearchWorkItemsQuery(searchTerm, { skip: searchTerm === '' })

  const alreadyLinked = new Set(request.outcomes.map((o) => o.targetId))
  const projectOptions = (projects ?? []).filter(
    (p) => !alreadyLinked.has(p.value as string),
  )
  // A chosen work item keeps its label after a later search drops it: the select caches the options it has shown.
  const workItemOptions = (workItems ?? [])
    .filter((w) => !alreadyLinked.has(w.id))
    .map((w) => ({ label: `${w.key} · ${w.title}`, value: w.id }))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddOutcomeFormValues>({
      onSubmit: async (values, form) => {
        const isWorkItem = values.kind === IntakeRequestOutcomeKind.WorkItem
        try {
          await addOutcome({
            requestId: request.summary.id,
            request: {
              kind: values.kind,
              projectId: isWorkItem ? undefined : values.projectId,
              workItemId: isWorkItem ? values.workItemId : undefined,
              note: values.note,
            },
          }).unwrap()
          messageApi.success('Outcome added.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to add the outcome.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to add the outcome.',
    })

  const kind = Form.useWatch('kind', form)
  const noteRequired = kind === IntakeRequestOutcomeKind.ExistingProjectScope

  return (
    <Modal
      title="Add Outcome"
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
        Record what this request became. The project or work item itself is not
        changed.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-intake-request-outcome-form"
        initialValues={{ kind: IntakeRequestOutcomeKind.NewProject }}
      >
        <Item name="kind" label="Outcome" rules={[{ required: true }]}>
          <Radio.Group
            options={[
              {
                label: 'New project',
                value: IntakeRequestOutcomeKind.NewProject,
              },
              {
                label: 'Scope added to an existing project',
                value: IntakeRequestOutcomeKind.ExistingProjectScope,
              },
              {
                label: 'Work item',
                value: IntakeRequestOutcomeKind.WorkItem,
              },
            ]}
          />
        </Item>
        {/* Reads the kind from the form's store, not useWatch, so the picker swaps on the same render. */}
        <Item noStyle dependencies={['kind']}>
          {({ getFieldValue }) =>
            getFieldValue('kind') === IntakeRequestOutcomeKind.WorkItem ? (
              <Item
                name="workItemId"
                label="Work Item"
                rules={[{ required: true, message: 'Choose a work item.' }]}
                validateStatus={workItemsFailed ? 'error' : undefined}
                help={
                  workItemsFailed
                    ? 'The work items could not be loaded.'
                    : undefined
                }
              >
                <Select
                  showSearch={{
                    filterOption: false,
                    onSearch: setWorkItemSearch,
                  }}
                  placeholder="Search by key or title"
                  loading={workItemsSearching || typedTerm !== searchTerm}
                  options={workItemOptions}
                  notFoundContent={
                    typedTerm === ''
                      ? 'Type to search for a work item.'
                      : undefined
                  }
                />
              </Item>
            ) : (
              <Item
                name="projectId"
                label="Project"
                rules={[{ required: true, message: 'Choose a project.' }]}
                validateStatus={projectsFailed ? 'error' : undefined}
                help={
                  projectsFailed
                    ? 'The projects could not be loaded.'
                    : undefined
                }
              >
                <Select
                  showSearch={{ optionFilterProp: 'label' }}
                  placeholder="Select a project"
                  loading={projectsLoading}
                  options={projectOptions}
                />
              </Item>
            )
          }
        </Item>
        <Item
          name="note"
          label="Note"
          required={noteRequired}
          dependencies={['kind']}
          rules={[
            ({ getFieldValue }) => ({
              required:
                getFieldValue('kind') ===
                IntakeRequestOutcomeKind.ExistingProjectScope,
              whitespace: true,
              message: 'Say what the project absorbed from this request.',
            }),
            { max: 1024 },
          ]}
          extra={
            noteRequired
              ? 'What the project took on from this request.'
              : kind === IntakeRequestOutcomeKind.WorkItem
                ? 'Optional: what the work item covers of this request.'
                : undefined
          }
        >
          <TextArea autoSize={{ minRows: 2, maxRows: 6 }} maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddIntakeRequestOutcomeForm
