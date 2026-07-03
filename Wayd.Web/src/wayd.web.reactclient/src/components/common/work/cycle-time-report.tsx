'use client'

import { FC, useState } from 'react'
import {
  DatePicker,
  Flex,
  InputNumber,
  Select,
  Space,
  Tooltip,
  Typography,
} from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import utc from 'dayjs/plugin/utc'
import { useGetTeamWorkItemsQuery } from '@/src/store/features/organizations/team-api'
import { useGetEmployeeWorkItemsQuery } from '@/src/store/features/organizations/employee-api'
import { WorkStatusCategory, WorkItemListDto } from '@/src/services/wayd-api'
import { useDebounce } from '@/src/hooks'
import { CycleTimeAnalysisChart, WorkItemsGrid } from '.'
import {
  applyBalancedPercentileFilter,
  CycleTimeOutlierMethod,
  applyForecastingPercentileFilter,
  getCycleTimeWorkItems,
  normalizePercentile,
  sortCycleTimeWorkItems,
} from './cycle-time-report.filtering'
import { InfoCircleOutlined } from '@ant-design/icons'

const { Title, Text } = Typography

dayjs.extend(utc)

export interface CycleTimeReportProps {
  teamCode: string
}

export interface EmployeeCycleTimeReportProps {
  employeeId: string
}

interface CycleTimeReportContentProps {
  source:
    | { type: 'team'; teamCode: string }
    | { type: 'employee'; employeeId: string }
  workItemsScope: string
}

const CycleTimeReportContent: FC<CycleTimeReportContentProps> = ({
  source,
  workItemsScope,
}) => {
  const doneFromPresets = [30, 60, 90, 120, 180].map((days) => ({
    label: `${days} Days`,
    value: dayjs().utc().subtract(days, 'days').startOf('day'),
  }))

  const [selectedDate, setSelectedDate] = useState<Dayjs>(() =>
    dayjs().utc().subtract(90, 'days').startOf('day'),
  )
  const [chartWorkItems, setChartWorkItems] = useState<WorkItemListDto[]>([])
  const [percentileInputValue, setPercentileInputValue] = useState<number>(100)
  const [method, setMethod] = useState<CycleTimeOutlierMethod>('Balanced')
  const percentile = useDebounce(percentileInputValue, 500)

  const doneFrom = selectedDate.toISOString()

  const teamQuery = useGetTeamWorkItemsQuery(
    {
      idOrCode: source.type === 'team' ? source.teamCode : '',
      statusCategories: [WorkStatusCategory.Done],
      doneFrom,
      doneTo: null,
    },
    { skip: source.type !== 'team' },
  )

  const employeeQuery = useGetEmployeeWorkItemsQuery(
    {
      employeeId: source.type === 'employee' ? source.employeeId : '',
      statusCategories: [WorkStatusCategory.Done],
      doneFrom,
      doneTo: null,
    },
    { skip: source.type !== 'employee' },
  )

  const activeQuery = source.type === 'team' ? teamQuery : employeeQuery
  const { data: workItemsData, isLoading, error, refetch } = activeQuery

  const cycleTimeItems = getCycleTimeWorkItems(workItemsData)

  const normalizedPercentile = normalizePercentile(percentile)

  const sortedCycleTimeItems =
    normalizedPercentile === 1 || normalizedPercentile === 0
      ? cycleTimeItems
      : sortCycleTimeWorkItems(cycleTimeItems)

  const filteredWorkItems = (() => {
    if (cycleTimeItems.length === 0) {
      return []
    }

    if (normalizedPercentile === 1) {
      return cycleTimeItems
    }
    if (normalizedPercentile === 0) {
      return []
    }

    if (method === 'Balanced') {
      return applyBalancedPercentileFilter(
        sortedCycleTimeItems,
        normalizedPercentile,
      )
    }

    return applyForecastingPercentileFilter(
      sortedCycleTimeItems,
      normalizedPercentile,
    )
  })()

  if (error) {
    return <div>Error loading work items</div>
  }

  return (
    <Flex vertical>
      <Flex justify="space-between" align="start" wrap>
        <Space>
          <Title level={4} style={{ marginTop: 0 }}>
            Cycle Time Report
          </Title>
          <Tooltip
            title={
              <>
                Shows cycle time analysis for work items completed since
                the selected date.
                {` ${workItemsScope}`} Work items that were never activated and
                that went straight to Done are excluded.
                <br />
                <br />
                The chart updates dynamically based on grid filters.
              </>
            }
          >
            <InfoCircleOutlined />
          </Tooltip>
        </Space>
        <Flex gap="16px" wrap>
          <Space>
            <Tooltip title="Date represents the beginning of the day in UTC">
              <Text>Done From:</Text>
            </Tooltip>
            <DatePicker
              value={selectedDate}
              onChange={(date) =>
                date && setSelectedDate(date.utc().startOf('day'))
              }
              presets={doneFromPresets}
              format="YYYY-MM-DD"
              allowClear={false}
            />
          </Space>
          <Space>
            <Tooltip title="Percentage of work items included in the calculation after outliers are removed.">
              <Text>Percentile:</Text>
            </Tooltip>
            <InputNumber
              min={0}
              max={100}
              value={percentileInputValue}
              onChange={(value) => setPercentileInputValue(value ?? 100)}
              style={{ width: 90 }}
              suffix="%"
            />
          </Space>
          <Space>
            <Tooltip
              title={
                <>
                  Determines how outliers are identified and removed from cycle
                  time calculations.
                  <br />
                  <br />
                  Balanced: Provides a statistically balanced view of cycle time
                  and prevents extreme values from skewing averages.
                  <br />
                  <br />
                  Forecasting: Removes the slowest outliers from the dataset.
                </>
              }
            >
              <Text>Method:</Text>
            </Tooltip>
            <Select<CycleTimeOutlierMethod>
              value={method}
              onChange={setMethod}
              options={[
                { value: 'Balanced', label: 'Balanced' },
                { value: 'Forecasting', label: 'Forecasting' },
              ]}
              style={{ width: 120 }}
            />
          </Space>
        </Flex>
      </Flex>
      {workItemsData !== undefined && (
        <CycleTimeAnalysisChart
          workItems={chartWorkItems}
          isLoading={isLoading}
        />
      )}
      {/* The chart tracks the grid's displayed rows — grid filters re-scope it. */}
      <WorkItemsGrid
        workItems={filteredWorkItems}
        isLoading={isLoading}
        refetch={refetch}
        showStats={true}
        onDisplayedRowsChange={setChartWorkItems}
      />
    </Flex>
  )
}

export const CycleTimeReport: FC<CycleTimeReportProps> = ({ teamCode }) => {
  return (
    <CycleTimeReportContent
      source={{ type: 'team', teamCode }}
      workItemsScope="Shows work items owned by the selected team."
    />
  )
}

export const EmployeeCycleTimeReport: FC<EmployeeCycleTimeReportProps> = ({
  employeeId,
}) => {
  return (
    <CycleTimeReportContent
      source={{ type: 'employee', employeeId }}
      workItemsScope="Shows work items assigned to this employee."
    />
  )
}
