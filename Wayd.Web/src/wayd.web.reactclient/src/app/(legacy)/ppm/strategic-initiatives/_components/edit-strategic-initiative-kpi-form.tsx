'use client'

import { createTypedFormItem } from '@/src/components/common/forms/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  KpiTargetDirection,
  UpdateStrategicInitiativeKpiRequest,
} from '@/src/services/wayd-api'
import {
  useGetStrategicInitiativeKpiQuery,
  useUpdateStrategicInitiativeKpiMutation,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, InputNumber, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'

export interface EditStrategicInitiativeKpiFormProps {
  strategicInitiativeId: string
  kpiId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditStrategicInitiativeKpiFormValues {
  name: string
  description?: string
  startingValue?: number
  targetValue: number
  prefix?: string
  suffix?: string
  targetDirection: KpiTargetDirection
}

const TypedFormItem =
  createTypedFormItem<EditStrategicInitiativeKpiFormValues>()

const kpiTargetDirectionOptions = [
  { label: 'Increase', value: KpiTargetDirection.Increase },
  { label: 'Decrease', value: KpiTargetDirection.Decrease },
]

const mapToRequestValues = (
  values: EditStrategicInitiativeKpiFormValues,
  strategicInitiativeId: string,
  kpiId: string,
): UpdateStrategicInitiativeKpiRequest => {
  return {
    strategicInitiativeId: strategicInitiativeId,
    kpiId: kpiId,
    name: values.name,
    description: values.description,
    startingValue: values.startingValue,
    targetValue: values.targetValue,
    prefix: values.prefix,
    suffix: values.suffix,
    targetDirection: values.targetDirection,
  }
}

const EditStrategicInitiativeKpiForm = ({
  strategicInitiativeId,
  kpiId,
  onFormComplete,
  onFormCancel,
}: EditStrategicInitiativeKpiFormProps) => {
  const messageApi = useMessage()

  const { data: kpiData, error: kpiError } = useGetStrategicInitiativeKpiQuery({
    strategicInitiativeId,
    kpiId,
  })

  const [updateKpi] = useUpdateStrategicInitiativeKpiMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditStrategicInitiativeKpiFormValues>({
      onSubmit: async (values: EditStrategicInitiativeKpiFormValues, form) => {
        try {
          const request = mapToRequestValues(
            values,
            strategicInitiativeId,
            kpiId,
          )
          const response = await updateKpi(request)
          if (response.error) throw response.error

          messageApi.success('KPI updated successfully.')
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
                'An error occurred while updating the KPI. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the KPI. Please try again.',
      permission: 'Permissions.StrategicInitiatives.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!kpiData) return

    form.setFieldsValue({
      name: kpiData.name,
      description: kpiData.description,
      startingValue: kpiData.startingValue,
      targetValue: kpiData.targetValue,
      prefix: kpiData.prefix,
      suffix: kpiData.suffix,
      targetDirection: kpiData.targetDirection,
    })
  }, [kpiData, form])

  useEffect(() => {
    if (kpiError) {
      console.error(kpiError)
      messageApi.error((isApiError(kpiError) ? kpiError.detail : undefined) || 'An error occurred while loading form data.')
    }
  }, [kpiError, messageApi])

  return (
    <Modal
      title="Edit Strategic Initiative KPI"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-strategic-initiative-kpi-form"
      >
        <TypedFormItem
          name="name"
          label="Name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
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
        <TypedFormItem name="prefix" label="Prefix" rules={[{ max: 8 }]}>
          <Input placeholder="e.g. $, €" maxLength={8} style={{ width: 120 }} />
        </TypedFormItem>
        <TypedFormItem name="suffix" label="Suffix" rules={[{ max: 8 }]}>
          <Input
            placeholder="e.g. %, K, M"
            maxLength={8}
            style={{ width: 120 }}
          />
        </TypedFormItem>
        <TypedFormItem
          name="targetDirection"
          label="Target Direction"
          rules={[{ required: true, message: 'Target Direction is required' }]}
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

export default EditStrategicInitiativeKpiForm
