'use client'

import { DownloadOutlined } from '@ant-design/icons'
import {
  App,
  Button,
  DatePicker,
  Flex,
  Modal,
  Progress,
  Radio,
  RadioChangeEvent,
  Typography,
} from 'antd'
import type { TimeRangePickerProps } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC, useMemo, useRef, useState } from 'react'
import { ActivityLogDto } from '@/src/services/wayd-api'
import { downloadJsonWithTimestamp } from '@/src/utils/json-utils'

const { Text, Paragraph } = Typography
const { RangePicker } = DatePicker

export type ExportScope = 'all' | 'dateRange' | 'filtered'

export interface ExportActivitiesModalProps {
  open: boolean
  onClose: () => void
  activities?: ActivityLogDto[]
  totalCount?: number
  searchQuery?: string
  isMatchingSearch?: (activity: ActivityLogDto, query: string) => boolean
  exportFilename?: string
  onFetchBatch?: (
    page: number,
    pageSize: number,
  ) => Promise<{ items: ActivityLogDto[]; totalCount: number }>
}

export const formatEventForExport = (event: ActivityLogDto) => {
  let parsedPayload: unknown = event.payload
  if (typeof event.payload === 'string') {
    try {
      parsedPayload = JSON.parse(event.payload)
    } catch {
      parsedPayload = event.payload
    }
  }

  return {
    id: event.id,
    eventType: event.eventType,
    domainArea: event.domainArea,
    aggregateType: event.aggregateType,
    aggregateId: event.aggregateId,
    timestamp: event.timestamp,
    actorKind: event.actorKind,
    employee: event.employee
      ? {
          id: event.employee.id,
          key: event.employee.key,
          name: event.employee.name,
        }
      : undefined,
    summary: event.summary,
    correlationId: event.correlationId,
    eventVersion: event.eventVersion ?? '1.0',
    payload: parsedPayload,
  }
}

