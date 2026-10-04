'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { InstantWindowDto, SprintDetailsDto } from '@/src/services/wayd-api'
import {
  useCompleteSprintMutation,
  useReopenSprintMutation,
  useStartSprintMutation,
} from '@/src/store/features/work-management/sprints-api'
import { isApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Modal, Space } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { useState } from 'react'

export enum SprintLifecycleAction {
  Start = 'Start',
  Complete = 'Complete',
  Reopen = 'Reopen',
}

const pastTense: Record<SprintLifecycleAction, string> = {
  [SprintLifecycleAction.Start]: 'started',
  [SprintLifecycleAction.Complete]: 'completed',
  [SprintLifecycleAction.Reopen]: 'reopened',
}

const presentParticiple: Record<SprintLifecycleAction, string> = {
  [SprintLifecycleAction.Start]: 'starting',
  [SprintLifecycleAction.Complete]: 'completing',
  [SprintLifecycleAction.Reopen]: 'reopening',
}

const explanation: Record<SprintLifecycleAction, string> = {
  [SprintLifecycleAction.Start]:
    'Records when the team started the sprint, in place of its planned start.',
  [SprintLifecycleAction.Complete]:
    'Records when the team completed the sprint, in place of its planned end.',
  [SprintLifecycleAction.Reopen]:
    'The recorded completion is cleared, so the sprint ends on its planned end again.',
}

const MOMENT_FORMAT = 'MMM D, YYYY h:mm A'

/**
 * The window as the picker offers it. The server's earliest moment sits a tick
 * past the bound it excludes, so it rounds up to the next whole minute. The
 * latest is used as given — rounding it up would send a moment the server
 * refuses — and is absent when the window runs up to now.
 */
const pickableWindow = (range: InstantWindowDto) => {
  const earliest = dayjs(range.earliest)
  return {
    earliest: earliest.isSame(earliest.startOf('minute'))
      ? earliest
      : earliest.startOf('minute').add(1, 'minute'),
    latest: range.latest ? dayjs(range.latest) : undefined,
  }
}

export interface ChangeSprintLifecycleFormProps {
  sprint: SprintDetailsDto
  action: SprintLifecycleAction
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Confirms starting, completing or reopening a sprint. Starting and completing
 * record a moment — now by default, or earlier within the window the server
 * reports for the sprint.
 *
 * A team has one open sprint at a time. Starting while another is open
 * completes that one at the same moment, which the API refuses unless asked
 * for explicitly — confirming this dialog is that request.
 */
const ChangeSprintLifecycleForm = ({
  sprint,
  action,
  onFormComplete,
  onFormCancel,
}: ChangeSprintLifecycleFormProps) => {
  const messageApi = useMessage()

  const [startSprint] = useStartSprintMutation()
  const [completeSprint] = useCompleteSprintMutation()
  const [reopenSprint] = useReopenSprintMutation()

  const momentWindow =
    action === SprintLifecycleAction.Start
      ? sprint.startWindow
      : action === SprintLifecycleAction.Complete
        ? sprint.completeWindow
        : undefined
  const pickable = momentWindow ? pickableWindow(momentWindow) : undefined

  // Now, unless the window has already closed — a start can be recorded
  // after the planned end, but only for a moment before it.
  const [moment, setMoment] = useState<Dayjs | null>(
    () => pickable?.latest ?? dayjs(),
  )

  const momentInWindow =
    !pickable ||
    (!!moment &&
      !moment.isBefore(pickable.earliest) &&
      !moment.isAfter(pickable.latest ?? dayjs()))

  const openSprint =
    action === SprintLifecycleAction.Start ? sprint.openSprint : undefined
  const errorMessage = `An unexpected error occurred while ${presentParticiple[action]} the sprint.`

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request = { id: sprint.id, key: sprint.key }
        const at = pickable && moment ? moment.toDate() : undefined
        const response =
          action === SprintLifecycleAction.Start
            ? await startSprint({
                ...request,
                completeOpenSprintId: openSprint?.id,
                startedAt: at,
                openSprint,
              })
            : action === SprintLifecycleAction.Complete
              ? await completeSprint({ ...request, completedAt: at })
              : await reopenSprint(request)

        if (response.error) throw response.error

        messageApi.success(`Successfully ${pastTense[action]} sprint.`)
        return true
      } catch (error) {
        messageApi.error(
          (isApiError(error) ? error.detail : undefined) ?? errorMessage,
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage,
    permission: 'Permissions.Iterations.Update',
  })

  const momentLabel =
    action === SprintLifecycleAction.Start ? 'Started' : 'Completed'

  return (
    <Modal
      title={`Are you sure you want to ${action.toLowerCase()} this sprint?`}
      open={isOpen}
      onOk={handleOk}
      okText={`${action} Sprint`}
      okButtonProps={{ disabled: !momentInWindow }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Space vertical style={{ width: '100%' }}>
        <div>
          {sprint.key} - {sprint.name}
        </div>
        <div>{explanation[action]}</div>
        {pickable && (
          <Form layout="vertical" size="small">
            <Form.Item
              label={momentLabel}
              required
              validateStatus={momentInWindow ? undefined : 'error'}
              help={`Between ${pickable.earliest.format(MOMENT_FORMAT)} and ${pickable.latest ? pickable.latest.format(MOMENT_FORMAT) : 'now'}, in your time zone.`}
            >
              <DatePicker
                showTime={{ format: 'h:mm A' }}
                format={MOMENT_FORMAT}
                value={moment}
                onChange={setMoment}
                minDate={pickable.earliest}
                maxDate={pickable.latest ?? dayjs()}
                allowClear={false}
                style={{ width: '100%' }}
                aria-label={momentLabel}
              />
            </Form.Item>
          </Form>
        )}
        {openSprint && (
          <Alert
            type="warning"
            showIcon
            title={`Starting this sprint also completes "${openSprint.name}" at the same moment.`}
            description="A team has one open sprint at a time."
          />
        )}
      </Space>
    </Modal>
  )
}

export default ChangeSprintLifecycleForm
