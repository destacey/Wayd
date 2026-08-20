import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import StageTimeline from '../../_components/stage-timeline'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { useGetProjectPlanSummaryQuery } from '@/src/store/features/ppm/projects-api'
import { Card, Col, Flex, Row, Typography } from 'antd'
import { ProjectDetails } from '.'
import { projectHelpText } from './project-help-text'
import ProjectTaskMetrics from './project-task-metrics'
import { FC } from 'react'
import { WaydEmpty } from '@/src/components/common'

const { Title } = Typography

const isPreExecution = (statusName: string) =>
  statusName === 'Proposed' || statusName === 'Approved'

const { Text } = Typography

const NOT_PROVIDED = 'Not provided'

const ProjectJustification: FC<{ project: ProjectDetailsDto }> = ({
  project,
}) => (
  <>
    <Flex align="center" gap={8}>
      <Title level={4} style={{ margin: 0 }}>Project Definition</Title>
    </Flex>
    <Card size="small">
      <Flex vertical gap={10}>
        <LabeledContent
          label="Description"
          tooltip={projectHelpText.description}
        >
          {project.description ? (
            <ExpandableContent lines={8}>
              <MarkdownRenderer markdown={project.description} />
            </ExpandableContent>
          ) : (
            <Text type="secondary">{NOT_PROVIDED}</Text>
          )}
        </LabeledContent>
        <LabeledContent
          label="Business Case"
          tooltip={projectHelpText.businessCase}
        >
          {project.businessCase ? (
            <ExpandableContent lines={8}>
              <MarkdownRenderer markdown={project.businessCase} />
            </ExpandableContent>
          ) : (
            <Text type="secondary">{NOT_PROVIDED}</Text>
          )}
        </LabeledContent>
        <LabeledContent
          label="Expected Benefits"
          tooltip={projectHelpText.expectedBenefits}
        >
          {project.expectedBenefits ? (
            <ExpandableContent lines={8}>
              <MarkdownRenderer markdown={project.expectedBenefits} />
            </ExpandableContent>
          ) : (
            <Text type="secondary">{NOT_PROVIDED}</Text>
          )}
        </LabeledContent>
      </Flex>
    </Card>
  </>
)

export interface ProjectDetailsTabProps {
  project: ProjectDetailsDto
}

const ProjectDetailsTab: FC<ProjectDetailsTabProps> = ({ project }) => {
  const { data: planSummary } = useGetProjectPlanSummaryQuery(
    { projectKey: project?.key },
    { skip: !project?.stages?.length },
  )
  const hasTasks = (planSummary?.totalLeafTasks ?? 0) > 0

  if (!project) return null

  const preExecution = isPreExecution(project.status.name)

  const executionContent = (
    <>
      {project.stages?.length > 0 && (
        <Flex vertical gap="large">
          <Flex align="center" gap={8}>
            <Title level={4} style={{ margin: 0 }}>Stages</Title>
          </Flex>
          <StageTimeline stages={project.stages} />
          {hasTasks && (
            <>
              <Flex align="center" gap={8}>
                <Title level={4} style={{ margin: 0 }}>Task Summary</Title>
              </Flex>
              <ProjectTaskMetrics projectKey={project.key} />
            </>
          )}
        </Flex>
      )}
      {!project.projectLifecycle && (
        <WaydEmpty message="No lifecycle defined for this project." />
      )}
    </>
  )

  const justificationContent = <ProjectJustification project={project} />

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} md={9} xxl={6}>
        <ProjectDetails project={project} />
      </Col>
      <Col xs={24} md={15} xxl={18}>
        <Flex vertical gap="large">
          {preExecution ? (
            <>
              {justificationContent}
              {executionContent}
            </>
          ) : (
            <>
              {executionContent}
              {justificationContent}
            </>
          )}
        </Flex>
      </Col>
    </Row>
  )
}

export default ProjectDetailsTab
