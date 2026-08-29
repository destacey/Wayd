'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { RecordFactsGroup } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetStrategicInitiativeQuery } from '@/src/store/features/ppm/strategic-initiatives-api'
import { getDrawerWidthPixels, isApiError } from '@/src/utils'
import { Divider, Drawer, Flex } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import RecordRoleList from '@/src/app/ppm/_components/record-role-list'
import { FC, useEffect, useState } from 'react'

export interface StrategicInitiativeDrawerProps {
  strategicInitiativeKey: number
  drawerOpen: boolean
  onDrawerClose: () => void
}

const StrategicInitiativeDrawer: FC<StrategicInitiativeDrawerProps> = ({
  strategicInitiativeKey,
  drawerOpen,
  onDrawerClose,
}: StrategicInitiativeDrawerProps) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()

  const {
    data: strategicInitiativeData,
    isLoading,
    error,
  } = useGetStrategicInitiativeQuery(strategicInitiativeKey)

  const { hasPermissionClaim } = useAuth()
  const canViewStrategicInitiative = hasPermissionClaim(
    'Permissions.StrategicInitiatives.View',
  )

  useEffect(() => {
    if (!canViewStrategicInitiative) {
      messageApi.error(
        'You do not have permission to view strategic initiatives.',
      )
      onDrawerClose()
    }
  }, [canViewStrategicInitiative, messageApi, onDrawerClose])

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading strategic initiative data. Please try again.',
      )
    }
  }, [error, messageApi])

  const hasStarted =
    strategicInitiativeData?.start &&
    dayjs(strategicInitiativeData.start).isBefore(dayjs(), 'day')

  const timelineFormat =
    strategicInitiativeData?.start &&
    strategicInitiativeData.end &&
    new Date(strategicInitiativeData.start).getFullYear() ===
      new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <Drawer
      title={strategicInitiativeData?.name ?? 'Strategic Initiative Details'}
      placement="right"
      onClose={onDrawerClose}
      open={drawerOpen}
      loading={isLoading}
      size={size}
      resizable={{
        onResize: (newSize) => setSize(newSize),
      }}
      destroyOnHidden={true}
    >
      <Flex vertical gap="middle">
        <Flex vertical gap={10}>
          <LabeledContent label="Key">
            <Link
              href={`/ppm/strategic-initiatives/${strategicInitiativeData?.key}`}
            >
              {strategicInitiativeData?.key}
            </Link>
          </LabeledContent>
          <LabeledContent label="Status">
            {strategicInitiativeData?.status.name}
          </LabeledContent>
          <LabeledContent label="Dates">
            <WaydDateRange
              dateRange={{
                start: strategicInitiativeData?.start,
                end: strategicInitiativeData?.end,
              }}
            />
          </LabeledContent>
          {strategicInitiativeData?.description && (
            <LabeledContent label="Description">
              <ExpandableContent background="var(--ant-color-bg-elevated)">
                <MarkdownRenderer
                  markdown={strategicInitiativeData.description}
                />
              </ExpandableContent>
            </LabeledContent>
          )}
        </Flex>

        <Divider size="small" style={{ margin: 0 }} />

        <RecordFactsGroup label="Roles">
          <LabeledContent label="Sponsors">
            <RecordRoleList
              people={
                strategicInitiativeData?.strategicInitiativeSponsors ?? []
              }
              emptyText="No sponsors assigned"
            />
          </LabeledContent>
          <LabeledContent label="Owners">
            <RecordRoleList
              people={
                strategicInitiativeData?.strategicInitiativeOwners ?? []
              }
              emptyText="No owners assigned"
            />
          </LabeledContent>
        </RecordFactsGroup>

        {strategicInitiativeData?.portfolio && (
          <>
            <Divider size="small" style={{ margin: 0 }} />
            <RecordFactsGroup label="Relationships">
              <LabeledContent label="Portfolio">
                <Link
                  href={`/ppm/portfolios/${strategicInitiativeData.portfolio.key}`}
                >
                  {strategicInitiativeData.portfolio.name}
                </Link>
              </LabeledContent>
            </RecordFactsGroup>
          </>
        )}

        {hasStarted && (
          <>
            <Divider size="small" style={{ margin: 0 }} />
            <TimelineProgress
              start={strategicInitiativeData?.start ?? null}
              end={strategicInitiativeData?.end ?? null}
              variant="borderless"
              style={{ width: '100%' }}
              dateFormat={timelineFormat}
            />
          </>
        )}

        {strategicInitiativeData?.id && (
          <>
            <Divider size="small" style={{ margin: 0 }} />
            <LinksCard objectId={strategicInitiativeData.id} width="100%" />
          </>
        )}
      </Flex>
    </Drawer>
  )
}

export default StrategicInitiativeDrawer
