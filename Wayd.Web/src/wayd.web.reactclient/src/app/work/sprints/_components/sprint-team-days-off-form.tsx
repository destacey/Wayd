'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import { useSetSprintTeamDaysOffMutation } from '@/src/store/features/work-management/sprints-api'
import {
  calendarDaysBetween,
  formatCalendarDate,
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  type ApiError,
} from '@/src/utils'
import { Checkbox, Flex, Modal, Typography } from 'antd'
import { IsoDayOfWeek } from '@/src/services/wayd-api'

/** dayjs's day of the week, Sunday 0 to Saturday 6, for each day of the week. */
const DAYJS_WEEKDAY: Record<IsoDayOfWeek, number> = {
  [IsoDayOfWeek.None]: -1,
  [IsoDayOfWeek.Sunday]: 0,
  [IsoDayOfWeek.Monday]: 1,
  [IsoDayOfWeek.Tuesday]: 2,
  [IsoDayOfWeek.Wednesday]: 3,
  [IsoDayOfWeek.Thursday]: 4,
  [IsoDayOfWeek.Friday]: 5,
  [IsoDayOfWeek.Saturday]: 6,
}
import { useState } from 'react'

const { Text } = Typography

export interface SprintTeamDaysOffFormProps {
  sprint: SprintDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/** Every planned day of the sprint, first to last. */
export const sprintPlannedDays = (sprint: SprintDetailsDto): string[] => {
  const start = parseCalendarDate(sprint.start)
  const count = calendarDaysBetween(sprint.start, sprint.end) + 1
  return Array.from({ length: Math.max(count, 0) }, (_, i) =>
    toCalendarDate(start.add(i, 'day')),
  )
}

/** Why a planned day cannot be a team day off, or undefined when it can. */
export const alreadyOff = (
  sprint: SprintDetailsDto,
  day: string,
): string | undefined => {
  if (sprint.holidays.includes(day)) return 'Holiday'
  const weekday = parseCalendarDate(day).day()
  if (
    sprint.workingDays.length > 0 &&
    !sprint.workingDays.some((d) => DAYJS_WEEKDAY[d] === weekday)
  )
    return 'Not a working day'
  return undefined
}

/**
 * Picks the sprint's planned days the whole team is off — an offsite, a
 * hackathon — beyond its working week and holiday calendar.
 */
const SprintTeamDaysOffForm = ({
  sprint,
  onFormComplete,
  onFormCancel,
}: SprintTeamDaysOffFormProps) => {
  const messageApi = useMessage()
  const [setTeamDaysOff, { isLoading }] = useSetSprintTeamDaysOffMutation()
  const [selected, setSelected] = useState<string[]>(sprint.teamDaysOff)

  const days = sprintPlannedDays(sprint)

  const handleOk = async () => {
    try {
      const response = await setTeamDaysOff({
        id: sprint.id,
        key: sprint.key,
        teamDaysOff: days.filter(
          (d) => selected.includes(d) && !alreadyOff(sprint, d),
        ),
      })
      if (response.error) throw response.error
      messageApi.success('Team days off saved.')
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ?? 'An error occurred while saving the team days off.',
      )
    }
  }

  return (
    <Modal
      title="Team Days Off"
      open={true}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isLoading}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text type="secondary">
          Days the whole team is off during this sprint, such as an offsite.
          Holidays from its calendar and days outside its working week are
          already off, so they are shown but cannot be chosen. On a team day off
          nobody counts as working: the ideal burn-down stays flat and the
          sprint&apos;s capacity leaves it out.
        </Text>
        <Checkbox.Group
          value={selected}
          onChange={(values) => setSelected(values as string[])}
        >
          <Flex vertical gap={4}>
            {days.map((day) => {
              const reason = alreadyOff(sprint, day)
              return (
                <Checkbox key={day} value={day} disabled={!!reason}>
                  {formatCalendarDate(day, 'ddd, MMM D, YYYY')}
                  {reason && <Text type="secondary">{` · ${reason}`}</Text>}
                </Checkbox>
              )
            })}
          </Flex>
        </Checkbox.Group>
      </Flex>
    </Modal>
  )
}

export default SprintTeamDaysOffForm
