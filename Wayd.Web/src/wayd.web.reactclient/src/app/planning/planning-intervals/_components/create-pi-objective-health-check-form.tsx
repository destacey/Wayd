'use client'

import { DatePicker, Form, Modal, Radio } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useMemo } from 'react'
import { useModalForm } from '@/src/hooks'
import { useGetHealthStatusesQuery } from '@/src/store/features/common/health-checks-api'
import { useCreateObjectiveHealthCheckMutation } from '@/src/store/features/planning/pi-objective-health-checks-api'
import { toFormErrors } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import { MarkdownEditor } from '@/src/components/common/markdown'

const { Item } = Form
const { Group: RadioGroup } = Radio

export interface CreateHealthCheckFormValues {
  statusId: number
  expiration: Dayjs
  note?: string | undefined
}

export interface CreateHealthCheckFormProps {
  planningIntervalId: string
  objectiveId: string
  onFormCreate: () => void
  onFormCancel: () => void
}

const datePresets = [
  { label: 'Tomorrow', value: dayjs().add(1, 'd').endOf('day') },
  { label: '1 Week', value: dayjs().add(7, 'd').endOf('day') },
  { label: '2 Weeks', value: dayjs().add(14, 'd').endOf('day') },
  { label: '1 Month', value: dayjs().add(30, 'd').endOf('day') },
]

const CreateHealthCheckForm = ({
  planningIntervalId,
  objectiveId,
  onFormCreate,
  onFormCancel,
}: CreateHealthCheckFormProps) => {
  const messageApi = useMessage()

  const { data: healthStatuses } = useGetHealthStatusesQuery()
  const statusOptions = useMemo(
    () =>
      healthStatuses?.map((status) => ({
        value: status.id,
        label: status.name,
      })) ?? [],
    [healthStatuses],
  )

  const [createHealthCheck] = useCreateObjectiveHealthCheckMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateHealthCheckFormValues>({
      permission: 'Permissions.PlanningIntervalObjectives.Manage',
      onSubmit: async (values, form) => {
        const response = await createHealthCheck({
          planningIntervalId,
          objectiveId,
          request: {
            planningIntervalObjectiveId: objectiveId,
            statusId: values.statusId,
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
        name="create-health-check-form"
        initialValues={{
          expiration: dayjs().add(2, 'week').endOf('day'),
        }}
      >
        <Item name="statusId" label="Status" rules={[{ required: true }]}>
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

export default CreateHealthCheckForm

