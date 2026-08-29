'use client'

import { MetricCard } from '@/src/components/common/metrics'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { ProgramDetailsDto, ProjectListDto } from '@/src/services/wayd-api'
import { Card, Col, Flex, Row, theme } from 'antd'
import BreakdownPie from '../../../_components/breakdown-pie'
import ProjectsNeedingAttentionCard from '../../../_components/projects-needing-attention-card'
import {
  getHealthBreakdown,
  getStatusBreakdown,
  getThemeBreakdown,
  HEALTH_SCOPE_TOOLTIP,
} from '../../../_components/project-breakdowns'

export interface ProgramOverviewProps {
  program: ProgramDetailsDto
  /** The filtered project set the Projects section renders, so counts agree. */
  projects: ProjectListDto[]
  projectsLoading: boolean
  /**
   * Navigates to a section by id. Supplied by the page so section ids stay
   * defined in one place rather than being restated here.
   */
  onNavigateToSection: (sectionId: string) => void
}

/**
 * Where the program stands, at a glance.
 *
 * Everything here reads from the same filtered query the Projects section
 * renders, so a tile can never disagree with the list it links to.
 */
const ProgramOverview = ({
  program,
  projects,
  projectsLoading,
  onNavigateToSection,
}: ProgramOverviewProps) => {
  const { token } = theme.useToken()

  const themeBreakdown = getThemeBreakdown(projects)
  const statusBreakdown = getStatusBreakdown(projects, token)
  const healthBreakdown = getHealthBreakdown(projects, token)

  const timelineFormat =
    program.start &&
    program.end &&
    new Date(program.start).getFullYear() === new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <Flex vertical gap="large">
      <Row gutter={[16, 16]} align="stretch">
        <Col xs={12} sm={12} md={6}>
          <MetricCard
            title="Projects"
            value={projects.length}
            loading={projectsLoading}
            onClick={() => onNavigateToSection('projects')}
          />
        </Col>
      </Row>

      {/* Undated programs have nothing to plot, and TimelineProgress renders
          nothing for them — the card would be an empty box. */}
      {program.start && program.end && (
        <Card size="small">
          <TimelineProgress
            start={program.start}
            end={program.end}
            variant="borderless"
            style={{ width: '100%' }}
            dateFormat={timelineFormat}
          />
        </Card>
      )}

      <Row gutter={[16, 16]} align="stretch">
        <Col xs={24} lg={12} xl={8}>
          <BreakdownPie
            title="Projects by Strategic Theme"
            data={themeBreakdown}
            isLoading={projectsLoading}
            emptyMessage="No projects match the selected statuses."
          />
        </Col>
        <Col xs={24} lg={12} xl={8}>
          <BreakdownPie
            title="Projects by Status"
            data={statusBreakdown}
            isLoading={projectsLoading}
            emptyMessage="No projects match the selected statuses."
          />
        </Col>
        <Col xs={24} lg={12} xl={8}>
          <BreakdownPie
            title="Projects by Health"
            data={healthBreakdown}
            isLoading={projectsLoading}
            tooltip={HEALTH_SCOPE_TOOLTIP}
            emptyMessage="No open projects match the selected statuses."
          />
        </Col>
      </Row>

      {/* Every project here shares this program, so the program row on each
          card would repeat a constant. */}
      <ProjectsNeedingAttentionCard
        projects={projects}
        isLoading={projectsLoading}
        hideProgram
      />
    </Flex>
  )
}

export default ProgramOverview
