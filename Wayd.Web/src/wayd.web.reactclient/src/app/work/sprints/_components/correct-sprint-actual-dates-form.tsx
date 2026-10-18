'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { SprintDetailsDto, SprintListDto } from '@/src/services/wayd-api'
import { useCorrectSprintActualDatesMutation } from '@/src/store/features/work-management/sprints-api'
import { errorsByField, pickerTimeZoneNote, toApiError } from '@/src/utils'
import { disabledTimeAfter } from './past-moment'
import { DatePicker, Flex, Form, Modal, Space, Typography } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { useState } from 'react'

const { Text } = Typography

const MOMENT_FORMAT = 'MMM D, YYYY h:mm A'

const errorMessage =
  'An unexpected error occurred while correcting the actual dates.'

type CorrectedSprint = Pick<
  SprintListDto,
  'id' | 'key' | 'name' | 'started' | 'completed'
>

interface ActualDates {
  started: Dayjs | null
  completed: Dayjs | null
}

const toDayjs = (instant?: Date) => (instant ? dayjs(instant) : null)

const sameMoment = (a: Dayjs | null, b: Dayjs | null) =>
  a === null || b === null ? a === b : a.isSame(b)

const actualDatesOf = (sprint: CorrectedSprint): ActualDates => ({
  started: toDayjs(sprint.started),
  completed: toDayjs(sprint.completed),
})

const isChanged = (sprint: CorrectedSprint, dates: ActualDates) => {
  const original = actualDatesOf(sprint)
  return (
    !sameMoment(original.started, dates.started) ||
    !sameMoment(original.completed, dates.completed)
  )
}

export interface CorrectSprintActualDatesFormProps {
  sprint: SprintDetailsDto
  previousSprint?: SprintListDto
  nextSprint?: SprintListDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Corrects when the team actually started and completed a sprint, after the
 * fact. A cleared value follows the planned date again. The neighbouring
 * sprints can be corrected in the same save: moving this sprint's completion
 * past the next one's start, for example, is accepted only alongside a
 * matching correction of the next sprint. Neighbours are sent only when
 * changed.
 */
const CorrectSprintActualDatesForm = ({
  sprint,
  previousSprint,
  nextSprint,
  onFormComplete,
  onFormCancel,
}: CorrectSprintActualDatesFormProps) => {
  const messageApi = useMessage()
  const [correctActualDates] = useCorrectSprintActualDatesMutation()

  const sprints: CorrectedSprint[] = [
    ...(previousSprint ? [previousSprint] : []),
    sprint,
    ...(nextSprint ? [nextSprint] : []),
  ]

  const [dates, setDates] = useState<Record<string, ActualDates>>(() =>
    Object.fromEntries(sprints.map((s) => [s.id, actualDatesOf(s)])),
  )

  // The server's errors, keyed `${sprintId}.${field}`, until that date changes.
  const [serverErrors, setServerErrors] = useState<Record<string, string>>({})

  const setDate = (id: string, field: keyof ActualDates, value: Dayjs | null) => {
    setDates((current) => ({
      ...current,
      [id]: { ...current[id], [field]: value },
    }))
    setServerErrors(({ [`${id}.${field}`]: _, ...rest }) => rest)
  }

  const completedBeforeStarted = (id: string) => {
    const { started, completed } = dates[id]
    return !!started && !!completed && !completed.isAfter(started)
  }

  // A typed time can still pass the picker's limits.
  const inFuture = (value: Dayjs | null) => !!value && value.isAfter(dayjs())

  const clientError = (id: string, field: keyof ActualDates) =>
    inFuture(dates[id][field])
      ? "Can't be in the future."
      : field === 'completed' && completedBeforeStarted(id)
        ? 'Must be after the start.'
        : undefined

  const fieldError = (id: string, field: keyof ActualDates) =>
    clientError(id, field) ?? serverErrors[`${id}.${field}`]

  const invalid = sprints.some(
    (s) => clientError(s.id, 'started') || clientError(s.id, 'completed'),
  )
  const changed = sprints.filter((s) => isChanged(s, dates[s.id]))

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await correctActualDates({
          sprints: changed.map((s) => ({
            id: s.id,
            key: s.key,
            started: dates[s.id].started?.toDate(),
            completed: dates[s.id].completed?.toDate(),
          })),
        })

        if (response.error) throw response.error

        messageApi.success('Successfully corrected the actual dates.')
        return true
      } catch (error) {
        const apiError = toApiError(error)
        // `sprints[i]` is the i-th sprint sent, which is the i-th changed one.
        const errors = apiError.errors
          ? errorsByField(apiError.errors, (key) => {
              const match = /^sprints\[(\d+)\]\.(started|completed)$/.exec(
                key,
              )
              const sent = match ? changed[Number(match[1])] : undefined
              return sent ? `${sent.id}.${match![2]}` : undefined
            })
          : {}
        if (apiError.status === 422 && Object.keys(errors).length > 0) {
          setServerErrors(
            Object.fromEntries(
              Object.entries(errors).map(([name, e]) => [name, e.join(' ')]),
            ),
          )
          messageApi.error('Correct the validation error(s) to continue.')
        } else {
          messageApi.error(apiError.detail ?? errorMessage)
        }
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage,
    permission: 'Permissions.Iterations.Update',
  })

  const renderSprint = (s: CorrectedSprint, role?: string) => {
    const label = (field: string) =>
      role ? `${s.name} ${field.toLowerCase()}` : field
    return (
      <div key={s.id}>
        <Text strong={!role}>
          {s.key} - {s.name}
        </Text>
        {role && <Text type="secondary"> ({role})</Text>}
        <Flex gap="small" wrap>
          {(['started', 'completed'] as const).map((field) => {
            const fieldLabel = field === 'started' ? 'Started' : 'Completed'
            const error = fieldError(s.id, field)
            const now = dayjs()
            return (
              <Form.Item
                key={field}
                label={fieldLabel}
                validateStatus={error ? 'error' : undefined}
                help={error}
                style={{ flex: 1, minWidth: 220 }}
              >
                <DatePicker
                  showTime={{ format: 'h:mm A' }}
                  format={MOMENT_FORMAT}
                  value={dates[s.id][field]}
                  onChange={(value) => setDate(s.id, field, value)}
                  maxDate={now}
                  disabledTime={disabledTimeAfter(now)}
                  placeholder="Not recorded"
                  style={{ width: '100%' }}
                  aria-label={label(fieldLabel)}
                />
              </Form.Item>
            )
          })}
        </Flex>
      </div>
    )
  }

  return (
    <Modal
      title="Correct Actual Dates"
      open={isOpen}
      onOk={handleOk}
      okText="Save"
      okButtonProps={{ disabled: invalid || changed.length === 0 }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={640}
    >
      <Space vertical style={{ width: '100%' }}>
        <div>
          Corrects when the team actually started and completed the sprint, in{' '}
          {pickerTimeZoneNote(sprint.timeZone)}. Clear a value to have the
          sprint follow its planned date. Correct a neighbouring sprint as well
          when moving one past the other.
        </div>
        <Form layout="vertical" size="small">
          {previousSprint && renderSprint(previousSprint, 'previous sprint')}
          {renderSprint(sprint)}
          {nextSprint && renderSprint(nextSprint, 'next sprint')}
        </Form>
      </Space>
    </Modal>
  )
}

export default CorrectSprintActualDatesForm
