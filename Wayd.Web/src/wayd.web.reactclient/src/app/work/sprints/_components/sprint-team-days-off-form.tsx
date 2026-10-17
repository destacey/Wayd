'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { SprintDetailsDto, IsoDayOfWeek } from '@/src/services/wayd-api'
import { useSetSprintTeamDaysOffMutation } from '@/src/store/features/work-management/sprints-api'
import {
  calendarDaysBetween,
  formatCalendarDate,
  FULL_DAY_OFF,
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  type ApiError,
} from '@/src/utils'
import {
  DEFAULT_WORKING_DAY_HOURS,
  durationOf,
  percentAwayFor,
  TimeAwayDuration,
} from '@/src/app/organizations/_components/time-away/time-away-duration'
import { Checkbox, Flex, InputNumber, Modal, Segmented, Typography } from 'antd'
import { useState } from 'react'

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

const { Text } = Typography

export interface SprintTeamDaysOffFormProps {
  sprint: SprintDetailsDto
  /**
   * Whether a day can be off for only part of it, which counts in capacity;
   * otherwise every day chosen is a whole day off.
   */
  allowPartDays?: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/** How much of a chosen day the team is off, as entered and as saved. */
interface ChosenDay {
  duration: TimeAwayDuration
  /** The hours off, when the duration is in hours. */
  hours?: number
  /** What is saved; undefined while hours are chosen but none entered. */
  percentOff: number | undefined
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
 * hackathon — beyond its working week and holiday calendar, each for all of
 * the day or, when part days are allowed, half of it or a number of hours.
 */
const SprintTeamDaysOffForm = ({
  sprint,
  allowPartDays = false,
  onFormComplete,
  onFormCancel,
}: SprintTeamDaysOffFormProps) => {
  const messageApi = useMessage()
  const [setTeamDaysOff, { isLoading }] = useSetSprintTeamDaysOffMutation()
  const workingDayHours = sprint.workingDayHours || DEFAULT_WORKING_DAY_HOURS
  const [chosen, setChosen] = useState<Record<string, ChosenDay>>(() =>
    Object.fromEntries(
      sprint.teamDaysOff.map((d) => [
        d.date,
        {
          ...durationOf(d.percentOff, workingDayHours),
          percentOff: d.percentOff,
        },
      ]),
    ),
  )

  const days = sprintPlannedDays(sprint)
  // A day read back keeps its stored percent whatever its hours round to, so
  // only hours the user has cleared hold the form back.
  const missingHours = Object.values(chosen).some(
    (d) => d.percentOff === undefined,
  )

  const choose = (values: string[]) =>
    setChosen((current) =>
      Object.fromEntries(
        values.map((day) => [
          day,
          current[day] ?? { duration: 'full', percentOff: FULL_DAY_OFF },
        ]),
      ),
    )

  const changeDay = (
    day: string,
    duration: TimeAwayDuration,
    hours: number | undefined,
  ) =>
    setChosen((current) => ({
      ...current,
      [day]: {
        duration,
        hours,
        percentOff:
          duration === 'hours' && !hours
            ? undefined
            : percentAwayFor(duration, hours, workingDayHours),
      },
    }))

  const handleOk = async () => {
    if (missingHours) return
    try {
      const response = await setTeamDaysOff({
        id: sprint.id,
        key: sprint.key,
        teamDaysOff: days.flatMap((d) => {
          const percentOff = chosen[d]?.percentOff
          return percentOff !== undefined && !alreadyOff(sprint, d)
            ? [{ date: d, percentOff }]
            : []
        }),
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
      okButtonProps={{ disabled: missingHours }}
      confirmLoading={isLoading}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text type="secondary">
          Days the whole team is off during this sprint, such as an offsite.
          Holidays from its calendar and days outside its working week are
          already off, so they are shown but cannot be chosen.{' '}
          {allowPartDays
            ? `A day off takes its share of everyone's capacity, of the ${workingDayHours}-hour working day, and the ideal burn-down drops that much less: flat on a full day, by half a day on a half day.`
            : 'On a team day off nobody counts as working: the ideal burn-down stays flat and the sprint’s capacity leaves it out.'}
        </Text>
        <Checkbox.Group
          value={Object.keys(chosen)}
          onChange={(values) => choose(values as string[])}
        >
          <Flex vertical gap={4}>
            {days.map((day) => {
              const reason = alreadyOff(sprint, day)
              const label = formatCalendarDate(day, 'ddd, MMM D, YYYY')
              const dayOff = !reason ? chosen[day] : undefined
              return (
                <Flex key={day} justify="space-between" align="center" gap={8}>
                  <Checkbox value={day} disabled={!!reason}>
                    {label}
                    {reason && <Text type="secondary">{` · ${reason}`}</Text>}
                  </Checkbox>
                  {allowPartDays && dayOff && (
                    <Flex gap={4} align="center">
                      <Segmented<TimeAwayDuration>
                        size="small"
                        aria-label={`How much of ${label} the team is off`}
                        value={dayOff.duration}
                        onChange={(duration) =>
                          changeDay(day, duration, dayOff.hours)
                        }
                        options={[
                          { label: 'Full day', value: 'full' },
                          { label: 'Half day', value: 'half' },
                          { label: 'Hours', value: 'hours' },
                        ]}
                      />
                      {dayOff.duration === 'hours' && (
                        <InputNumber
                          size="small"
                          min={0.25}
                          max={workingDayHours}
                          step={0.25}
                          precision={2}
                          suffix="h"
                          aria-label={`Hours the team is off on ${label}`}
                          value={dayOff.hours}
                          onChange={(hours) =>
                            changeDay(day, 'hours', hours ?? undefined)
                          }
                        />
                      )}
                    </Flex>
                  )}
                </Flex>
              )
            })}
          </Flex>
        </Checkbox.Group>
      </Flex>
    </Modal>
  )
}

export default SprintTeamDaysOffForm
