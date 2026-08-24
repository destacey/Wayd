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
import { useGetProgramQuery } from '@/src/store/features/ppm/programs-api'
import { getDrawerWidthPixels, getSortedNameList, isApiError} from '@/src/utils'
import { Drawer, Flex } from 'antd'
import Link from 'next/link'
import { FC, useEffect, useState } from 'react'

export interface ProgramDrawerProps {
  programKey: number
  drawerOpen: boolean
  onDrawerClose: () => void
}

const ProgramDrawer: FC<ProgramDrawerProps> = ({
  programKey,
  drawerOpen,
  onDrawerClose,
}: ProgramDrawerProps) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()

  const { data: programData, isLoading, error } = useGetProgramQuery(programKey)

  const { hasPermissionClaim } = useAuth()
  const canViewProgram = hasPermissionClaim('Permissions.Programs.View')

  useEffect(() => {
    if (!canViewProgram) {
      messageApi.error('You do not have permission to view programs.')
      onDrawerClose()
    }
  }, [canViewProgram, messageApi, onDrawerClose])

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading program data. Please try again.',
      )
    }
  }, [error, messageApi])

  const sponsorNames = getSortedNameList(programData?.programSponsors ?? [])

  const ownerNames = getSortedNameList(programData?.programOwners ?? [])

  const managerNames = getSortedNameList(programData?.programManagers ?? [])

  const strategicThemes = getSortedNameList(programData?.strategicThemes ?? [])

  return (
    <Drawer
      title={programData?.name ?? 'Program Details'}
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
            <Link href={`/ppm/programs/${programData?.key}`}>
              {programData?.key}
            </Link>
          </LabeledContent>
          <LabeledContent label="Status">
            {programData?.status.name}
          </LabeledContent>
          <LabeledContent label="Dates">
            <WaydDateRange
              dateRange={{ start: programData?.start, end: programData?.end }}
            />
          </LabeledContent>
          <LabeledContent label="Sponsors">
            <ContentList items={sponsorNames} emptyText="No sponsor assigned" />
          </LabeledContent>
          <LabeledContent label="Owners">
            <ContentList items={ownerNames} emptyText="No owner assigned" />
          </LabeledContent>
          <LabeledContent label="PMs" tooltip="Program Managers">
            <ContentList items={managerNames} emptyText="No PM assigned" />
          </LabeledContent>
          {strategicThemes.length > 0 && (
            <LabeledContent label="Strategic Themes">
              {strategicThemes.join(', ')}
            </LabeledContent>
          )}
          {programData?.description && (
            <LabeledContent label="Description">
              <ExpandableContent background="var(--ant-color-bg-elevated)">
                <MarkdownRenderer markdown={programData.description} />
              </ExpandableContent>
            </LabeledContent>
          )}
        </Flex>
        {programData?.id && (
          <LinksCard objectId={programData.id} width="100%" />
        )}
      </Flex>
    </Drawer>
  )
}

export default ProgramDrawer
