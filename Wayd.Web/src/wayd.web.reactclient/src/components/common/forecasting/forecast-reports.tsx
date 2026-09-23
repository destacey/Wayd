'use client'

import {
  ForecastOptionsRequest,
  useGetObjectiveForecastQuery,
  useGetProjectForecastQuery,
  useGetTeamThroughputForecastQuery,
  useGetWorkItemForecastQuery,
} from '@/src/store/features/work-management/forecasts-api'
import { Flex } from 'antd'
import dayjs from 'dayjs'
import { FC, ReactNode, useState } from 'react'
import CompletionForecast from './completion-forecast'
import ForecastSettingsBar, {
  DEFAULT_FORECAST_SETTINGS,
  ForecastSettings,
} from './forecast-settings-bar'
import ThroughputForecast from './throughput-forecast'

const toRequest = (settings: ForecastSettings): ForecastOptionsRequest => ({
  targetDate: settings.targetDate?.format('YYYY-MM-DD'),
  lookbackDays: settings.lookbackDays,
  ignoreDependencies: settings.ignoreDependencies,
})

const ReportLayout: FC<{ settings: ReactNode; children: ReactNode }> = ({
  settings,
  children,
}) => (
  <Flex vertical gap="middle">
    {settings}
    {children}
  </Flex>
)

export const WorkItemForecastReport: FC<{
  workspaceKey: string
  workItemKey: string
}> = ({ workspaceKey, workItemKey }) => {
  const [settings, setSettings] = useState(DEFAULT_FORECAST_SETTINGS)
  const { data, isFetching, error } = useGetWorkItemForecastQuery({
    workspaceIdOrKey: workspaceKey,
    workItemKey,
    ...toRequest(settings),
  })
  return (
    <ReportLayout
      settings={
        <ForecastSettingsBar
          value={settings}
          onChange={setSettings}
          targetDateLabel="Target date"
          targetDatePlaceholder="None"
          showIgnoreDependencies
        />
      }
    >
      <CompletionForecast
        forecast={data}
        isLoading={isFetching}
        error={error}
      />
    </ReportLayout>
  )
}

export const ObjectiveForecastReport: FC<{
  planningIntervalKey: string
  objectiveKey: string
}> = ({ planningIntervalKey, objectiveKey }) => {
  const [settings, setSettings] = useState(DEFAULT_FORECAST_SETTINGS)
  const { data, isFetching, error } = useGetObjectiveForecastQuery({
    planningIntervalIdOrKey: planningIntervalKey,
    objectiveIdOrKey: objectiveKey,
    ...toRequest(settings),
  })
  return (
    <ReportLayout
      settings={
        <ForecastSettingsBar
          value={settings}
          onChange={setSettings}
          targetDateLabel="Target date"
          targetDatePlaceholder="Objective's"
          showIgnoreDependencies
        />
      }
    >
      <CompletionForecast
        forecast={data}
        isLoading={isFetching}
        error={error}
      />
    </ReportLayout>
  )
}

export const ProjectForecastReport: FC<{ projectKey: string }> = ({
  projectKey,
}) => {
  const [settings, setSettings] = useState(DEFAULT_FORECAST_SETTINGS)
  const { data, isFetching, error } = useGetProjectForecastQuery({
    projectIdOrKey: projectKey,
    ...toRequest(settings),
  })
  return (
    <ReportLayout
      settings={
        <ForecastSettingsBar
          value={settings}
          onChange={setSettings}
          targetDateLabel="Target date"
          targetDatePlaceholder="Planned end"
          showIgnoreDependencies
        />
      }
    >
      <CompletionForecast
        forecast={data}
        isLoading={isFetching}
        error={error}
      />
    </ReportLayout>
  )
}

export const TeamThroughputForecastReport: FC<{ teamCode: string }> = ({
  teamCode,
}) => {
  // Two weeks, today included — a typical sprint.
  const [settings, setSettings] = useState<ForecastSettings>(() => ({
    ...DEFAULT_FORECAST_SETTINGS,
    targetDate: dayjs().add(13, 'day'),
  }))

  const { data, isFetching, error } = useGetTeamThroughputForecastQuery({
    teamIdOrCode: teamCode,
    targetDate: settings.targetDate!.format('YYYY-MM-DD'),
    lookbackDays: settings.lookbackDays,
  })

  return (
    <ReportLayout
      settings={
        <ForecastSettingsBar
          value={settings}
          onChange={setSettings}
          targetDateLabel="Forecast through"
          targetDateRequired
        />
      }
    >
      <ThroughputForecast
        forecast={data}
        isLoading={isFetching}
        error={error}
      />
    </ReportLayout>
  )
}
