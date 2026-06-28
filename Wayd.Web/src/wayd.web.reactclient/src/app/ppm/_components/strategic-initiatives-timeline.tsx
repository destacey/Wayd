'use client'

import { FC, ReactNode, useState } from 'react'
import dayjs from 'dayjs'
import { theme } from 'antd'
import { StrategicInitiativeListDto } from '@/src/services/wayd-api'
import { WaydTimeline } from '@/src/components/common/timeline'
import type { TimelineItem } from '@/src/components/common/timeline'
import { getLifecyclePhaseColorFromStatus } from '@/src/utils'
import { StrategicInitiativeDrawer } from '.'

const ms = (d: dayjs.ConfigType) => dayjs(d).valueOf()

interface StrategicInitiativePayload {
  dto: StrategicInitiativeListDto
}

export interface StrategicInitiativesTimelineProps {
  strategicInitiatives: StrategicInitiativeListDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactNode
  onRefresh?: () => void
}

function mapStrategicInitiatives(
  initiatives: StrategicInitiativeListDto[],
  token: ReturnType<typeof theme.useToken>['token'],
): {
  items: TimelineItem<StrategicInitiativePayload>[]
  windowStart: number
  windowEnd: number
  minDate: number
  maxDate: number
} {
  const dated = initiatives.filter((i) => i.start && i.end)

  let minMs = dated.length > 0 ? ms(dated[0].start!) : dayjs().valueOf()
  let maxMs = dated.length > 0 ? ms(dated[0].end!) : dayjs().valueOf()

  const items: TimelineItem<StrategicInitiativePayload>[] = dated.map(
    (i, idx) => {
      const start = ms(i.start!)
      const end = ms(i.end!)
      if (start < minMs) minMs = start
      if (end > maxMs) maxMs = end
      return {
        id: String(i.id),
        kind: 'range',
        label: i.name ?? '',
        color: getLifecyclePhaseColorFromStatus(i.status, token),
        start,
        end,
        order: idx,
        data: { dto: i },
      }
    },
  )

  const windowStart = dayjs().subtract(6, 'months').valueOf()
  const windowEnd = dayjs().add(6, 'months').valueOf()
  const minDate = dayjs(minMs).subtract(1, 'month').valueOf()
  const maxDate = dayjs(maxMs).add(1, 'month').valueOf()

  return { items, windowStart, windowEnd, minDate, maxDate }
}

const StrategicInitiativesTimeline: FC<StrategicInitiativesTimelineProps> =
  ({ strategicInitiatives, isLoading, viewSelector, onRefresh }) => {
    const [drawerOpen, setDrawerOpen] = useState(false)
    const [selectedKey, setSelectedKey] = useState<number | null>(null)
    const { token } = theme.useToken()

    const { items, windowStart, windowEnd, minDate, maxDate } = mapStrategicInitiatives(
      isLoading ? [] : strategicInitiatives,
      token,
    )

    return (
      <>
        <WaydTimeline<StrategicInitiativePayload>
          variant="timeline"
          items={items}
          windowStart={windowStart}
          windowEnd={windowEnd}
          minDate={minDate}
          maxDate={maxDate}
          storageKey="ppm-strategic-initiatives"
          height={650}
          editable={false}
          isLoading={isLoading}
          allowFullScreen
          allowSaveAsImage
          saveImageFileName="Strategic Initiatives Timeline"
          onRefresh={onRefresh}
          toolbarRightSlot={viewSelector}
          onItemClick={(item) => {
            if (!item.data) return
            setSelectedKey(item.data.dto.key)
            setDrawerOpen(true)
          }}
        />
        {selectedKey !== null && (
          <StrategicInitiativeDrawer
            strategicInitiativeKey={selectedKey}
            drawerOpen={drawerOpen}
            onDrawerClose={() => {
              setDrawerOpen(false)
              setSelectedKey(null)
            }}
          />
        )}
      </>
    )
  }

export default StrategicInitiativesTimeline
