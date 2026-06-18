'use client'

import { DatePicker, Form, Modal, Radio } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo } from 'react'
import { useModalForm } from '@/src/hooks'
import { useGetHealthStatusesQuery } from '@/src/store/features/common/health-checks-api'
import { useCreateProjectHealthCheckMutation } from '@/src/store/features/ppm/project-health-checks-api'
import { toFormErrors } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { HealthStatus } from '@/src/services/wayd-api'
const { Item } = Form
const { Group: RadioGroup } = Radio

interface CreateProjectHealthCheckFormValues {
  status: HealthStatus
  expiration: Dayjs
  note?: string
}

export interface CreateProjectHealthCheckFormProps {
  projectId: string
  onFormCreate: () => void
  onFormCancel: () => void
}

const datePresets = [
  { label: 'Tomorrow', value: dayjs().add(1, 'd').endOf('day') },
  { label: '1 Week', value: dayjs().add(7, 'd').endOf('day') },
  { label: '2 Weeks', value: dayjs().add(14, 'd').endOf('day') },
  { label: '1 Month', value: dayjs().add(30, 'd').endOf('day') },
]

const CreateProjectHealthCheckForm = ({
  projectId,
  onFormCreate,
  onFormCancel,
}: CreateProjectHealthCheckFormProps) => {
  const messageApi = useMessage()

  const { data: healthStatuses } = useGetHealthStatusesQuery()
  const statusOptions = useMemo(
    () =>
      healthStatuses?.map((status) => ({
        value: HealthStatus[status.name.replace(' ', '') as keyof typeof HealthStatus],
        label: status.name,
      })) ?? [],
    [healthStatuses],
  )

  const [createHealthCheck] = useCreateProjectHealthCheckMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateProjectHealthCheckFormValues>({
      permission: 'Permissions.Projects.Update',
      onSubmit: async (values, form) => {
        const response = await createHealthCheck({
          projectId,
          request: {
            status: values.status,
            expiration: values.expiration?.toDate(),
            note: values.note,
          },
        })

        if ('error' in response && response.error) {
          const error = response.error as {
            status?: number
            errors?: Record<string, string[]>
            detail?: string
          }
          if (error.status === 422 && error.errors) {
            form.setFields(toFormErrors(error.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              error.detail ??
                'An error occurred while creating the health check. Please try again.',
            )
          }
          return false
        }

        messageApi.success('Health check created.')
        return true
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
    })

  return (
    <Modal
      title="Create Health Check"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-project-health-check-form"
        initialValues={{
          expiration: dayjs().add(2, 'week').endOf('day'),
        }}
      >
        <Item name="status" label="Status" rules={[{ required: true }]}>
          <RadioGroup
            options={statusOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>
        <Item name="note" label="Note" initialValue="" rules={[{ max: 1024 }]}>
          <MarkdownEditor
            value={form.getFieldValue('note')}
            onChange={(value) => form.setFieldValue('note', value || '')}
            maxLength={1024}
          />
        </Item>
        <Item
          label="Expiration"
          name="expiration"
          rules={[
            { required: true },
            {
              validator: (_, value) =>
                value && dayjs() <= value
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('The Expiration must be in the future.'),
                    ),
            },
          ]}
        >
          <DatePicker
            presets={datePresets}
            showTime
            format="YYYY-MM-DD HH:mm"
            disabledDate={(value) => value && value < dayjs().startOf('day')}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateProjectHealthCheckForm
