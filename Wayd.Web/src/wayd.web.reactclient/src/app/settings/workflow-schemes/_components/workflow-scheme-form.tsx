'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  StatusWorkflowState,
  WorkflowSchemeListDto,
} from '@/src/services/wayd-api'
import {
  useGetStatusWorkflowsQuery,
  useGetWorkflowOwnerTypesQuery,
} from '@/src/store/features/common/status-workflows-api'
import {
  useCloneWorkflowSchemeMutation,
  useCreateWorkflowSchemeMutation,
  useUpdateWorkflowSchemeMutation,
} from '@/src/store/features/common/workflow-schemes-api'
import { isApiError, toFormErrors, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

/** Create starts empty; edit and clone start from an existing scheme. */
export type WorkflowSchemeFormMode =
  | { kind: 'create' }
  | {
      kind: 'edit' | 'clone'
      scheme: Pick<WorkflowSchemeListDto, 'id' | 'name' | 'description'>
    }

export interface WorkflowSchemeFormProps {
  mode: WorkflowSchemeFormMode
  onFormComplete: () => void
  onFormCancel: () => void
}

interface WorkflowSchemeFormValues {
  ownerType?: string
  defaultWorkflowId?: string
  name: string
  description?: string
}

const titles = {
  create: 'Create Workflow Scheme',
  edit: 'Edit Workflow Scheme',
  clone: 'Clone Workflow Scheme',
}

/**
 * One form for the three ways a scheme's details are entered. Only creating
 * asks for an owner type and default workflow: a clone copies both, and an
 * edit cannot change them.
 */
const WorkflowSchemeForm = ({
  mode,
  onFormComplete,
  onFormCancel,
}: WorkflowSchemeFormProps) => {
  const messageApi = useMessage()

  const [createScheme] = useCreateWorkflowSchemeMutation()
  const [updateScheme] = useUpdateWorkflowSchemeMutation()
  const [cloneScheme] = useCloneWorkflowSchemeMutation()

  const { data: ownerTypes, isLoading: ownerTypesLoading } =
    useGetWorkflowOwnerTypesQuery(undefined, { skip: mode.kind !== 'create' })

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<WorkflowSchemeFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response =
            mode.kind === 'create'
              ? await createScheme({
                  ownerType: values.ownerType!,
                  defaultWorkflowId: values.defaultWorkflowId!,
                  name: values.name,
                  description: values.description,
                })
              : mode.kind === 'edit'
                ? await updateScheme({
                    id: mode.scheme.id,
                    request: {
                      name: values.name,
                      description: values.description,
                    },
                  })
                : await cloneScheme({
                    id: mode.scheme.id,
                    request: {
                      name: values.name,
                      description: values.description,
                    },
                  })
          if (response.error) throw response.error

          messageApi.success(
            mode.kind === 'edit'
              ? 'Workflow scheme updated.'
              : mode.kind === 'clone'
                ? 'Workflow scheme cloned.'
                : 'Workflow scheme created.',
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while saving the workflow scheme. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while saving the workflow scheme. Please try again.',
      permission:
        mode.kind === 'edit'
          ? 'Permissions.StatusWorkflows.Update'
          : 'Permissions.StatusWorkflows.Create',
    })

  const ownerType = Form.useWatch('ownerType', form)

  // A scheme's workflows must be published and of its owner type; the API
  // refuses anything else, so nothing else is offered.
  const { data: workflows, isFetching: workflowsLoading } =
    useGetStatusWorkflowsQuery(
      { ownerType, state: StatusWorkflowState.Published },
      { skip: mode.kind !== 'create' || !ownerType },
    )

  useEffect(() => {
    if (mode.kind === 'create') return
    form.setFieldsValue({
      name:
        mode.kind === 'clone' ? `${mode.scheme.name} (Copy)` : mode.scheme.name,
      description: mode.scheme.description,
    })
  }, [mode, form])

  return (
    <Modal
      title={titles[mode.kind]}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={mode.kind === 'edit' ? 'Save' : titles[mode.kind].split(' ')[0]}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="workflow-scheme-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} />
        </Item>
        {mode.kind === 'create' && (
          <>
            <Item
              label="Owner Type"
              name="ownerType"
              tooltip="The kind of record this scheme sends to workflows. Only kinds of record that have record types can have schemes."
              rules={[{ required: true, message: 'Owner Type is required' }]}
            >
              <Select
                loading={ownerTypesLoading}
                placeholder="Select an owner type"
                options={(ownerTypes ?? [])
                  .filter((o) => o.hasRecordTypes)
                  .map((o) => ({ value: o.key, label: o.displayName }))}
                onChange={() =>
                  form.setFieldValue('defaultWorkflowId', undefined)
                }
              />
            </Item>
            <Item
              label="Default Workflow"
              name="defaultWorkflowId"
              tooltip="The workflow records follow when their record type has no entry of its own."
              rules={[
                { required: true, message: 'Default Workflow is required' },
              ]}
            >
              <Select
                disabled={!ownerType}
                loading={workflowsLoading}
                placeholder="Select a published workflow"
                options={(workflows ?? []).map((w) => ({
                  value: w.id,
                  label: w.name,
                }))}
              />
            </Item>
          </>
        )}
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 3, maxRows: 8 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default WorkflowSchemeForm
