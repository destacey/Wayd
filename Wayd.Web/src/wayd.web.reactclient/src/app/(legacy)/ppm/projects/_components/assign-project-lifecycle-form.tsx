'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  AssignProjectLifecycleRequest,
  ProjectDetailsDto,
  ProjectLifecycleState,
} from '@/src/services/wayd-api'
import {
  useGetProjectLifecycleQuery,
  useGetProjectLifecyclesQuery,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { useAssignProjectLifecycleMutation } from '@/src/store/features/ppm/projects-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Card, Flex, Form, Modal, Select, Timeline, Typography } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { Text } = Typography

export interface AssignProjectLifecycleFormProps {
  project: ProjectDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AssignProjectLifecycleFormValues {
  lifecycleId: string
}

const AssignProjectLifecycleForm = ({
  project,
  onFormComplete,
  onFormCancel,
}: AssignProjectLifecycleFormProps) => {
  const messageApi = useMessage()

  const [assignProjectLifecycle] = useAssignProjectLifecycleMutation()

  const {
    data: lifecycleData,
    isLoading: lifecyclesLoading,
    error: lifecyclesError,
  } = useGetProjectLifecyclesQuery(ProjectLifecycleState.Active)

  const lifecycleOptions = !lifecycleData ? [] : [...lifecycleData]
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((lc) => ({
        label: lc.name,
        value: lc.id,
      }))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AssignProjectLifecycleFormValues>({
      onSubmit: async (values: AssignProjectLifecycleFormValues, form) => {
          try {
            const request: AssignProjectLifecycleRequest = {
              lifecycleId: values.lifecycleId,
            }
            const response = await assignProjectLifecycle({
              id: project.id,
              request,
              cacheKey: project.key,
            })
            if (response.error) throw response.error

            messageApi.success('Project lifecycle assigned successfully.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                (isApiError(apiError) ? apiError.detail : undefined) ??
                  'An error occurred while assigning the lifecycle. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while assigning the lifecycle. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  const selectedLifecycleId = Form.useWatch('lifecycleId', form)

  const { data: selectedLifecycle } = useGetProjectLifecycleQuery(
    selectedLifecycleId,
    { skip: !selectedLifecycleId },
  )

  const stageItems = !selectedLifecycle?.stages ? [] : [...selectedLifecycle.stages]
      .sort((a, b) => a.order - b.order)
      .map((stage) => ({
        content: (
          <>
            <Text strong>{stage.name}</Text>
            <br />
            <Text type="secondary">{stage.description}</Text>
          </>
        ),
      }))

  useEffect(() => {
    if (lifecyclesError) {
      console.error(lifecyclesError)
      messageApi.error(
        (isApiError(lifecyclesError) ? lifecyclesError.detail : undefined) ??
          'An error occurred while loading lifecycle options.',
      )
    }
  }, [lifecyclesError, messageApi])

  return (
    <Modal
      title="Assign Project Lifecycle"
      open={isOpen}
      width={'40vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Assign"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Text type="secondary">
          Select a lifecycle to assign to this project. This will create the
          project stages from the lifecycle template.
        </Text>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="assign-project-lifecycle-form"
        >
          <Item
            name="lifecycleId"
            label="Lifecycle"
            rules={[{ required: true, message: 'Lifecycle is required' }]}
          >
            <Select
              options={lifecycleOptions}
              placeholder="Select Lifecycle"
              loading={lifecyclesLoading}
            />
          </Item>
        </Form>
        {stageItems.length > 0 && (
          <Card size="small" title="Stages">
            <Timeline items={stageItems} />
          </Card>
        )}
      </Flex>
    </Modal>
  )
}

export default AssignProjectLifecycleForm
