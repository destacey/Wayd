'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  KpiMeasurementDto,
  KpiMeasurementRequest,
  KpiMeasurementWindowDto,
  KpiWindowKind,
} from '@/src/services/api/wayd.schemas'
import {
  KpiTarget,
  useChangeKpiMeasurementMutation,
  useRecordKpiMeasurementMutation,
} from '@/src/store/features/common/kpis-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { parseCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'
import { toInstant } from '@/src/utils/instant'
import { DatePicker, Form, Input, InputNumber, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { useEffect } from 'react'
import { describeWindow } from './kpi-format'

const { Item } = Form

/**
 * Puts an error on the field that holds it. The API names them under the
 * command's measurement (`measurement.periodEnd`), and both ends of a period
 * belong to its range picker.
 */
const timeErrorsOnField = (key: string) => {
  if (/(^|\.)period(Start|End)$/.test(key)) return 'period'
  if (/(^|\.)asOf$/.test(key)) return 'asOf'
  if (/(^|\.)measuredAt$/.test(key)) return 'measuredAt'
  return undefined
}
const { TextArea } = Input
const { RangePicker } = DatePicker

export interface KpiMeasurementFormProps {
  target: KpiTarget
  /** How the KPI's measurements are windowed, which decides how one says when it was taken. */
  measurementWindow: KpiMeasurementWindowDto
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
  period?: [Dayjs, Dayjs]
  asOf?: Dayjs
  measuredAt?: Dayjs
  sampleSize?: number | null
  note?: string
}

/** Whether the KPI's values are taken as of a day, their period worked out by the server. */
const takesAsOf = (measurementWindow: KpiMeasurementWindowDto) =>
  measurementWindow.kind === KpiWindowKind.Cumulative ||
  measurementWindow.kind === KpiWindowKind.Rolling

/**
 * Records the raw value of a KPI, or corrects one, saying when it was taken the
 * way the KPI's window takes it: for a period, as of a day, or at a moment.
 */
const KpiMeasurementForm = ({
  target,
  measurementWindow,
  prefix,
  suffix,
  measurement,
  onFormComplete,
  onFormCancel,
}: KpiMeasurementFormProps) => {
  const messageApi = useMessage()
  const isEdit = measurement !== undefined
  const action = isEdit ? 'correcting' : 'recording'
  const pointInTime = measurementWindow.kind === KpiWindowKind.PointInTime
  const asOf = takesAsOf(measurementWindow)

  const [recordMeasurement] = useRecordKpiMeasurementMutation()
  const [changeMeasurement] = useChangeKpiMeasurementMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<KpiMeasurementFormValues>({
      onSubmit: async (values, form) => {
        const when: Partial<KpiMeasurementRequest> = pointInTime
          ? { measuredAt: values.measuredAt && toInstant(values.measuredAt) }
          : asOf
            ? { asOf: values.asOf && toCalendarDate(values.asOf) }
            : {
                periodStart: values.period && toCalendarDate(values.period[0]),
                periodEnd: values.period && toCalendarDate(values.period[1]),
                periodRecordKind: measurement?.periodRecordKind,
                periodRecordId: measurement?.periodRecordId,
              }
        const request: KpiMeasurementRequest = {
          value: values.value,
          ...when,
          sampleSize: values.sampleSize ?? undefined,
          note: values.note?.trim() || undefined,
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
              toFormErrors(apiError.errors, { fieldFor: timeErrorsOnField }),
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
    if (!measurement) {
      if (pointInTime) form.setFieldsValue({ measuredAt: dayjs() })
      return
    }
    form.setFieldsValue({
      value: measurement.value,
      period: [
        parseCalendarDate(measurement.periodStart),
        parseCalendarDate(measurement.periodEnd),
      ],
      asOf: parseCalendarDate(measurement.periodEnd),
      measuredAt: measurement.measuredAt
        ? dayjs(measurement.measuredAt)
        : undefined,
      sampleSize: measurement.sampleSize,
      note: measurement.note ?? undefined,
    })
  }, [measurement, form, pointInTime])

  const anchor = measurementWindow.anchorDate
    ? parseCalendarDate(measurementWindow.anchorDate)
    : undefined

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
        {pointInTime ? (
          <Item
            label="Measured at"
            name="measuredAt"
            rules={[{ required: true, message: 'The moment is required' }]}
            extra="The moment the value describes, in your time zone. Several readings a day are fine."
          >
            <DatePicker
              showTime={{ format: 'h:mm A', use12Hours: true }}
              format="MMM D, YYYY h:mm A"
              style={{ width: '100%' }}
            />
          </Item>
        ) : asOf ? (
          <Item
            label="As of"
            name="asOf"
            rules={[{ required: true, message: 'The day is required' }]}
            extra={`${describeWindow(measurementWindow)}, ending on this day. One value per day.`}
          >
            <DatePicker
              style={{ width: '100%' }}
              format="MMM D, YYYY"
              disabledDate={(day) => !!anchor && day.isBefore(anchor, 'day')}
            />
          </Item>
        ) : (
          <Item
            label="Period"
            name="period"
            rules={[{ required: true, message: 'The period is required' }]}
            extra="The days the value describes, such as a sprint or a month."
          >
            <RangePicker style={{ width: '100%' }} format="MMM D, YYYY" />
          </Item>
        )}
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
