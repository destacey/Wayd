'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  KpiAtRiskKind,
  KpiExpressedAs,
  KpiSummaryDto,
  KpiTargetDirection,
  KpiTargetKind,
} from '@/src/services/api/wayd.schemas'
import {
  KpiSubject,
  useAddKpiMutation,
  useGetKpiCategoriesQuery,
  useUpdateKpiMutation,
} from '@/src/store/features/common/kpis-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { parseCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'
import {
  DatePicker,
  Divider,
  Flex,
  Form,
  Input,
  InputNumber,
  Modal,
  Radio,
  Select,
} from 'antd'
import { Dayjs } from 'dayjs'
import { useEffect } from 'react'

const { Item } = Form

/**
 * Puts an error on either end of the baseline window on its range picker. The
 * API names them under the baseline (`baseline.windowStart`).
 */
const windowErrorsOnPicker = (key: string) =>
  /(^|\.)window(Start|End)$/.test(key) ? 'baselineWindow' : undefined
const { TextArea } = Input
const { RangePicker } = DatePicker

export interface KpiFormProps {
  subject: KpiSubject
  /** The KPI to edit, or none to add one at the end of the scorecard. */
  kpi?: KpiSummaryDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface KpiFormValues {
  name: string
  description?: string
  categoryId?: string
  prefix?: string
  suffix?: string
  expressedAs: KpiExpressedAs
  direction: KpiTargetDirection
  baselineValue?: number | null
  baselineWindow?: [Dayjs, Dayjs] | null
  targetKind: KpiTargetKind
  targetValue: number
  atRiskKind?: KpiAtRiskKind | 'none'
  atRiskValue?: number | null
}

const expressedAsOptions = [
  { value: KpiExpressedAs.Value, label: 'Value' },
  { value: KpiExpressedAs.ChangeFromBaseline, label: 'Change from baseline' },
  { value: KpiExpressedAs.ProgressToTarget, label: 'Progress to target' },
]

const atRiskOptions = [
  { value: 'none', label: 'None' },
  { value: KpiAtRiskKind.Absolute, label: 'A value' },
  {
    value: KpiAtRiskKind.ProgressToTarget,
    label: 'A share of the way to target',
  },
]

const blankToUndefined = (value?: string) => value?.trim() || undefined

/**
 * Adds a KPI to a record's scorecard, or changes one. An edit saves the details
 * and the goal together, so it succeeds or fails as a whole; the server records
 * only the parts that changed in the KPI's history.
 */
const KpiForm = ({
  subject,
  kpi,
  onFormComplete,
  onFormCancel,
}: KpiFormProps) => {
  const messageApi = useMessage()
  const isEdit = kpi !== undefined
  const action = isEdit ? 'updating' : 'adding'

  const { data: categories = [] } = useGetKpiCategoriesQuery({
    includeInactive: isEdit,
  })
  const [addKpi] = useAddKpiMutation()
  const [updateKpi] = useUpdateKpiMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<KpiFormValues>({
      onSubmit: async (values, form) => {
        const details = {
          name: values.name.trim(),
          description: blankToUndefined(values.description),
          categoryId: values.categoryId || undefined,
          prefix: blankToUndefined(values.prefix),
          suffix: blankToUndefined(values.suffix),
          expressedAs: values.expressedAs,
        }
        const hasAtRisk = values.atRiskKind && values.atRiskKind !== 'none'
        const target = {
          direction: values.direction,
          kind: values.targetKind,
          value: values.targetValue,
          atRiskKind: hasAtRisk
            ? (values.atRiskKind as KpiAtRiskKind)
            : undefined,
          atRiskValue: hasAtRisk
            ? (values.atRiskValue ?? undefined)
            : undefined,
        }
        const baseline = {
          value: values.baselineValue ?? undefined,
          windowStart: values.baselineWindow
            ? toCalendarDate(values.baselineWindow[0])
            : undefined,
          windowEnd: values.baselineWindow
            ? toCalendarDate(values.baselineWindow[1])
            : undefined,
        }

        try {
          const request = { ...details, baseline, target }
          if (kpi) {
            await updateKpi({ ...subject, kpiId: kpi.id, request }).unwrap()
            messageApi.success('KPI updated.')
          } else {
            await addKpi({ ...subject, request }).unwrap()
            messageApi.success('KPI added.')
          }
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(
              toFormErrors(apiError.errors, {
                fieldFor: windowErrorsOnPicker,
              }),
            )
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                `An error occurred while ${action} the KPI. Please try again.`,
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: `An error occurred while ${action} the KPI. Please try again.`,
    })

  useEffect(() => {
    if (!kpi) {
      form.setFieldsValue({
        expressedAs: KpiExpressedAs.Value,
        direction: KpiTargetDirection.Increase,
        targetKind: KpiTargetKind.Absolute,
        atRiskKind: 'none',
      })
      return
    }
    const goal = kpi.goal
    form.setFieldsValue({
      name: kpi.name,
      description: kpi.description ?? undefined,
      categoryId: kpi.category?.id,
      prefix: kpi.prefix ?? undefined,
      suffix: kpi.suffix ?? undefined,
      expressedAs: kpi.expressedAs,
      direction: goal.direction,
      baselineValue: goal.baselineValue,
      baselineWindow:
        goal.baselineWindowStart && goal.baselineWindowEnd
          ? [
              parseCalendarDate(goal.baselineWindowStart),
              parseCalendarDate(goal.baselineWindowEnd),
            ]
          : null,
      targetKind: goal.targetKind,
      targetValue: goal.targetValue,
      atRiskKind: goal.atRiskKind ?? 'none',
      atRiskValue: goal.atRiskValue,
    })
  }, [kpi, form])

  const targetKind = Form.useWatch('targetKind', form)
  const atRiskKind = Form.useWatch('atRiskKind', form)

  const categoryOptions = categories
    .filter((c) => c.isActive || c.id === kpi?.category?.id)
    .map((c) => ({ value: c.id, label: c.name }))

  return (
    <Modal
      title={isEdit ? 'Edit KPI' : 'Add KPI'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Add'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={640}
    >
      <Form form={form} size="small" layout="vertical" name="kpi-form">
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item label="Description" name="description" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 2, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
        <Flex gap={12} wrap>
          <Item
            label="Category"
            name="categoryId"
            style={{ flex: '1 1 200px' }}
          >
            <Select
              allowClear
              options={categoryOptions}
              placeholder="Uncategorized"
            />
          </Item>
          <Item
            label="Prefix"
            name="prefix"
            rules={[{ max: 16 }]}
            style={{ width: 90 }}
          >
            <Input placeholder="$" maxLength={16} />
          </Item>
          <Item
            label="Suffix"
            name="suffix"
            rules={[{ max: 16 }]}
            style={{ width: 120 }}
          >
            <Input placeholder="days" maxLength={16} />
          </Item>
        </Flex>
        <Item
          label="Show as"
          name="expressedAs"
          extra="Which reading the scorecard leads with. Measurements are always the raw value."
        >
          <Select options={expressedAsOptions} />
        </Item>

        <Divider titlePlacement="start" plain>
          Goal
        </Divider>
        <Item label="Direction" name="direction" rules={[{ required: true }]}>
          <Radio.Group
            options={[
              { value: KpiTargetDirection.Increase, label: 'Higher is better' },
              { value: KpiTargetDirection.Decrease, label: 'Lower is better' },
            ]}
          />
        </Item>
        <Flex gap={12} wrap>
          <Item
            label="Baseline"
            name="baselineValue"
            style={{ flex: '1 1 140px' }}
            extra="Where it started. Needed for a relative target."
          >
            <InputNumber style={{ width: '100%' }} />
          </Item>
          <Item
            label="Baseline taken over"
            name="baselineWindow"
            style={{ flex: '2 1 240px' }}
          >
            <RangePicker style={{ width: '100%' }} format="MMM D, YYYY" />
          </Item>
        </Flex>
        <Flex gap={12} wrap align="flex-start">
          <Item label="Target" name="targetKind" style={{ flex: '2 1 240px' }}>
            <Radio.Group
              options={[
                { value: KpiTargetKind.Absolute, label: 'A value' },
                { value: KpiTargetKind.RelativeChange, label: 'A % change' },
              ]}
            />
          </Item>
          <Item
            label={
              targetKind === KpiTargetKind.RelativeChange
                ? 'Change (%)'
                : 'Target value'
            }
            name="targetValue"
            rules={[{ required: true, message: 'Target is required' }]}
            style={{ flex: '1 1 140px' }}
          >
            <InputNumber style={{ width: '100%' }} />
          </Item>
        </Flex>
        <Flex gap={12} wrap align="flex-start">
          <Item
            label="At risk"
            name="atRiskKind"
            style={{ flex: '2 1 240px' }}
            extra="Short of the target but past this line reads At Risk rather than Unhealthy."
          >
            <Select options={atRiskOptions} />
          </Item>
          {atRiskKind && atRiskKind !== 'none' && (
            <Item
              label={
                atRiskKind === KpiAtRiskKind.ProgressToTarget
                  ? 'Share of the way (%)'
                  : 'At-risk value'
              }
              name="atRiskValue"
              rules={[{ required: true, message: 'A value is required' }]}
              style={{ flex: '1 1 140px' }}
            >
              <InputNumber style={{ width: '100%' }} />
            </Item>
          )}
        </Flex>
      </Form>
    </Modal>
  )
}

export default KpiForm
