'use client'

import { createTypedFormItem } from '@/src/components/common/forms/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { AddStrategicInitiativeKpiMeasurementRequest } from '@/src/services/wayd-api'
import {
  useAddStrategicInitiativeKpiMeasurementMutation,
  useGetStrategicInitiativeKpiQuery,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Descriptions, Form, InputNumber, Modal } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect } from 'react'

const { Item: DescriptionItem } = Descriptions

export interface AddStrategicInitiativeKpiMeasurementFormProps {
  strategicInitiativeId: string
  kpiId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddStrategicInitiativeKpiMeasurementFormValues {
  actualValue: number
  measurementDate: Dayjs
  note: string
}

const TypedFormItem =
  createTypedFormItem<AddStrategicInitiativeKpiMeasurementFormValues>()

const mapToRequestValues = (
  values: AddStrategicInitiativeKpiMeasurementFormValues,
  strategicInitiativeId: string,
  kpiId: string,
): AddStrategicInitiativeKpiMeasurementRequest => {
  return {
    strategicInitiativeId: strategicInitiativeId,
    kpiId: kpiId,
    actualValue: values.actualValue,
    measurementDate: values.measurementDate?.toDate(),
    note: values.note,
  }
}

const AddStrategicInitiativeKpiMeasurementForm = ({
  strategicInitiativeId,
  kpiId,
  onFormComplete,
  onFormCancel,
}: AddStrategicInitiativeKpiMeasurementFormProps) => {
  const messageApi = useMessage()

  const {
    data: kpiData,
    error: kpiError,
  } = useGetStrategicInitiativeKpiQuery({ strategicInitiativeId, kpiId })

  const [addKpiMeasurement] =
    useAddStrategicInitiativeKpiMeasurementMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddStrategicInitiativeKpiMeasurementFormValues>({
      onSubmit: async (
        values: AddStrategicInitiativeKpiMeasurementFormValues,
        form,
      ) => {
        try {
          const request = mapToRequestValues(
            values,
            strategicInitiativeId,
            kpiId,
          )
          const response = await addKpiMeasurement(request)
          if (response.error) throw response.error

          messageApi.success('KPI measurement added successfully.')
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
                'An error occurred while adding a measurement to the KPI. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding a measurement to the KPI. Please try again.',
      permission: 'Permissions.StrategicInitiatives.Update',
    })

  useEffect(() => {
    if (kpiError) {
      console.error(kpiError)
      messageApi.error((isApiError(kpiError) ? kpiError.detail : undefined) || 'An error occurred while loading form data.')
    }
  }, [kpiError, messageApi])

  return (
    <Modal
      title="Add KPI Measurement"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-strategic-initiative-kpi-measurement-form"
      >
        <Descriptions size="small" column={1}>
          <DescriptionItem label="KPI">{`${kpiData?.key} - ${kpiData?.name}`}</DescriptionItem>
          <DescriptionItem label="Target Value">
            {kpiData?.targetValue}
          </DescriptionItem>
          {kpiData?.prefix && (
            <DescriptionItem label="Prefix">
              {kpiData.prefix}
            </DescriptionItem>
          )}
          {kpiData?.suffix && (
            <DescriptionItem label="Suffix">
              {kpiData.suffix}
            </DescriptionItem>
          )}
          <DescriptionItem label="Target Direction">
            {kpiData?.targetDirection as unknown as string}
          </DescriptionItem>
        </Descriptions>
        <br />
        <TypedFormItem
          name="measurementDate"
          label="Measurement Date"
          rules={[
            { required: true },
            {
              validator: (_, value) =>
                value && dayjs() >= value
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('The Measurement Date must be in the Past.'),
                    ),
            },
          ]}
        >
          <DatePicker
            showTime
            format="YYYY-MM-DD h:mm A"
            disabledDate={(value) => value && value > dayjs()}
          />
        </TypedFormItem>
        <TypedFormItem
          name="actualValue"
          label="Actual Value"
          rules={[{ required: true, message: 'Actual Value is required' }]}
        >
          <InputNumber style={{ width: 200 }} />
        </TypedFormItem>
        <TypedFormItem name="note" label="Note" rules={[{ max: 1024 }]}>
          <MarkdownEditor maxLength={1024} />
        </TypedFormItem>
      </Form>
    </Modal>
  )
}

export default AddStrategicInitiativeKpiMeasurementForm
