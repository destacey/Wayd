'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  KpiCheckpointDto,
  KpiCheckpointPlanRequest,
  KpiTargetDirection,
} from '@/src/services/api/wayd.schemas'
import {
  KpiTarget,
  useChangeKpiCheckpointPlanMutation,
} from '@/src/store/features/common/kpis-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { parseCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import {
  Button,
  DatePicker,
  Flex,
  Form,
  Grid,
  Input,
  InputNumber,
  Modal,
  Typography,
} from 'antd'
import { Dayjs } from 'dayjs'
import { useEffect, useState } from 'react'

const { Item, List } = Form
const { Text } = Typography

/**
 * Each column's width, set on the field rather than its control so an error
 * message wraps beneath it instead of widening the row and wrapping the row's
 * remove button onto a line of its own.
 */
const TEXT_WIDTH = 150
const VALUE_WIDTH = 140

/** The longest label a checkpoint holds, as the API allows. */
export const CHECKPOINT_LABEL_MAX_LENGTH = 32

export interface KpiCheckpointPlanFormProps {
  target: KpiTarget
  /** The plan as it stands, in date order. */
  checkpoints: KpiCheckpointDto[]
  /** Which way the KPI should go, which decides the side an at-risk value sits on. */
  direction: KpiTargetDirection
  /** Shown before and after the value fields, so the unit is clear. */
  prefix?: string
  suffix?: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CheckpointRowValues {
  date?: Dayjs
  label?: string
  plannedValue?: number
  atRiskValue?: number | null
}

interface KpiCheckpointPlanFormValues {
  checkpoints: CheckpointRowValues[]
}

/**
 * Why an at-risk value is not short of its planned value in the KPI's
 * direction, or undefined when it is.
 */
export const atRiskProblem = (
  direction: KpiTargetDirection,
  plannedValue: number | undefined,
  atRiskValue: number | null | undefined,
) => {
  if (atRiskValue == null || plannedValue == null) return undefined
  if (direction === KpiTargetDirection.Increase && atRiskValue >= plannedValue)
    return 'Must be below the planned value'
  if (direction === KpiTargetDirection.Decrease && atRiskValue <= plannedValue)
    return 'Must be above the planned value'
  return undefined
}

/**
 * Puts an error on the row that holds it. The API names a row's fields
 * `checkpoints[0].plannedValue`; the form names them by list path.
 */
const rowErrorsOnField = (key: string) => {
  const match = /^checkpoints\[(\d+)\]\.(\w+)$/i.exec(key)
  if (!match) return undefined
  const field = match[2].charAt(0).toLowerCase() + match[2].slice(1)
  return ['checkpoints', Number(match[1]), field]
}

/**
 * Replaces a KPI's checkpoint plan as a whole: each row a date, a label, the
 * value planned by then and an optional at-risk value. Rows left out are
 * removed; an empty plan clears it.
 */
const KpiCheckpointPlanForm = ({
  target,
  checkpoints,
  direction,
  prefix,
  suffix,
  onFormComplete,
  onFormCancel,
}: KpiCheckpointPlanFormProps) => {
  const messageApi = useMessage()
  const [changePlan] = useChangeKpiCheckpointPlanMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<KpiCheckpointPlanFormValues>({
      onSubmit: async (values, form) => {
        const request: KpiCheckpointPlanRequest = {
          checkpoints: (values.checkpoints ?? []).map((c) => ({
            date: toCalendarDate(c.date!),
            label: c.label!.trim(),
            plannedValue: c.plannedValue!,
            atRiskValue: c.atRiskValue ?? undefined,
          })),
        }
        try {
          await changePlan({ ...target, request }).unwrap()
          messageApi.success('Checkpoint plan saved.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(
              toFormErrors(apiError.errors, { fieldFor: rowErrorsOnField }),
            )
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while saving the checkpoint plan. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while saving the checkpoint plan. Please try again.',
    })

  // Read once, when the modal opens: a refetch of the KPI while it is open
  // must not replace what the user is editing.
  const [initialValues] = useState<KpiCheckpointPlanFormValues>(() => ({
    checkpoints: checkpoints.map((c) => ({
      date: parseCalendarDate(c.date),
      label: c.label,
      plannedValue: c.plannedValue,
      atRiskValue: c.atRiskValue ?? undefined,
    })),
  }))

  // Labels sit over the first row only, unless a narrow screen wraps each
  // row's fields, when every row needs its own.
  const screens = Grid.useBreakpoint()
  const labelEveryRow = !screens.md

  // A removed row can leave a "One per day" error on the row it clashed with,
  // which only re-validating the remaining dates clears. It runs as an effect
  // so the rows have re-rendered under their new indexes, and nothing is left
  // pending once the modal closes.
  const [removals, setRemovals] = useState(0)
  useEffect(() => {
    if (removals === 0) return
    const rows: CheckpointRowValues[] = form.getFieldValue('checkpoints') ?? []
    const dated = rows.flatMap((row, index) =>
      row?.date ? [['checkpoints', index, 'date']] : [],
    )
    form.validateFields(dated).catch(() => undefined)
  }, [removals, form])

  const atRiskHint =
    direction === KpiTargetDirection.Increase
      ? 'below the planned value'
      : 'above the planned value'

  return (
    <Modal
      title="Checkpoint Plan"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={720}
    >
      <Text type="secondary">
        Each measurement is judged against the first checkpoint on or after the
        last day it covers; past the last checkpoint, against the target. An
        at-risk value sits {atRiskHint}; without one, a measurement is on plan
        or not.
      </Text>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="kpi-checkpoint-plan-form"
        initialValues={initialValues}
        style={{ marginTop: 16 }}
      >
        <List name="checkpoints">
          {(fields, { add, remove }) => (
            <Flex vertical gap={8}>
              {fields.map(({ key, name, ...restField }) => {
                const labelled = name === 0 || labelEveryRow
                const row = `Checkpoint ${name + 1}`
                return (
                  <Flex key={key} gap={8} align="start" wrap>
                    <Item
                      {...restField}
                      name={[name, 'date']}
                      label={labelled ? 'Date' : undefined}
                      style={{ margin: 0, width: TEXT_WIDTH }}
                      dependencies={fields
                        .filter((f) => f.name !== name)
                        .map((f) => ['checkpoints', f.name, 'date'])}
                      rules={[
                        { required: true, message: 'Required' },
                        {
                          validator: (_, value?: Dayjs) => {
                            const rows: CheckpointRowValues[] =
                              form.getFieldValue('checkpoints') ?? []
                            const taken = rows.some(
                              (row, index) =>
                                index !== name &&
                                !!value &&
                                !!row?.date &&
                                row.date.isSame(value, 'day'),
                            )
                            return taken
                              ? Promise.reject(new Error('One per day'))
                              : Promise.resolve()
                          },
                        },
                      ]}
                    >
                      <DatePicker
                        format="MMM D, YYYY"
                        style={{ width: '100%' }}
                        aria-label={`${row} date`}
                      />
                    </Item>
                    <Item
                      {...restField}
                      name={[name, 'label']}
                      label={labelled ? 'Label' : undefined}
                      style={{ margin: 0, width: TEXT_WIDTH }}
                      rules={[
                        {
                          required: true,
                          whitespace: true,
                          message: 'Required',
                        },
                      ]}
                    >
                      <Input
                        placeholder="Q1"
                        maxLength={CHECKPOINT_LABEL_MAX_LENGTH}
                        style={{ width: '100%' }}
                        aria-label={`${row} label`}
                      />
                    </Item>
                    <Item
                      {...restField}
                      name={[name, 'plannedValue']}
                      label={labelled ? 'Planned' : undefined}
                      style={{ margin: 0, width: VALUE_WIDTH }}
                      rules={[{ required: true, message: 'Required' }]}
                    >
                      <InputNumber
                        prefix={prefix}
                        suffix={suffix}
                        style={{ width: '100%' }}
                        aria-label={`${row} planned value`}
                      />
                    </Item>
                    <Item
                      {...restField}
                      name={[name, 'atRiskValue']}
                      label={labelled ? 'At risk (optional)' : undefined}
                      style={{ margin: 0, width: VALUE_WIDTH }}
                      dependencies={[['checkpoints', name, 'plannedValue']]}
                      rules={[
                        {
                          validator: (_, value?: number | null) => {
                            const planned = form.getFieldValue([
                              'checkpoints',
                              name,
                              'plannedValue',
                            ]) as number | undefined
                            const problem = atRiskProblem(
                              direction,
                              planned,
                              value,
                            )
                            return problem
                              ? Promise.reject(new Error(problem))
                              : Promise.resolve()
                          },
                        },
                      ]}
                    >
                      <InputNumber
                        prefix={prefix}
                        suffix={suffix}
                        style={{ width: '100%' }}
                        aria-label={`${row} at-risk value`}
                      />
                    </Item>
                    <Button
                      type="text"
                      icon={<MinusCircleOutlined />}
                      onClick={() => {
                        remove(name)
                        setRemovals((count) => count + 1)
                      }}
                      aria-label={`Remove ${row.toLowerCase()}`}
                      // Clears the field labels above the row's inputs.
                      style={{ marginTop: labelled ? 32 : 0 }}
                    />
                  </Flex>
                )
              })}
              <Button
                type="dashed"
                onClick={() => add()}
                block
                icon={<PlusOutlined />}
              >
                Add Checkpoint
              </Button>
            </Flex>
          )}
        </List>
      </Form>
    </Modal>
  )
}

export default KpiCheckpointPlanForm
