'use client'

import { createTypedFormItem } from '@/src/components/common/forms/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  CreateStrategicInitiativeKpiRequest,
  KpiTargetDirection,
} from '@/src/services/wayd-api'
import { useCreateStrategicInitiativeKpiMutation } from '@/src/store/features/ppm/strategic-initiatives-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, InputNumber, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'

export interface CreateStrategicInitiativeKpiFormProps {
  strategicInitiativeId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateStrategicInitiativeKpiFormValues {
  name: string
  description?: string
  startingValue?: number
  targetValue: number
  prefix?: string
  suffix?: string
  targetDirection: KpiTargetDirection
}

const TypedFormItem =
  createTypedFormItem<CreateStrategicInitiativeKpiFormValues>()

const kpiTargetDirectionOptions = [
  { label: 'Increase', value: KpiTargetDirection.Increase },
  { label: 'Decrease', value: KpiTargetDirection.Decrease },
]

const mapToRequestValues = (
  values: CreateStrategicInitiativeKpiFormValues,
  strategicInitiativeId: string,
): CreateStrategicInitiativeKpiRequest => {
  return {
    strategicInitiativeId: strategicInitiativeId,
    name: values.name,
    description: values.description,
    startingValue: values.startingValue,
    targetValue: values.targetValue,
    prefix: values.prefix,
    suffix: values.suffix,
    targetDirection: values.targetDirection,
  }
}

const CreateStrategicInitiativeKpiForm = ({
  strategicInitiativeId,
  onFormComplete,
  onFormCancel,
}: CreateStrategicInitiativeKpiFormProps) => {
  const messageApi = useMessage()

  const [createKpi] = useCreateStrategicInitiativeKpiMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateStrategicInitiativeKpiFormValues>({
      onSubmit: async (values: CreateStrategicInitiativeKpiFormValues, form) => {
        try {
          const request = mapToRequestValues(values, strategicInitiativeId)
          const response = await createKpi(request)
          if (response.error) throw response.error

          messageApi.success(
            'KPI created successfully. KPI key: ' + response.data!.key,
          )
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
                'An error occurred while creating the KPI. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the KPI. Please try again.',
      permission: 'Permissions.StrategicInitiatives.Update',
    })

  return (
    <Modal
      title="Create Strategic Initiative KPI"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-strategic-initiative-kpi-form"
      >
        <TypedFormItem
          name="name"
          label="Name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 64 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={64}
          />
        </TypedFormItem>
        <TypedFormItem
          name="description"
          label="Description"
          rules={[{ max: 512 }]}
        >
          <MarkdownEditor maxLength={512} />
        </TypedFormItem>
        <TypedFormItem name="startingValue" label="Starting Value">
          <InputNumber style={{ width: 200 }} />
        </TypedFormItem>
        <TypedFormItem
          name="targetValue"
          label="Target Value"
          rules={[{ required: true, message: 'Target Value is required' }]}
        >
          <InputNumber style={{ width: 200 }} />
        </TypedFormItem>
        <TypedFormItem
          name="prefix"
          label="Prefix"
          rules={[{ max: 8 }]}
        >
          <Input
            placeholder="e.g. $, €"
            maxLength={8}
            style={{ width: 120 }}
          />
        </TypedFormItem>
        <TypedFormItem
          name="suffix"
          label="Suffix"
          rules={[{ max: 8 }]}
        >
          <Input
            placeholder="e.g. %, K, M"
            maxLength={8}
            style={{ width: 120 }}
          />
        </TypedFormItem>
        <TypedFormItem
          name="targetDirection"
          label="Target Direction"
          rules={[
            { required: true, message: 'Target Direction is required' },
          ]}
        >
          <Select
            allowClear
            options={kpiTargetDirectionOptions}
            placeholder="Select Target Direction"
          />
        </TypedFormItem>
      </Form>
    </Modal>
  )
}

export default CreateStrategicInitiativeKpiForm
