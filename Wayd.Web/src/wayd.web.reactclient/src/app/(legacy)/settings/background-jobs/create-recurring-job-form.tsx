'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  BackgroundJobTypeDto,
  CreateRecurringJobRequest,
} from '@/src/services/wayd-api'
import { useCreateRecurringJobMutation } from '@/src/store/features/admin/background-jobs-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface CreateRecurringJobFormProps {
  jobTypes: BackgroundJobTypeDto[]
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateRecurringJobFormValues {
  jobId: string
  jobTypeId: number
  cronExpression: string
}

/**
 * Only schedulable types may be offered: the rest run on demand only, and the
 * API rejects them with a 400. The flag comes from the same server-side set the
 * recurring endpoint gates on, so the two cannot disagree.
 */
export const toSchedulableOptions = (jobTypes: BackgroundJobTypeDto[]) =>
  jobTypes
    .filter((jobType) => jobType.isSchedulable)
    .map((jobType) => ({ value: jobType.id, label: jobType.name }))

const mapToRequestValues = (values: CreateRecurringJobRequest) => {
  const request = {
    jobId: values.jobId,
    jobTypeId: values.jobTypeId,
    cronExpression: values.cronExpression,
  } as CreateRecurringJobRequest
  return request
}

const CreateRecurringJobForm = ({
  jobTypes,
  onFormCreate,
  onFormCancel,
}: CreateRecurringJobFormProps) => {
  const messageApi = useMessage()

  const [createRecurringJob] =
    useCreateRecurringJobMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateRecurringJobFormValues>({
      onSubmit: async (values: CreateRecurringJobFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await createRecurringJob(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully created recurring job.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                apiError.detail ??
                  'An unexpected error occurred while creating a recurring job.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while creating a recurring job.',
    })

  return (
    <Modal
      title="Create Recurring Job"
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
        name="create-recurring-job-form"
      >
        <Item label="Job Id" name="jobId" rules={[{ required: true }]}>
          <Input />
        </Item>
        <Item name="jobTypeId" label="Job Type" rules={[{ required: true }]}>
          <Select
            showSearch
            placeholder="Select a Job Type"
            optionFilterProp="children"
            filterOption={(input, option) =>
              (option?.label.toLowerCase() ?? '').includes(input.toLowerCase())
            }
            filterSort={(optionA, optionB) =>
              (optionA?.label ?? '')
                .toLowerCase()
                .localeCompare((optionB?.label ?? '').toLowerCase())
            }
            options={toSchedulableOptions(jobTypes)}
          />
        </Item>
        <Item
          label="Cron Expression"
          name="cronExpression"
          rules={[{ required: true }]}
        >
          <Input placeholder="Example for Every 5 minutes: */5 * * * *" />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateRecurringJobForm
