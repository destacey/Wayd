'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { KpiMeasurementDto } from '@/src/services/api/wayd.schemas'
import {
  KpiTarget,
  useChangeKpiMeasurementMutation,
  useRecordKpiMeasurementMutation,
} from '@/src/store/features/common/kpis-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { parseCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'
import { DatePicker, Form, Input, InputNumber, Modal } from 'antd'
import { Dayjs } from 'dayjs'
import { useEffect } from 'react'

const { Item } = Form

/**
 * Puts an error on either end of the period on the range picker. The API
 * names them under the command's measurement (`measurement.periodEnd`).
 */
const periodErrorsOnPicker = (key: string) =>
  /(^|\.)period(Start|End)$/.test(key) ? 'period' : undefined
const { TextArea } = Input
const { RangePicker } = DatePicker

export interface KpiMeasurementFormProps {
  target: KpiTarget
  /** Shown before and after the value field, so the unit is clear. */
  prefix?: string
  suffix?: string
  /** The measurement to correct, or none to record a new one. */
  measurement?: KpiMeasurementDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface KpiMeasurementFormValues {
  value: number
  period: [Dayjs, Dayjs]
  sampleSize?: number | null
  note?: string
}

/**
 * Records the raw value of a KPI for a period, or corrects one. Periods on a
 * KPI never overlap, so each period is recorded once.
 */
const KpiMeasurementForm = ({
  target,
  prefix,
  suffix,
  measurement,
  onFormComplete,
  onFormCancel,
}: KpiMeasurementFormProps) => {
  const messageApi = useMessage()
  const isEdit = measurement !== undefined
  const action = isEdit ? 'correcting' : 'recording'

  const [recordMeasurement] = useRecordKpiMeasurementMutation()
  const [changeMeasurement] = useChangeKpiMeasurementMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<KpiMeasurementFormValues>({
      onSubmit: async (values, form) => {
        const request = {
          value: values.value,
          periodStart: toCalendarDate(values.period[0]),
          periodEnd: toCalendarDate(values.period[1]),
          sampleSize: values.sampleSize ?? undefined,
          note: values.note?.trim() || undefined,
          periodRecordKind: measurement?.periodRecordKind,
          periodRecordId: measurement?.periodRecordId,
          measuredOn: measurement?.measuredOn,
        }
        try {
          if (measurement) {
            await changeMeasurement({
              ...target,
              measurementId: measurement.id,
              request,
            }).unwrap()
            messageApi.success('Measurement corrected.')
          } else {
            await recordMeasurement({ ...target, request }).unwrap()
            messageApi.success('Measurement recorded.')
          }
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(
              toFormErrors(apiError.errors, {
                fieldFor: periodErrorsOnPicker,
              }),
            )
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                `An error occurred while ${action} the measurement. Please try again.`,
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: `An error occurred while ${action} the measurement. Please try again.`,
    })

  useEffect(() => {
    if (!measurement) return
    form.setFieldsValue({
      value: measurement.value,
      period: [
        parseCalendarDate(measurement.periodStart),
        parseCalendarDate(measurement.periodEnd),
      ],
      sampleSize: measurement.sampleSize,
      note: measurement.note ?? undefined,
    })
  }, [measurement, form])

  return (
    <Modal
      title={isEdit ? 'Correct Measurement' : 'Record Measurement'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Record'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="kpi-measurement-form"
      >
        <Item
          label="Period"
          name="period"
          rules={[{ required: true, message: 'The period is required' }]}
          extra="The days the value describes, such as a sprint or a month."
        >
          <RangePicker style={{ width: '100%' }} format="MMM D, YYYY" />
        </Item>
        <Item
          label="Value"
          name="value"
          rules={[{ required: true, message: 'A value is required' }]}
          extra="The raw value — not a percentage worked out from it."
        >
          <InputNumber
            style={{ width: '100%' }}
            prefix={prefix}
            suffix={suffix}
          />
        </Item>
        <Item
          label="Sample size"
          name="sampleSize"
          rules={[{ type: 'number', min: 0 }]}
          extra="How many items the value was worked out from, when it is an average or a rate."
        >
          <InputNumber style={{ width: '100%' }} min={0} precision={0} />
        </Item>
        <Item label="Note" name="note" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 2, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default KpiMeasurementForm
