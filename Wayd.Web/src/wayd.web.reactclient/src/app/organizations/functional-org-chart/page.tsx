'use client'

import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useState } from 'react'
import { PageTitle } from '@/src/components/common'
import { useGetFunctionalOrganizationChartQuery } from '@/src/store/features/organizations/team-api'
import {
  OrganizationalUnitDto,
  FunctionalOrganizationChartDto,
} from '@/src/services/wayd-api'
import { DatePicker, Spin } from 'antd'
import {
  WaydOrganizationChart,
  OrganizationChartEdgeData,
  OrganizationChartGraphData,
  OrganizationChartNodeData,
} from '@/src/components/common/organization-chart'
import { OrganizationalChartTeamNode } from '../_components'
import dayjs from 'dayjs'

export type OrganizationalUnitWithoutId = Omit<
  OrganizationalUnitDto,
  'id' | 'children'
>
export type OrganizationalUnitRecord = Record<
  string,
  OrganizationalUnitWithoutId
>

// Type alias for your specific use case
type TeamOrganizationGraphData = OrganizationChartGraphData<
  OrganizationalUnitWithoutId,
  Record<string, any>,
  Record<string, any>
>

function transformOrganizationToGraph(
  orgData: FunctionalOrganizationChartDto,
): TeamOrganizationGraphData {
  if (!orgData.organization) {
    return { nodes: [], edges: [] }
  }

  const nodes: OrganizationChartNodeData<OrganizationalUnitWithoutId>[] = []
  const edges: OrganizationChartEdgeData[] = []
  const visitedNodeIds = new Set<string>()
  const edgeKeys = new Set<string>()

  function processUnit(unit: OrganizationalUnitDto) {
    if (!unit.id) return
    if (visitedNodeIds.has(unit.id)) return
    visitedNodeIds.add(unit.id)

    const nodeData: OrganizationChartNodeData<OrganizationalUnitWithoutId> = {
      id: unit.id,
      data: {
        key: unit.key,
        name: unit.name,
        code: unit.code,
        type: unit.type,
        level: unit.level,
        path: unit.path,
      },
    }
    nodes.push(nodeData)

    if (unit.children && unit.children.length > 0) {
      unit.children.forEach((child) => {
        if (child.id) {
          if (child.id === unit.id) return

          const edgeKey = `${unit.id}->${child.id}`
          if (edgeKeys.has(edgeKey)) return
          edgeKeys.add(edgeKey)

          const edge: OrganizationChartEdgeData = {
            id: `${unit.id}-${child.id}`,
            source: unit.id!,
            target: child.id,
            sourcePort: '1',
            targetPort: '0',
          }
          edges.push(edge)
          processUnit(child)
        }
      })
    }
  }

  orgData.organization.forEach(processUnit)
  return { nodes, edges }
}

const FunctionalOrgChartPage: React.FC = () => {
  const [asOfDate, setAsOfDate] = useState<dayjs.Dayjs | null>(dayjs())

  useDocumentTitle('Functional Org Chart')

  const {
    data: orgChartData,
    isLoading,
    isError,
  } = useGetFunctionalOrganizationChartQuery(asOfDate?.toISOString(), {
    skip: !asOfDate,
  })

  const data: TeamOrganizationGraphData = orgChartData
    ? transformOrganizationToGraph(orgChartData)
    : { nodes: [], edges: [] }

  if (isError) {
    return (
      <>
        <br />
        <PageTitle title="Functional Org Chart" />
        <div>Error loading org chart. Please try again later.</div>
      </>
    )
  }

  const actions = () => {
    return (
      <>
        <DatePicker
          value={asOfDate}
          placeholder="As of date"
          onChange={setAsOfDate}
          title="The as of date for the functional org chart."
        />
      </>
    )
  }

  return (
    <div className="page-gutters">
      <br />
      <PageTitle title="Functional Org Chart" actions={actions()} />
      <Spin
        spinning={isLoading}
        description="Loading functional org chart..."
        size="large"
      >
        {!isLoading && (
          <WaydOrganizationChart
            data={data}
            NodeComponent={OrganizationalChartTeamNode}
          />
        )}
      </Spin>
    </div>
  )
}

const FunctionalOrgChartPageWithAuthorization = authorizePage(
  FunctionalOrgChartPage,
  'Permission',
  'Permissions.Teams.View',
)

export default FunctionalOrgChartPageWithAuthorization
