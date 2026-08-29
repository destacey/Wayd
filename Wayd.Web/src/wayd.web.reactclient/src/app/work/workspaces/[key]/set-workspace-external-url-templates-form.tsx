'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  SetExternalUrlTemplatesRequest,
  WorkspaceDto,
} from '@/src/services/wayd-api'
import {
  SetWorkspaceExternalUrlTemplatesRequest,
  useGetWorkspaceQuery,
  useSetWorkspaceExternalUrlTemplatesMutation,
} from '@/src/store/features/work-management/workspace-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface SetWorkspaceExternalUrlTemplatesFormProps {
  workspaceId: string
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface SetWorkspaceExternalUrlTemplatesFormValues {
  externalViewWorkItemUrlTemplate?: string | undefined
}

const mapToRequestValues = (
  workspaceId: string,
  values: SetWorkspaceExternalUrlTemplatesFormValues,
): SetWorkspaceExternalUrlTemplatesRequest => {
  return {
    workspaceId: workspaceId,
    externalUrlTemplatesRequest: {
      externalViewWorkItemUrlTemplate: values.externalViewWorkItemUrlTemplate,
    } as SetExternalUrlTemplatesRequest,
  }
}

const SetWorkspaceExternalUrlTemplatesForm = (
  props: SetWorkspaceExternalUrlTemplatesFormProps,
) => {
  const messageApi = useMessage()

  const {
    data: workspaceData,
    isLoading,
    refetch,
  } = useGetWorkspaceQuery(props.workspaceId)
  const [setWorkspaceExternalUrlTemplatesMutation] =
    useSetWorkspaceExternalUrlTemplatesMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetWorkspaceExternalUrlTemplatesFormValues>({
      onSubmit: async (values: SetWorkspaceExternalUrlTemplatesFormValues, form) => {
          try {
            const request = mapToRequestValues(props.workspaceId, values)
            const response =
              await setWorkspaceExternalUrlTemplatesMutation(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully set external URL templates')
            refetch()
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An unexpected error occurred while updating external URL templates.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: props.onFormUpdate,
      onCancel: props.onFormCancel,
    })

  useEffect(() => {
    if (!workspaceData || !isOpen) return
    const mapToFormValues = (workspace: WorkspaceDto) => {
      form.setFieldsValue({
        externalViewWorkItemUrlTemplate:
          workspace.externalViewWorkItemUrlTemplate,
      })
    }
    mapToFormValues(workspaceData)
  }, [form, workspaceData, isOpen])

  return (
    <Modal
      title="Set External URL Templates"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Item
          label="View Work Item URL"
          name="externalViewWorkItemUrlTemplate"
          extra={
            <span>
              <br />
              This template plus the work item external id will create a url to
              view the work item in the external system.
            </span>
          }
          rules={[
            {
              max: 256,
              message: 'The URL must be equal to or less than 256 characters',
            },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 4 }}
            showCount
            maxLength={256}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default SetWorkspaceExternalUrlTemplatesForm
