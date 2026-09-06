'use client'

import {
  CheckCircleOutlined,
  CheckOutlined,
  ClockCircleOutlined,
  CopyOutlined,
  EditOutlined,
  PlusCircleOutlined,
  SearchOutlined,
  StopOutlined,
  SwapOutlined,
  UserOutlined,
} from '@ant-design/icons'
import {
  Avatar,
  Button,
  Card,
  Collapse,
  Descriptions,
  Empty,
  Flex,
  Input,
  Pagination,
  Skeleton,
  Tag,
  Tooltip,
  Typography,
  message,
  theme,
} from 'antd'
import dayjs from 'dayjs'
import { FC, useMemo, useState } from 'react'
import EntityLink from '@/src/components/common/entity-link'
import { ActivityLogDto, EventActorKind } from '@/src/services/wayd-api'
import { getInitials } from '@/src/utils/get-initials'

const { Text, Paragraph } = Typography

export interface ActivityLogTimelineProps {
  activities: ActivityLogDto[] | undefined
  isLoading: boolean
  totalCount?: number
  page?: number
  pageSize?: number
  onPageChange?: (page: number, pageSize: number) => void
  /** Custom message when there is no activity history. */
  emptyDescription?: string
}

const formatFieldLabel = (key: string): string => {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (s) => s.toUpperCase())
    .trim()
}

const formatEventTitle = (
  eventType: string,
  summary?: string | null,
): string => {
  if (summary && summary.trim().length > 0) {
    return summary
  }
  return formatFieldLabel(eventType.replace(/Event$/, ''))
}

const actorTagColor = (kind: EventActorKind): string => {
  switch (kind) {
    case EventActorKind.User:
      return 'blue'
    case EventActorKind.System:
      return 'default'
    case EventActorKind.Import:
      return 'orange'
    case EventActorKind.Sync:
      return 'cyan'
    default:
      return 'default'
  }
}

const getEventBadge = (
  eventType: string,
  token: ReturnType<typeof theme.useToken>['token'],
) => {
  const lower = eventType.toLowerCase()

  if (lower.includes('created') || lower.includes('added')) {
    return {
      icon: <PlusCircleOutlined style={{ color: token.colorSuccess }} />,
      color: 'green' as const,
      label: 'Created',
    }
  }

  if (lower.includes('activated')) {
    return {
      icon: <CheckCircleOutlined style={{ color: token.colorSuccess }} />,
      color: 'green' as const,
      label: 'Activated',
    }
  }

  if (
    lower.includes('deactivated') ||
    lower.includes('deleted') ||
    lower.includes('removed') ||
    lower.includes('withdrawn') ||
    lower.includes('failed')
  ) {
    return {
      icon: <StopOutlined style={{ color: token.colorError }} />,
      color: 'red' as const,
      label: 'Deactivated',
    }
  }

  if (
    lower.includes('updated') ||
    lower.includes('modified') ||
    lower.includes('changed')
  ) {
    return {
      icon: <EditOutlined style={{ color: token.colorPrimary }} />,
      color: 'blue' as const,
      label: 'Updated',
    }
  }

  if (
    lower.includes('assigned') ||
    lower.includes('reassigned') ||
    lower.includes('reparented')
  ) {
    return {
      icon: <SwapOutlined style={{ color: token.colorWarning }} />,
      color: 'gold' as const,
      label: 'Assigned',
    }
  }

  return {
    icon: <ClockCircleOutlined style={{ color: token.colorTextSecondary }} />,
    color: 'default' as const,
    label: 'Event',
  }
}

