'use client'

import { WaydEmpty } from '@/src/components/common'
import { ProjectCard } from '@/src/app/ppm/_components/projects-card-view'
import { ProjectListDto } from '@/src/services/wayd-api'
import ProjectDrawer from '@/src/app/ppm/_components/project-drawer'
import { Card, Col, Row, Segmented } from 'antd'
import { useMemo, useState } from 'react'
import {
  AttentionSortMode,
  getProjectsNeedingAttention,
} from './projects-needing-attention'

export interface ProjectsNeedingAttentionCardProps {
  projects: ProjectListDto[]
  isLoading?: boolean
  /**
   * Hides the portfolio row on each card, and the by-program sort. True on a
   * program record, where both would repeat a constant.
   */
  hideProgram?: boolean
}

/**
 * The projects somebody should look at: those flagged At Risk or Unhealthy.
 *
 * A shortlist rather than another view of the whole set — the charts above
 * report the shape of the portfolio, and this names the projects to act on.
 * Closed projects are excluded, as they are from the health chart.
 *
 * Renders the same card as the Projects section rather than a lighter copy, so
 * a project looks the same wherever it is seen.
 */
const ProjectsNeedingAttentionCard = ({
  projects,
  isLoading = false,
  hideProgram = false,
}: ProjectsNeedingAttentionCardProps) => {
  const [sortMode, setSortMode] = useState<AttentionSortMode>('health')
  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )
  const [drawerOpen, setDrawerOpen] = useState(false)

  // The same two targets the Projects section offers: the card opens the
  // drawer for a quick look, and the name link inside it goes to the record.
  // The card stops propagation on that link, so the two do not fight.
  const onCardClick = (key: string) => {
    setSelectedProjectKey(key)
    setDrawerOpen(true)
  }

  const attentionProjects = useMemo(
    () => getProjectsNeedingAttention(projects, sortMode),
    [projects, sortMode],
  )

  if (isLoading) {
    return <Card size="small" loading title="Needs Attention — Projects" />
  }

  // Sorting by program where every project shares one is a control that does
  // nothing.
  const sortOptions = [
    { label: 'by health', value: 'health' },
    { label: 'by name', value: 'name' },
    { label: 'by rank', value: 'rank' },
    ...(hideProgram ? [] : [{ label: 'by program', value: 'program' }]),
  ]

  return (
    <>
      <Card
        size="small"
        title="Needs Attention — Projects"
        extra={
          <Segmented
            size="small"
            value={sortMode}
            onChange={(value) => setSortMode(value as AttentionSortMode)}
            options={sortOptions}
          />
        }
        styles={{ body: { padding: 8 } }}
      >
        {attentionProjects.length === 0 ? (
          <WaydEmpty message="No projects need attention" />
        ) : (
          <Row gutter={[12, 12]}>
            {attentionProjects.map((project) => (
              <Col xs={24} md={12} xl={8} key={project.id}>
                <ProjectCard
                  project={project}
                  onCardClick={onCardClick}
                  // The record above already names the portfolio, and on a
                  // program every card would repeat the same program.
                  hidePortfolio
                  hideProgram={hideProgram}
                />
              </Col>
            ))}
          </Row>
        )}
      </Card>
      {selectedProjectKey && (
        <ProjectDrawer
          projectKey={selectedProjectKey}
          drawerOpen={drawerOpen}
          onDrawerClose={() => {
            setDrawerOpen(false)
            setSelectedProjectKey(null)
          }}
        />
      )}
    </>
  )
}

export default ProjectsNeedingAttentionCard
