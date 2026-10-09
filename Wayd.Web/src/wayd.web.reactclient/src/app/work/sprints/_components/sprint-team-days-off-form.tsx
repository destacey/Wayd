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
        teamDaysOff: days.filter((d) => selected.includes(d)),
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
          Weekends outside the team&apos;s working days and holidays from its
          calendar are already off and need not be chosen. The ideal burn-down
          stays flat on these days.
        </Text>
        <Checkbox.Group
          value={selected}
          onChange={(values) => setSelected(values as string[])}
        >
          <Flex vertical gap={4}>
            {days.map((day) => (
              <Checkbox key={day} value={day}>
                {formatCalendarDate(day, 'ddd, MMM D, YYYY')}
              </Checkbox>
            ))}
          </Flex>
        </Checkbox.Group>
      </Flex>
    </Modal>
  )
}

export default SprintTeamDaysOffForm
