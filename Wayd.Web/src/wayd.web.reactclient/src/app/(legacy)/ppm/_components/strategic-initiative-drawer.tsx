'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ContentList,
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetStrategicInitiativeQuery } from '@/src/store/features/ppm/strategic-initiatives-api'
import { getDrawerWidthPixels, getSortedNameList, isApiError} from '@/src/utils'
import { Drawer, Flex } from 'antd'
import Link from 'next/link'
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

  const sponsorNames = getSortedNameList(
    strategicInitiativeData?.strategicInitiativeSponsors ?? [],
  )

  const ownerNames = getSortedNameList(
    strategicInitiativeData?.strategicInitiativeOwners ?? [],
  )

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
          <LabeledContent label="Sponsors">
            <ContentList
              items={sponsorNames}
              emptyText="No sponsors assigned"
            />
          </LabeledContent>
          <LabeledContent label="Owners">
            <ContentList items={ownerNames} emptyText="No owners assigned" />
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
        {strategicInitiativeData?.id && (
          <LinksCard objectId={strategicInitiativeData.id} width="100%" />
        )}
      </Flex>
    </Drawer>
  )
}

export default StrategicInitiativeDrawer
