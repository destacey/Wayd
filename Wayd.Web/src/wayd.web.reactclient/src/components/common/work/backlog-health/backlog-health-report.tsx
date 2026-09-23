'use client'

import { InfoCircleOutlined } from '@ant-design/icons'
import {
  Alert,
  Button,
  Flex,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { FC, ReactNode, useState } from 'react'
import {
  BacklogHealthCheckDto,
  TeamBacklogHealthDto,
} from '@/src/services/wayd-api'
import { useGetTeamBacklogHealthQuery } from '@/src/store/features/organizations/team-api'
import { isApiError } from '@/src/utils/problem-details'
import { healthCheckTagColor } from '../../health-check/health-check-utils'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import BacklogHealthGrid from './backlog-health-grid'
import {
  BacklogHealthCheck,
  describeCheck,
  findCheck,
  formatCheckValue,
  isMeasureCheck,
} from './backlog-health-formatting'
import {
  EMPTY_BACKLOG_HEALTH_SETTINGS,
  hasOverrides,
} from './backlog-health-settings'
import BacklogHealthSettingsPopover from './backlog-health-settings-popover'
import { useBacklogHealthSettings } from './use-backlog-health-settings'

const { Title, Text } = Typography

const GradeTag: FC<{ check?: BacklogHealthCheckDto }> = ({ check }) =>
  check?.grade ? (
    <Tag color={healthCheckTagColor(check.grade.name)}>{check.grade.name}</Tag>
  ) : (
    <Text type="secondary">Not graded</Text>
  )

const checkColumns = (
  health: TeamBacklogHealthDto | undefined,
): ColumnsType<BacklogHealthCheckDto> => [
  {
    key: 'check',
    title: 'Check',
    render: (_, check) => (
      <Space>
        <Text>{check.check.name}</Text>
        {health && (
          <Tooltip
            title={describeCheck(check.check.id as BacklogHealthCheck, health)}
          >
            <InfoCircleOutlined />
          </Tooltip>
        )}
      </Space>
    ),
  },
  {
    key: 'grade',
    title: 'Grade',
    width: 140,
    render: (_, check) => <GradeTag check={check} />,
  },
  {
    key: 'result',
    title: 'Result',
    width: 200,
    render: (_, check) => formatCheckValue(check),
  },
]

export interface BacklogHealthReportViewProps {
  health?: TeamBacklogHealthDto
  isLoading: boolean
  error?: unknown
  refetch: () => void
  /** The thresholds control, rendered beside the title. */
  settings: ReactNode
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
  /** Returns the report to the default thresholds; offered when it fails to load. */
  onReset?: () => void
}

/** The API's validation messages, when the request was rejected as invalid. */
const validationMessages = (error: unknown): string[] =>
  isApiError(error) && error.errors ? Object.values(error.errors).flat() : []

export const BacklogHealthReportView: FC<BacklogHealthReportViewProps> = ({
  health,
  isLoading,
  error,
  refetch,
  settings,
  persistStateKey,
  onReset,
}) => {
  const [selectedCheck, setSelectedCheck] = useState<BacklogHealthCheck>()

  const checks = health?.checks ?? []
  const itemChecks = checks.filter((c) => !isMeasureCheck(c))
  const measure = (check: BacklogHealthCheck) => findCheck(checks, check)

  const workItems = health?.workItems ?? []
  const shownWorkItems =
    selectedCheck === undefined
      ? workItems
      : workItems.filter((w) => w.flags.some((f) => f.id === selectedCheck))
  const selectedName = checks.find((c) => c.check.id === selectedCheck)?.check
    .name

  const measureCard = (check: BacklogHealthCheck, title: string) => {
    const result = measure(check)
    return (
      <MetricCard
        title={title}
        tooltip={health && describeCheck(check, health)}
        value={result ? formatCheckValue(result) : ''}
        secondaryValue={<GradeTag check={result} />}
        loading={isLoading && !health}
        cardStyle={METRIC_CARD_FLEX}
      />
    )
  }

  return (
    // A plain block, not a flex column: WaydGrid sizes itself as a block child
    // and collapses to its toolbar as the item of an unsized flex container.
    <div>
      <Flex
        justify="space-between"
        align="start"
        wrap
        style={{ marginBottom: 16 }}
      >
        <Space>
          <Title level={4} style={{ marginTop: 0 }}>
            Backlog Health
          </Title>
          <Tooltip title="Grades the team's open backlog. Select a check to list the work items it flagged. Thresholds you change are kept for you and added to the page link, so a shared link shows the same grades.">
            <InfoCircleOutlined />
          </Tooltip>
        </Space>
        {settings}
      </Flex>

      {error ? (
        <Alert
          type="error"
          title="The backlog health report could not be loaded."
          description={
            validationMessages(error).length > 0 && (
              <ul style={{ margin: 0, paddingInlineStart: 20 }}>
                {validationMessages(error).map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            )
          }
          // The failing request may be using thresholds the viewer saved, and
          // the thresholds control needs a loaded report to open, so the way
          // back to the defaults is offered here too.
          action={
            onReset && (
              <Button size="small" onClick={onReset}>
                Reset to defaults
              </Button>
            )
          }
          showIcon
        />
      ) : (
        <>
          <Flex gap="middle" wrap style={{ marginBottom: 16 }}>
            <MetricCard
              title="Backlog"
              value={health?.totalWorkItems ?? 0}
              secondaryValue={
                health &&
                `${health.proposedWorkItems} proposed · ${health.activeWorkItems} active · ${health.totalStoryPoints} pts`
              }
              loading={isLoading && !health}
              cardStyle={METRIC_CARD_FLEX}
            />
            {measureCard(BacklogHealthCheck.Runway, 'Runway')}
            {measureCard(BacklogHealthCheck.NetFlow, 'Net Flow')}
            {measureCard(BacklogHealthCheck.WipLoad, 'WIP Load')}
          </Flex>

          {health && (
            <Text
              type="secondary"
              style={{ display: 'block', marginBottom: 16 }}
            >
              {`History ${dayjs(health.from).format('MMM D, YYYY')} – ${dayjs(health.to).format('MMM D, YYYY')}: ${health.itemsCompleted} completed, ${health.itemsCreated} created. Readiness checks look at the top ${health.readinessWindowWorkItems} work items.`}
              {health.agingWipDays != null &&
                ` Aging beyond ${Number(health.agingWipDays.toFixed(1))} days.`}
              {health.oversizedStoryPoints != null &&
                ` Oversized above ${health.oversizedStoryPoints} points.`}
            </Text>
          )}

          <Table<BacklogHealthCheckDto>
            size="small"
            rowKey={(c) => c.check.id}
            columns={checkColumns(health)}
            dataSource={itemChecks}
            loading={isLoading && !health}
            pagination={false}
            style={{ marginBottom: 16 }}
            rowSelection={{
              type: 'radio',
              selectedRowKeys:
                selectedCheck === undefined ? [] : [selectedCheck],
              onChange: (keys) =>
                setSelectedCheck(keys[0] as BacklogHealthCheck),
              getCheckboxProps: (c) => ({ disabled: !c.flagged }),
            }}
            onRow={(c) => ({
              onClick: () =>
                c.flagged &&
                setSelectedCheck(
                  selectedCheck === c.check.id
                    ? undefined
                    : (c.check.id as BacklogHealthCheck),
                ),
              style: { cursor: c.flagged ? 'pointer' : undefined },
            })}
          />

          <Flex align="center" gap="small" style={{ marginBottom: 8 }}>
            {selectedName ? (
              <Tag closable onClose={() => setSelectedCheck(undefined)}>
                {`Flagged by ${selectedName}`}
              </Tag>
            ) : (
              <Text type="secondary">All backlog work items</Text>
            )}
          </Flex>
          <BacklogHealthGrid
            workItems={shownWorkItems}
            isLoading={isLoading}
            refetch={refetch}
            persistStateKey={persistStateKey}
          />
        </>
      )}
    </div>
  )
}

export const BacklogHealthReport: FC<{ teamCode: string }> = ({ teamCode }) => {
  const [settings, setSettings] = useBacklogHealthSettings(
    `team-backlog-health:${teamCode}`,
  )

  const { data, isFetching, error, refetch } = useGetTeamBacklogHealthQuery({
    teamIdOrCode: teamCode,
    lookbackDays: settings.lookbackDays,
    thresholds: settings.thresholds,
  })

  return (
    <BacklogHealthReportView
      health={data}
      isLoading={isFetching}
      error={error}
      refetch={refetch}
      persistStateKey="team-backlog-health"
      onReset={
        hasOverrides(settings)
          ? () => setSettings(EMPTY_BACKLOG_HEALTH_SETTINGS)
          : undefined
      }
      settings={
        <BacklogHealthSettingsPopover
          settings={settings}
          effective={
            data && {
              lookbackDays: data.lookbackDays,
              thresholds: data.thresholds,
            }
          }
          onChange={setSettings}
        />
      }
    />
  )
}
