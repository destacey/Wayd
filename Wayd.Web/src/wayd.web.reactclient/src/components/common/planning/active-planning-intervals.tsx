'use client'

import { Col, Row, Space, Typography } from 'antd'
import PlanningIntervalCard from './planning-interval-card'
import {
  calendarDaysBetween,
  CalendarDate,
  compareCalendarDates,
  todayCalendarDate,
} from '@/src/utils'
import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'
import { IterationState } from '../../types'

const { Title } = Typography

const isWithinTwoWeeks = (date: CalendarDate) =>
  calendarDaysBetween(todayCalendarDate(), date) <= 14

const ActivePlanningIntervals = () => {
  const { data: piData } = useGetPlanningIntervalsQuery()

  const activePlanningIntervals =
    piData
      ?.filter(
        (pi) =>
          (pi.state.id as IterationState) === IterationState.Active ||
          ((pi.state.id as IterationState) === IterationState.Future &&
            isWithinTwoWeeks(pi.start)),
      )
      ?.sort((a, b) => compareCalendarDates(a.start, b.start)) || []

  if (activePlanningIntervals.length === 0) {
    return <div>No active planning intervals found.</div>
  }

  return (
    <>
      <Space vertical>
        <Title level={2} style={{ margin: '0px', fontWeight: '400' }}>
          Planning Intervals
        </Title>
        <Row gutter={[16, 16]}>
          {activePlanningIntervals.map((pi) => (
            <Col key={pi.key}>
              <PlanningIntervalCard planningInterval={pi} />
            </Col>
          ))}
        </Row>
      </Space>
    </>
  )
}

export default ActivePlanningIntervals
