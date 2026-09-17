'use client'

import {
  DeliveryRecordKind,
  ProductStatusAlias,
  RecentDeliveryEventDto,
} from '@/src/services/wayd-api'
import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  InboxOutlined,
} from '@ant-design/icons'
import { Empty, Flex, List, Tooltip, Typography, theme } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Text } = Typography

/**
 * How a record is described by the state it reached.
 *
 * Keyed on the alias rather than the status name so an organization that renames "Released" to
 * "Shipped" still gets the icon and colour the meaning deserves.
 */
const appearance = (alias: ProductStatusAlias) => {
  switch (alias) {
    case ProductStatusAlias.Released:
      return { icon: <CheckCircleOutlined />, tone: 'success' as const }
    case ProductStatusAlias.Withdrawn:
      return { icon: <CloseCircleOutlined />, tone: 'danger' as const }
    case ProductStatusAlias.Ready:
      return { icon: <ClockCircleOutlined />, tone: 'secondary' as const }
    default:
      return { icon: <InboxOutlined />, tone: 'secondary' as const }
  }
}

/**
 * The time, with the day only when it is not today.
 *
 * A feed is read as "what just happened", so repeating today's date on every row is noise — but
 * dropping the day entirely makes yesterday look like this morning.
 */
const when = (changedOn: string | Date): string => {
  const at = dayjs(changedOn)
  const today = dayjs()

  if (at.isSame(today, 'day')) return at.format('HH:mm')
  if (at.isSame(today.subtract(1, 'day'), 'day')) return `Yest. ${at.format('HH:mm')}`
  // Without the year a December event read as this December once January arrived.
  if (!at.isSame(today, 'year')) return at.format('D MMM YYYY HH:mm')
  return at.format('D MMM HH:mm')
}

const href = (event: RecentDeliveryEventDto): string =>
  event.kind === DeliveryRecordKind.ReleasePackage
    ? `/product-management/release-packages/${event.recordKey}`
    : `/product-management/versions/${event.recordKey}`

/** The line under the title: what happened, when, and whatever context that state needs. */
const detail = (event: RecentDeliveryEventDto): string => {
  const parts = [event.statusName, when(event.changedOn)]

  if (event.kind === DeliveryRecordKind.ReleasePackage) {
    parts.unshift(
      `Package · ${event.componentCount} component${event.componentCount === 1 ? '' : 's'}`,
    )
  }

  // A withdrawal has to say what it withdrew, or a reader is left asking when the thing went out.
  if (event.alias === ProductStatusAlias.Withdrawn && event.releasedDate) {
    parts.push(`released ${dayjs(event.releasedDate).format('D MMM')}`)
  }

  if (event.alias === ProductStatusAlias.Ready && !event.releasedDate) {
    parts.push('awaiting release')
  }

  return parts.join(' · ')
}

export interface RecentDeliveryActivityProps {
  events: RecentDeliveryEventDto[]
}

/**
 * What has happened to versions and packages lately.
 *
 * Ordered by when each record changed rather than by its dates: the dates carry no time of day, so
 * two things that happened on one afternoon could not be told apart.
 */
const RecentDeliveryActivity = ({ events }: RecentDeliveryActivityProps) => {
  const { token } = theme.useToken()

  if (events.length === 0) {
    return (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description="Nothing has happened yet."
      />
    )
  }

  return (
    <List
      size="small"
      dataSource={events}
      renderItem={(event) => {
        const { icon, tone } = appearance(event.alias)

        return (
          <List.Item
            key={`${event.recordId}-${event.changedOn}`}
            style={
              event.alias === ProductStatusAlias.Withdrawn
                ? { background: token.colorErrorBg }
                : undefined
            }
          >
            <Flex gap={10} align="flex-start" style={{ width: '100%' }}>
              <Text type={tone === 'secondary' ? 'secondary' : tone}>{icon}</Text>
              <Flex vertical gap={2} style={{ minWidth: 0 }}>
                <Flex gap={8} align="baseline" wrap>
                  <Link href={href(event)}>
                    <Text strong>{event.product?.name ?? event.label}</Text>
                  </Link>
                  {event.product && (
                    <Text type="secondary" style={{ fontSize: 12 }}>
                      {event.label}
                    </Text>
                  )}
                </Flex>
                <Tooltip title={dayjs(event.changedOn).format('ddd D MMM YYYY HH:mm')}>
                  <Text
                    type={tone === 'danger' ? 'danger' : 'secondary'}
                    style={{ fontSize: 12 }}
                  >
                    {detail(event)}
                  </Text>
                </Tooltip>
              </Flex>
            </Flex>
          </List.Item>
        )
      }}
    />
  )
}

export default RecentDeliveryActivity