const getActorDisplay = (
  activity: ActivityLogDto,
  token: ReturnType<typeof theme.useToken>['token'],
) => {
  if (activity.employee) {
    return {
      title: (
        <EntityLink
          href={`/organizations/employees/${activity.employee.key}`}
        >
          {activity.employee.name}
        </EntityLink>
      ),
      subtitle: `Initiated by employee #${activity.employee.key}`,
      avatar: (
        <Avatar
          size={36}
          style={{
            backgroundColor: token.colorPrimary,
            fontWeight: 600,
          }}
        >
          {getInitials(activity.employee.name)}
        </Avatar>
      ),
    }
  }

  switch (activity.actorKind) {
    case EventActorKind.User:
      return {
        title: 'User',
        subtitle: 'Direct action performed by user',
        avatar: (
          <Avatar
            size={36}
            icon={<UserOutlined />}
            style={{ backgroundColor: token.colorPrimary }}
          />
        ),
      }
    case EventActorKind.Import:
      return {
        title: 'Data Import',
        subtitle: 'Bulk data import process',
        avatar: (
          <Avatar
            size={36}
            icon={<UserOutlined />}
            style={{ backgroundColor: token.colorWarning }}
          />
        ),
      }
    case EventActorKind.Sync:
      return {
        title: 'Integration Sync',
        subtitle: 'Automated synchronization from external system',
        avatar: (
          <Avatar
            size={36}
            icon={<UserOutlined />}
            style={{ backgroundColor: token.colorInfo }}
          />
        ),
      }
    case EventActorKind.Anonymous:
      return {
        title: 'Anonymous',
        subtitle: 'Action performed without authentication',
        avatar: (
          <Avatar
            size={36}
            icon={<UserOutlined />}
            style={{ backgroundColor: token.colorTextSecondary }}
          />
        ),
      }
    case EventActorKind.System:
    default:
      return {
        title: 'System Process',
        subtitle: 'Automated action performed by platform',
        avatar: (
          <Avatar
            size={36}
            icon={<UserOutlined />}
            style={{ backgroundColor: token.colorTextSecondary }}
          />
        ),
      }
  }
}

const parsePayloadDetails = (
  payload: string,
): Record<string, unknown> | null => {
  if (!payload) return null
  try {
    const raw = typeof payload === 'string' ? JSON.parse(payload) : payload
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null

    const excludedKeys = new Set([
      'id',
      'eventid',
      'timestamp',
      'actor',
      'aggregateid',
      'aggregatetype',
      'correlationid',
    ])

    const filtered: Record<string, unknown> = {}
    for (const [key, value] of Object.entries(raw)) {
      if (!excludedKeys.has(key.toLowerCase()) && value !== undefined) {
        filtered[key] = value
      }
    }

    return Object.keys(filtered).length > 0 ? filtered : null
  } catch {
    return null
  }
}

