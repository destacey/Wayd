'use client'

import { MetricCard } from '@/src/components/common/metrics'
import {
  ProgramListDto,
  ProjectListDto,
  StrategicInitiativeListDto,
} from '@/src/services/wayd-api'
import { Card, Col, Flex, Row, theme } from 'antd'
import { ReactNode } from 'react'
import BreakdownPie from '../../../_components/breakdown-pie'
import ProjectsNeedingAttentionCard from '../../../_components/projects-needing-attention-card'
import {
  getHealthBreakdown,
  getStatusBreakdown,
  getThemeBreakdown,
  HEALTH_SCOPE_TOOLTIP,
} from '../../../_components/project-breakdowns'

/**
 * Which child collection the overview is reporting on.
 *
 * A tab per collection because the three have *different* status vocabularies
 * — a project is Approved(5) where an initiative is Approved(2), and a program
 * has no Approved at all. One shared filter above all three could only ever be
 * right for one of them, so each keeps its own inside its own tab.
 */
export enum OverviewTab {
  Programs = 'programs',
  Projects = 'projects',
  StrategicInitiatives = 'strategic-initiatives',
}

export interface PortfolioOverviewProps {
  activeTab: OverviewTab
  onTabChange: (tab: OverviewTab) => void
  /** The filter bar for the active tab, owned by the page. */
  filterBar: ReactNode

  programs: ProgramListDto[]
  programsLoading: boolean
  projects: ProjectListDto[]
  projectsLoading: boolean
  strategicInitiatives: StrategicInitiativeListDto[]
  strategicInitiativesLoading: boolean

  /**
   * Navigates to a section by id. Supplied by the page so section ids stay
   * defined in one place rather than being restated here.
   */
  onNavigateToSection: (sectionId: string) => void
}

const TAB_LIST = [
  { key: OverviewTab.Programs, tab: 'Programs' },
  { key: OverviewTab.Projects, tab: 'Projects' },
  { key: OverviewTab.StrategicInitiatives, tab: 'Strategic Initiatives' },
]

/**
 * What this portfolio holds, one child collection at a time.
 *
 * Every number reads from the same query the matching section renders, under
 * the same filter, so a tile can never disagree with the list it links to.
 */
const PortfolioOverview = ({
  activeTab,
  onTabChange,
  filterBar,
  programs,
  programsLoading,
  projects,
  projectsLoading,
  strategicInitiatives,
  strategicInitiativesLoading,
  onNavigateToSection,
}: PortfolioOverviewProps) => {
  const { token } = theme.useToken()

  const tabContent = (() => {
    switch (activeTab) {
      case OverviewTab.Programs:
        return {
          label: 'Programs',
          count: programs.length,
          isLoading: programsLoading,
          themes: getThemeBreakdown(programs),
          statuses: getStatusBreakdown(programs, token),
          // Only projects carry health checks.
          health: null,
          empty: 'No programs match the selected statuses.',
        }
      case OverviewTab.StrategicInitiatives:
        return {
          label: 'Strategic Initiatives',
          count: strategicInitiatives.length,
          isLoading: strategicInitiativesLoading,
          // Initiatives carry no strategic themes, so there is nothing to
          // break down by — the status chart takes the full width.
          themes: null,
          statuses: getStatusBreakdown(strategicInitiatives, token),
          health: null,
          empty: 'No strategic initiatives match the selected statuses.',
        }
      default:
        return {
          label: 'Projects',
          count: projects.length,
          isLoading: projectsLoading,
          themes: getThemeBreakdown(projects),
          statuses: getStatusBreakdown(projects, token),
          health: getHealthBreakdown(projects, token),
          empty: 'No projects match the selected statuses.',
        }
    }
  })()

  // Divide the row by however many charts this tab actually has, so a third
  // chart shares the row rather than wrapping onto one of its own at half
  // width. Three across only at xl; at lg two fit comfortably.
  const chartCount =
    1 + (tabContent.themes ? 1 : 0) + (tabContent.health ? 1 : 0)
  const chartSpanLg = chartCount === 1 ? 24 : 12
  const chartSpanXl = chartCount === 3 ? 8 : chartSpanLg

  return (
    <Card
      style={{ width: '100%' }}
      tabList={TAB_LIST}
      activeTabKey={activeTab}
      onTabChange={(key) => onTabChange(key as OverviewTab)}
    >
      <Flex vertical gap="large">
        {filterBar}

        <Row gutter={[16, 16]} align="stretch">
          <Col xs={12} sm={8} md={6}>
            <MetricCard
              title={tabContent.label}
              value={tabContent.isLoading ? '—' : tabContent.count}
              onClick={() => onNavigateToSection(activeTab)}
            />
          </Col>
        </Row>

        <Row gutter={[16, 16]} align="stretch">
          {tabContent.themes && (
            <Col xs={24} lg={chartSpanLg} xl={chartSpanXl}>
              <BreakdownPie
                title={`${tabContent.label} by Strategic Theme`}
                data={tabContent.themes}
                isLoading={tabContent.isLoading}
                emptyMessage={tabContent.empty}
              />
            </Col>
          )}
          <Col xs={24} lg={chartSpanLg} xl={chartSpanXl}>
            <BreakdownPie
              title={`${tabContent.label} by Status`}
              data={tabContent.statuses}
              isLoading={tabContent.isLoading}
              emptyMessage={tabContent.empty}
            />
          </Col>
          {tabContent.health && (
            <Col xs={24} lg={chartSpanLg} xl={chartSpanXl}>
              <BreakdownPie
                title={`${tabContent.label} by Health`}
                data={tabContent.health}
                isLoading={tabContent.isLoading}
                tooltip={HEALTH_SCOPE_TOOLTIP}
                emptyMessage="No open projects match the selected statuses."
              />
            </Col>
          )}
        </Row>

        {/* Only the Projects tab has health to act on. */}
        {activeTab === OverviewTab.Projects && (
          <ProjectsNeedingAttentionCard
            projects={projects}
            isLoading={projectsLoading}
          />
        )}
      </Flex>
    </Card>
  )
}

export default PortfolioOverview