export const ExportActivitiesModal: FC<ExportActivitiesModalProps> = ({
  open,
  onClose,
  activities = [],
  totalCount,
  searchQuery = '',
  isMatchingSearch,
  exportFilename = 'activity-history',
  onFetchBatch,
}) => {
  const { message: messageApi } = App.useApp()
  const [scope, setScope] = useState<ExportScope>('all')
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs] | null>(null)
  const [isExporting, setIsExporting] = useState(false)
  const [progressPercent, setProgressPercent] = useState(0)
  const [progressText, setProgressText] = useState('')

  const safeSubtractDays = (days: number): Dayjs => {
    const now = dayjs()
    if (typeof now.subtract === 'function') {
      return now.subtract(days, 'day')
    }
    const d = new Date()
    d.setDate(d.getDate() - days)
    return dayjs(d)
  }

  const safeStartOfYear = (): Dayjs => {
    const now = dayjs()
    if (typeof now.startOf === 'function') {
      return now.startOf('year')
    }
    const d = new Date(new Date().getFullYear(), 0, 1)
    return dayjs(d)
  }

  const rangePresets: TimeRangePickerProps['presets'] = useMemo(
    () => [
      { label: 'Last 7 Days', value: [safeSubtractDays(7), dayjs()] },
      { label: 'Last 30 Days', value: [safeSubtractDays(30), dayjs()] },
      { label: 'Last 90 Days', value: [safeSubtractDays(90), dayjs()] },
      { label: 'This Year', value: [safeStartOfYear(), dayjs()] },
    ],
    [],
  )

  const isCancelledRef = useRef(false)

  const effectiveTotal = totalCount ?? activities.length
  const hasSearch = Boolean(searchQuery.trim())

  const handleScopeChange = (e: RadioChangeEvent) => {
    setScope(e.target.value)
  }

  const handleClose = () => {
    if (isExporting) {
      isCancelledRef.current = true
    }
    setIsExporting(false)
    setProgressPercent(0)
    setProgressText('')
    onClose()
  }

  const handleExport = async () => {
    isCancelledRef.current = false
    setIsExporting(true)
    setProgressPercent(0)
    setProgressText('Preparing export...')

    try {
      let allRecords: ActivityLogDto[] = []

      // If all records are already loaded or no batch fetcher is provided, use client activities
      if (activities.length >= effectiveTotal || !onFetchBatch) {
        allRecords = [...activities]
        setProgressPercent(100)
      } else {
        // Sequentially fetch all batches of 100
        const batchSize = 100
        const totalPages = Math.ceil(effectiveTotal / batchSize)

        for (let page = 1; page <= totalPages; page++) {
          if (isCancelledRef.current) return

          setProgressText(
            `Fetching batch ${page} of ${totalPages} (${allRecords.length} of ${effectiveTotal} events)...`,
          )
          setProgressPercent(Math.round(((page - 1) / totalPages) * 100))

          const response = await onFetchBatch(page, batchSize)
          const items = response?.items ?? []
          allRecords.push(...items)

          setProgressPercent(Math.round((page / totalPages) * 100))
        }
      }

      if (isCancelledRef.current) return

      setProgressText('Filtering and formatting events...')

      // Apply scope filter
      let targetEvents = allRecords

      if (scope === 'dateRange' && dateRange) {
        const [start, end] = dateRange
        const startDay = start.startOf('day')
        const endDay = end.endOf('day')

        targetEvents = targetEvents.filter((event) => {
          if (!event.timestamp) return false
          const t = dayjs(event.timestamp)
          return (
            (t.isAfter(startDay) || t.isSame(startDay)) &&
            (t.isBefore(endDay) || t.isSame(endDay))
          )
        })
      } else if (scope === 'filtered' && hasSearch && isMatchingSearch) {
        targetEvents = targetEvents.filter((event) =>
          isMatchingSearch(event, searchQuery),
        )
      }

      // Build envelope
      const exportEnvelope = {
        exportedAt: dayjs().toISOString(),
        entity: exportFilename,
        scope,
        dateRange:
          scope === 'dateRange' && dateRange
            ? {
                from: dateRange[0].startOf('day').toISOString(),
                to: dateRange[1].endOf('day').toISOString(),
              }
            : undefined,
        totalEvents: targetEvents.length,
        events: targetEvents.map(formatEventForExport),
      }

      const jsonString = JSON.stringify(exportEnvelope, null, 2)
      downloadJsonWithTimestamp(jsonString, exportFilename)

      messageApi.success(`Exported ${targetEvents.length} events to JSON`)
      handleClose()
    } catch (err) {
      console.error('Failed to export activity history:', err)
      messageApi.error('Failed to export activity history. Please try again.')
    } finally {
      setIsExporting(false)
    }
  }

  return (
    <Modal
      title={
        <Flex align="center" gap="small">
          <DownloadOutlined />
          <span>Export Activity History</span>
        </Flex>
      }
      open={open}
      onCancel={handleClose}
      destroyOnHidden
      mask={{ closable: !isExporting }}
      footer={[
        <Button key="cancel" onClick={handleClose} disabled={isExporting}>
          Cancel
        </Button>,
        <Button
          key="export"
          type="primary"
          icon={<DownloadOutlined />}
          loading={isExporting}
          onClick={handleExport}
          disabled={scope === 'dateRange' && !dateRange}
        >
          Download JSON
        </Button>,
      ]}
    >
      <Flex vertical gap="middle" style={{ marginTop: 16 }}>
        <Paragraph type="secondary" style={{ margin: 0 }}>
          Export activity events as a structured JSON file for auditing,
          reporting, or offline analysis.
        </Paragraph>

        <Radio.Group
          value={scope}
          onChange={handleScopeChange}
          disabled={isExporting}
        >
          <Flex vertical gap="small">
            <Radio value="all">
              <Flex vertical>
                <Text strong>All events</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  Export the complete recorded history ({effectiveTotal}{' '}
                  {effectiveTotal === 1 ? 'event' : 'events'})
                </Text>
              </Flex>
            </Radio>

            <Radio value="dateRange">
              <Flex vertical gap={6}>
                <Text strong>Date range</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  Export events that occurred within a specific timeframe
                </Text>
                {scope === 'dateRange' && (
                  <RangePicker
                    value={dateRange}
                    onChange={(dates) =>
                      setDateRange(
                        dates && dates[0] && dates[1]
                          ? [dates[0], dates[1]]
                          : null,
                      )
                    }
                    presets={rangePresets}
                    disabled={isExporting}
                    style={{ width: '100%', marginTop: 4 }}
                  />
                )}
              </Flex>
            </Radio>

            {hasSearch && (
              <Radio value="filtered">
                <Flex vertical>
                  <Text strong>Current search results</Text>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    Export only events matching &quot;{searchQuery}&quot;
                  </Text>
                </Flex>
              </Radio>
            )}
          </Flex>
        </Radio.Group>

        {isExporting && (
          <Flex
            vertical
            gap="xs"
            style={{
              marginTop: 8,
              padding: 12,
              background:
                'var(--ant-color-fill-quaternary, rgba(0, 0, 0, 0.02))',
              borderRadius: 8,
            }}
          >
            <Progress percent={progressPercent} status="active" />
            <Text type="secondary" style={{ fontSize: 12 }}>
              {progressText}
            </Text>
          </Flex>
        )}
      </Flex>
    </Modal>
  )
}

export default ExportActivitiesModal