export const ActivityLogTimeline: FC<ActivityLogTimelineProps> = ({
  activities,
  isLoading,
  totalCount,
  page = 1,
  pageSize = 50,
  onPageChange,
  emptyDescription = 'No activity has been recorded for this record.',
}) => {
  const { token } = theme.useToken()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [copiedPayload, setCopiedPayload] = useState(false)
  const [copiedCorrelation, setCopiedCorrelation] = useState(false)

  const filteredActivities = useMemo(() => {
    if (!activities) return []
    if (!searchQuery.trim()) return activities

    const q = searchQuery.toLowerCase()
    return activities.filter((act) => {
      const title = act.summary || act.eventType
      const employeeName = act.employee?.name || ''
      const actorKind = act.actorKind || ''
      return (
        title.toLowerCase().includes(q) ||
        employeeName.toLowerCase().includes(q) ||
        actorKind.toLowerCase().includes(q) ||
        act.eventType.toLowerCase().includes(q) ||
        act.domainArea.toLowerCase().includes(q)
      )
    })
  }, [activities, searchQuery])

  const selectedActivity = useMemo(() => {
    if (!filteredActivities || filteredActivities.length === 0) return null
    if (selectedId) {
      const found = filteredActivities.find((a) => a.id === selectedId)
      if (found) return found
    }
    return filteredActivities[0]
  }, [filteredActivities, selectedId])

  const handleCopyPayload = (rawPayload: string) => {
    try {
      const formatted = JSON.stringify(JSON.parse(rawPayload), null, 2)
      navigator.clipboard.writeText(formatted)
    } catch {
      navigator.clipboard.writeText(rawPayload)
    }
    setCopiedPayload(true)
    message.success('Event payload copied to clipboard')
    setTimeout(() => setCopiedPayload(false), 2000)
  }

  const handleCopyCorrelationId = (id: string) => {
    navigator.clipboard.writeText(id)
    setCopiedCorrelation(true)
    message.success('Correlation ID copied to clipboard')
    setTimeout(() => setCopiedCorrelation(false), 2000)
  }

  if (isLoading) {
    return (
      <Card variant="outlined" style={{ borderRadius: token.borderRadiusLG }}>
        <Skeleton active paragraph={{ rows: 6 }} />
      </Card>
    )
  }

  if (!activities || activities.length === 0) {
    return (
      <Card variant="outlined" style={{ borderRadius: token.borderRadiusLG }}>
        <Empty description={emptyDescription} />
      </Card>
    )
  }

  const selectedDetails = selectedActivity
    ? parsePayloadDetails(selectedActivity.payload)
    : null

  let formattedRawPayload = ''
  if (selectedActivity?.payload) {
    try {
      formattedRawPayload = JSON.stringify(
        JSON.parse(selectedActivity.payload),
        null,
        2,
      )
    } catch {
      formattedRawPayload = selectedActivity.payload
    }
  }

  return (
    <Flex
      gap="middle"
      align="stretch"
      style={{
        width: '100%',
        minHeight: 560,
      }}
    >
      {/* LEFT COLUMN: Activity Ledger / Master List */}
      <Flex
        vertical
        style={{
          flex: '1 1 50%',
          minWidth: 320,
          background: token.colorBgContainer,
          border: `1px solid ${token.colorBorderSecondary}`,
          borderRadius: token.borderRadiusLG,
          overflow: 'hidden',
        }}
      >
        {/* List Header & Search */}
        <Flex
          vertical
          gap="xs"
          style={{
            padding: `${token.paddingSM}px ${token.paddingMD}px`,
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            background: token.colorFillQuaternary,
          }}
        >
          <Flex justify="space-between" align="center">
            <Text strong style={{ fontSize: token.fontSize }}>
              Activity History
            </Text>
            {totalCount !== undefined && (
              <Tag variant="filled" style={{ margin: 0 }}>
                {totalCount} {totalCount === 1 ? 'event' : 'events'}
              </Tag>
            )}
          </Flex>
          <Input
            placeholder="Search events, actors, or types..."
            prefix={
              <SearchOutlined style={{ color: token.colorTextSecondary }} />
            }
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            allowClear
            size="small"
            style={{ marginTop: 4 }}
          />
        </Flex>

        {/* Scrollable Items List */}
        <Flex
          vertical
          style={{
            flex: 1,
            maxHeight: 540,
            overflowY: 'auto',
          }}
        >
          {filteredActivities.length === 0 ? (
            <Flex
              justify="center"
              align="center"
              style={{ padding: token.paddingLG }}
            >
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="No matching activity found."
              />
            </Flex>
          ) : (
            filteredActivities.map((entry) => {
              const isSelected = selectedActivity?.id === entry.id
              const badge = getEventBadge(entry.eventType, token)
              const title = formatEventTitle(entry.eventType, entry.summary)
              const timestampDayjs = dayjs(entry.timestamp)

              return (
                <Flex
                  key={entry.id}
                  vertical
                  gap={4}
                  onClick={() => setSelectedId(entry.id)}
                  style={{
                    padding: `${token.paddingSM}px ${token.paddingMD}px`,
                    cursor: 'pointer',
                    borderBottom: `1px solid ${token.colorBorderSecondary}`,
                    borderLeft: isSelected
                      ? `3px solid ${token.colorPrimary}`
                      : '3px solid transparent',
                    background: isSelected
                      ? token.controlItemBgActive
                      : undefined,
                    transition: 'background-color 0.15s ease',
                  }}
                  onMouseEnter={(e) => {
                    if (!isSelected) {
                      e.currentTarget.style.backgroundColor =
                        token.colorFillAlter
                    }
                  }}
                  onMouseLeave={(e) => {
                    if (!isSelected) {
                      e.currentTarget.style.backgroundColor = ''
                    }
                  }}
                >
                  <Flex justify="space-between" align="center" gap="small">
                    <Flex align="center" gap="small" style={{ minWidth: 0 }}>
                      <Tag
                        color={badge.color}
                        variant="filled"
                        style={{ margin: 0, fontSize: token.fontSizeSM - 1 }}
                      >
                        {badge.label}
                      </Tag>
                      <Text
                        strong={isSelected}
                        ellipsis
                        style={{
                          fontSize: token.fontSize,
                          color: isSelected
                            ? token.colorPrimaryText
                            : token.colorText,
                        }}
                      >
                        {title}
                      </Text>
                    </Flex>
                    <Tooltip
                      title={timestampDayjs.format('MMM D, YYYY h:mm:ss A UTC')}
                    >
                      <Text
                        type="secondary"
                        style={{
                          fontSize: token.fontSizeSM,
                          whiteSpace: 'nowrap',
                          flexShrink: 0,
                        }}
                      >
                        {timestampDayjs.format('MMM D, YYYY h:mm A')}
                      </Text>
                    </Tooltip>
                  </Flex>

                  {/* Actor info line */}
                  <Flex align="center" gap="small" style={{ marginTop: 2 }}>
                    {entry.employee ? (
                      <Flex align="center" gap={6}>
                        <Avatar
                          size={18}
                          style={{
                            backgroundColor: token.colorPrimary,
                            fontSize: 10,
                          }}
                        >
                          {getInitials(entry.employee.name)}
                        </Avatar>
                        <Text
                          type="secondary"
                          style={{ fontSize: token.fontSizeSM }}
                          ellipsis
                        >
                          {entry.employee.name}
                        </Text>
                      </Flex>
                    ) : (
                      <Tag
                        color={actorTagColor(entry.actorKind)}
                        variant="filled"
                        style={{ margin: 0, fontSize: 10, lineHeight: '16px' }}
                      >
                        {entry.actorKind}
                      </Tag>
                    )}

                    {entry.summary && entry.summary !== title && (
                      <Text
                        type="secondary"
                        ellipsis
                        style={{ fontSize: token.fontSizeSM, maxWidth: 200 }}
                      >
                        · {entry.summary}
                      </Text>
                    )}
                  </Flex>
                </Flex>
              )
            })
          )}
        </Flex>

        {/* Pagination Toolbar */}
        {totalCount !== undefined && totalCount > pageSize && onPageChange && (
          <Flex
            justify="end"
            align="center"
            style={{
              padding: `${token.paddingXS}px ${token.paddingMD}px`,
              borderTop: `1px solid ${token.colorBorderSecondary}`,
              background: token.colorFillQuaternary,
            }}
          >
            <Pagination
              current={page}
              pageSize={pageSize}
              total={totalCount}
              onChange={onPageChange}
              showSizeChanger
              pageSizeOptions={['20', '50', '100']}
              size="small"
            />
          </Flex>
        )}
      </Flex>

      {/* RIGHT COLUMN: Detail Inspector Pane */}
      <Flex
        vertical
        gap="middle"
        style={{
          flex: '1 1 50%',
          minWidth: 340,
          background: token.colorBgContainer,
          border: `1px solid ${token.colorBorderSecondary}`,
          borderRadius: token.borderRadiusLG,
          padding: token.paddingMD,
          maxHeight: 650,
          overflowY: 'auto',
        }}
      >
        {selectedActivity ? (
          <>
            {/* Header / Event Title */}
            <Flex vertical gap="xs">
              <Flex
                justify="space-between"
                align="flex-start"
                wrap="wrap"
                gap="small"
              >
                <Flex vertical gap={2}>
                  <Text strong style={{ fontSize: token.fontSizeLG }}>
                    {formatEventTitle(
                      selectedActivity.eventType,
                      selectedActivity.summary,
                    )}
                  </Text>
                  <Text type="secondary" style={{ fontSize: token.fontSizeSM }}>
                    {dayjs(selectedActivity.timestamp).format(
                      'MMMM D, YYYY [at] h:mm:ss A UTC',
                    )}
                  </Text>
                </Flex>
                <Tag
                  color={getEventBadge(selectedActivity.eventType, token).color}
                  variant="filled"
                  style={{ margin: 0, padding: '2px 8px' }}
                >
                  {getEventBadge(selectedActivity.eventType, token).label}
                </Tag>
              </Flex>

              {/* Classification Badges */}
              <Flex gap="xs" wrap style={{ marginTop: 4 }}>
                <Tag style={{ margin: 0 }}>
                  <Text type="secondary">Type: </Text>
                  <Text code style={{ fontSize: token.fontSizeSM }}>
                    {selectedActivity.eventType}
                  </Text>
                </Tag>
                <Tag style={{ margin: 0 }}>
                  <Text type="secondary">Domain: </Text>
                  <Text strong>{selectedActivity.domainArea}</Text>
                </Tag>
                <Tag style={{ margin: 0 }}>
                  <Text type="secondary">Target: </Text>
                  <Text>{selectedActivity.aggregateType}</Text>
                </Tag>
              </Flex>
            </Flex>

            {/* Actor Card */}
            {(() => {
              const actorDisplay = getActorDisplay(selectedActivity, token)
              return (
                <Card
                  size="small"
                  variant="outlined"
                  style={{
                    borderRadius: token.borderRadius,
                    background: token.colorFillQuaternary,
                  }}
                >
                  <Flex align="center" gap="middle">
                    {actorDisplay.avatar}
                    <Flex vertical gap={2} style={{ flex: 1 }}>
                      <Flex align="center" gap="small">
                        <Text strong>{actorDisplay.title}</Text>
                        <Tag
                          color={actorTagColor(selectedActivity.actorKind)}
                          variant="filled"
                          style={{ margin: 0 }}
                        >
                          {selectedActivity.actorKind}
                        </Tag>
                      </Flex>
                      <Text
                        type="secondary"
                        style={{ fontSize: token.fontSizeSM }}
                      >
                        {actorDisplay.subtitle}
                      </Text>
                    </Flex>
                  </Flex>
                </Card>
              )
            })()}

            {/* Event Properties / Changed Values */}
            <Flex vertical gap="xs">
              <Text strong style={{ fontSize: token.fontSizeSM }}>
                Event Properties
              </Text>
              {selectedDetails ? (
                <Descriptions
                  size="small"
                  bordered
                  column={1}
                  style={{
                    background: token.colorBgContainer,
                    borderRadius: token.borderRadius,
                    overflow: 'hidden',
                  }}
                  items={Object.entries(selectedDetails).map(([key, val]) => ({
                    key,
                    label: (
                      <Text strong style={{ fontSize: token.fontSizeSM }}>
                        {formatFieldLabel(key)}
                      </Text>
                    ),
                    children:
                      val === null || val === undefined || val === '' ? (
                        <Text type="secondary" italic>
                          None
                        </Text>
                      ) : typeof val === 'boolean' ? (
                        <Tag
                          color={val ? 'green' : 'default'}
                          style={{ margin: 0 }}
                        >
                          {val ? 'Yes' : 'No'}
                        </Tag>
                      ) : typeof val === 'object' ? (
                        <Text code style={{ wordBreak: 'break-all' }}>
                          {JSON.stringify(val)}
                        </Text>
                      ) : (
                        <Text style={{ wordBreak: 'break-word' }}>
                          {String(val)}
                        </Text>
                      ),
                  }))}
                />
              ) : (
                <Text
                  type="secondary"
                  style={{
                    fontSize: token.fontSizeSM,
                    padding: `${token.paddingSM}px 0`,
                  }}
                >
                  No domain property details recorded for this event.
                </Text>
              )}
            </Flex>

            {/* Traceability & Raw Payload Inspector */}
            <Collapse
              ghost
              size="small"
              items={[
                {
                  key: 'raw-payload',
                  label: (
                    <Text
                      type="secondary"
                      style={{ fontSize: token.fontSizeSM }}
                    >
                      Raw Event Payload & Traceability
                    </Text>
                  ),
                  children: (
                    <Flex vertical gap="small">
                      {selectedActivity.correlationId && (
                        <Flex
                          justify="space-between"
                          align="center"
                          style={{
                            padding: '6px 10px',
                            background: token.colorFillQuaternary,
                            borderRadius: token.borderRadiusSM,
                          }}
                        >
                          <Flex align="center" gap="small">
                            <Text
                              type="secondary"
                              style={{ fontSize: token.fontSizeSM }}
                            >
                              Correlation ID:
                            </Text>
                            <Text code style={{ fontSize: token.fontSizeSM }}>
                              {selectedActivity.correlationId}
                            </Text>
                          </Flex>
                          <Button
                            type="text"
                            size="small"
                            icon={
                              copiedCorrelation ? (
                                <CheckOutlined
                                  style={{ color: token.colorSuccess }}
                                />
                              ) : (
                                <CopyOutlined />
                              )
                            }
                            onClick={() =>
                              handleCopyCorrelationId(
                                selectedActivity.correlationId!,
                              )
                            }
                          />
                        </Flex>
                      )}

                      <Flex justify="space-between" align="center">
                        <Text
                          type="secondary"
                          style={{ fontSize: token.fontSizeSM }}
                        >
                          JSON Payload:
                        </Text>
                        <Button
                          type="text"
                          size="small"
                          icon={
                            copiedPayload ? (
                              <CheckOutlined
                                style={{ color: token.colorSuccess }}
                              />
                            ) : (
                              <CopyOutlined />
                            )
                          }
                          onClick={() =>
                            handleCopyPayload(selectedActivity.payload)
                          }
                        >
                          {copiedPayload ? 'Copied' : 'Copy JSON'}
                        </Button>
                      </Flex>

                      <Paragraph
                        code
                        style={{
                          margin: 0,
                          padding: token.paddingSM,
                          background: token.colorFillQuaternary,
                          borderRadius: token.borderRadiusSM,
                          maxHeight: 220,
                          overflowY: 'auto',
                          fontSize: 11,
                          whiteSpace: 'pre-wrap',
                          wordBreak: 'break-all',
                        }}
                      >
                        {formattedRawPayload}
                      </Paragraph>
                    </Flex>
                  ),
                },
              ]}
            />
          </>
        ) : (
          <Flex
            justify="center"
            align="center"
            style={{ height: '100%', minHeight: 260 }}
          >
            <Empty description="Select an activity to view details." />
          </Flex>
        )}
      </Flex>
    </Flex>
  )
}

export default ActivityLogTimeline

