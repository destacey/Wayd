'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ContentList,
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordFactsGroup } from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetProgramQuery } from '@/src/store/features/ppm/programs-api'
import { getDrawerWidthPixels, isApiError } from '@/src/utils'
import { Divider, Drawer, Flex } from 'antd'
import Link from 'next/link'
import RecordRoleList from '@/src/app/ppm/_components/record-role-list'
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

  const strategicThemeNames = [...(programData?.strategicThemes ?? [])]
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .map((t) => t.name)

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
          {strategicThemeNames.length > 0 && (
            <LabeledContent label="Strategic Themes">
              <ContentList items={strategicThemeNames} />
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

        <Divider size="small" style={{ margin: 0 }} />

        <RecordFactsGroup label="Roles">
          <LabeledContent label="Sponsors">
            <RecordRoleList
              people={programData?.programSponsors ?? []}
              emptyText="No sponsor assigned"
            />
          </LabeledContent>
          <LabeledContent label="Owners">
            <RecordRoleList
              people={programData?.programOwners ?? []}
              emptyText="No owner assigned"
            />
          </LabeledContent>
          <LabeledContent label="PMs" tooltip="Program Managers">
            <RecordRoleList
              people={programData?.programManagers ?? []}
              emptyText="No PM assigned"
            />
          </LabeledContent>
        </RecordFactsGroup>

        {programData?.portfolio && (
          <>
            <Divider size="small" style={{ margin: 0 }} />
            <RecordFactsGroup label="Relationships">
              <LabeledContent label="Portfolio">
                <Link href={`/ppm/portfolios/${programData.portfolio.key}`}>
                  {programData.portfolio.name}
                </Link>
              </LabeledContent>
            </RecordFactsGroup>
          </>
        )}

        {programData?.id && (
          <>
            <Divider size="small" style={{ margin: 0 }} />
            <LinksCard objectId={programData.id} width="100%" />
          </>
        )}
      </Flex>
    </Drawer>
  )
}

export default ProgramDrawer
