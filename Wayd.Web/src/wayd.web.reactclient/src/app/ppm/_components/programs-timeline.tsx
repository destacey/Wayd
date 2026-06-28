'use client'

import { FC, ReactNode, useState } from 'react'
import dayjs from 'dayjs'
import { theme } from 'antd'
import { ProgramListDto } from '@/src/services/wayd-api'
import { WaydTimeline } from '@/src/components/common/timeline'
import type { TimelineItem } from '@/src/components/common/timeline'
import { getLifecyclePhaseColorFromStatus } from '@/src/utils'
import { ProgramDrawer } from '.'

const ms = (d: dayjs.ConfigType) => dayjs(d).valueOf()

interface ProgramPayload {
  dto: ProgramListDto
}

export interface ProgramsTimelineProps {
  programs: ProgramListDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactNode
  onRefresh?: () => void
}

function mapPrograms(
  programs: ProgramListDto[],
  token: ReturnType<typeof theme.useToken>['token'],
): {
  items: TimelineItem<ProgramPayload>[]
  windowStart: number
  windowEnd: number
  minDate: number
  maxDate: number
} {
  const dated = programs.filter((p) => p.start && p.end)

  let minMs = dated.length > 0 ? ms(dated[0].start!) : dayjs().valueOf()
  let maxMs = dated.length > 0 ? ms(dated[0].end!) : dayjs().valueOf()

  const items: TimelineItem<ProgramPayload>[] = dated.map((p, idx) => {
    const start = ms(p.start!)
    const end = ms(p.end!)
    if (start < minMs) minMs = start
    if (end > maxMs) maxMs = end
    return {
      id: String(p.id),
      kind: 'range',
      label: p.name ?? '',
      color: getLifecyclePhaseColorFromStatus(p.status, token),
      start,
      end,
      order: idx,
      data: { dto: p },
    }
  })

  const windowStart = dayjs().subtract(6, 'months').valueOf()
  const windowEnd = dayjs().add(6, 'months').valueOf()
  const minDate = dayjs(minMs).subtract(1, 'month').valueOf()
  const maxDate = dayjs(maxMs).add(1, 'month').valueOf()

  return { items, windowStart, windowEnd, minDate, maxDate }
}

const ProgramsTimeline: FC<ProgramsTimelineProps> = ({
  programs,
  isLoading,
  viewSelector,
  onRefresh,
}) => {
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedKey, setSelectedKey] = useState<number | null>(null)
  const { token } = theme.useToken()

  const { items, windowStart, windowEnd, minDate, maxDate } = mapPrograms(
    isLoading ? [] : programs,
    token,
  )

  return (
    <>
      <WaydTimeline<ProgramPayload>
        variant="timeline"
        items={items}
        windowStart={windowStart}
        windowEnd={windowEnd}
        minDate={minDate}
        maxDate={maxDate}
        storageKey="ppm-programs"
        height={650}
        editable={false}
        isLoading={isLoading}
        allowFullScreen
        allowSaveAsImage
        saveImageFileName="Programs Timeline"
        onRefresh={onRefresh}
        toolbarRightSlot={viewSelector}
        onItemClick={(item) => {
          if (!item.data) return
          setSelectedKey(item.data.dto.key)
          setDrawerOpen(true)
        }}
      />
      {selectedKey !== null && (
        <ProgramDrawer
          programKey={selectedKey}
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

export default ProgramsTimeline
