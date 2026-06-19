'use client'

import { WorkspaceListDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import { ReactElement } from 'react'
import { WorkspaceCard } from '.'
import { WaydEmpty, WaydList } from '@/src/components/common'

const { Item: ListItem } = WaydList

export interface WorkspacesCardGridProps {
  workspaces: WorkspaceListDto[]
  viewSelector: ReactElement
  isLoading: boolean
}

const gridConfig = {
  gutter: 16,
  xs: 1,
  sm: 2,
  md: 3,
  lg: 3,
  xl: 4,
  xxl: 4,
}

const WorkspacesCardGrid: React.FC<WorkspacesCardGridProps> = (
  props: WorkspacesCardGridProps,
) => {
  const sortedWorkspaces: WorkspaceListDto[] = (() => {
    if (!props.workspaces || props.workspaces.length === 0) {
      return []
    }
    return [...props.workspaces].sort((a, b) => a.name.localeCompare(b.name))
  })()

  return (
    <>
      <Flex justify="end" align="center" style={{ paddingBottom: '16px' }}>
        {props.viewSelector}
      </Flex>
      <WaydList
        grid={gridConfig}
        loading={{
          spinning: props.isLoading,
          description: 'Loading workspaces...',
          size: 'large',
        }}
        locale={{ emptyText: <WaydEmpty message="No workspaces found" /> }}
        dataSource={sortedWorkspaces}
        renderItem={(item: WorkspaceListDto) => (
          <ListItem>
            <WorkspaceCard workspace={item} />
          </ListItem>
        )}
      />
    </>
  )
}

export default WorkspacesCardGrid
