'use client'

import { useGetProjectHealthChecksQuery } from '@/src/store/features/ppm/project-health-checks-api'
import HealthStatusHistoryChart from '@/src/components/common/health-check/health-status-history-chart'
import ProjectHealthReportGrid from './project-health-report-grid'
import { Flex } from 'antd'

interface ProjectHealthReportProps {
  projectId: string
}

const ProjectHealthReport = ({ projectId }: ProjectHealthReportProps) => {
  const { data, isLoading, refetch } = useGetProjectHealthChecksQuery(
    { projectId },
    { skip: !projectId },
  )

  return (
    <Flex vertical gap="middle">
      <HealthStatusHistoryChart
        data={data}
        isLoading={isLoading}
        cardStyle={{ width: 375 }}
      />
      <ProjectHealthReportGrid
        data={data}
        isLoading={isLoading}
        refetch={refetch}
      />
    </Flex>
  )
}

export default ProjectHealthReport

