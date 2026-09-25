'use client'

import BreakdownPie from '@/src/app/ppm/_components/breakdown-pie'
import {
  getHealthBreakdown,
  getStatusBreakdown,
  getThemeBreakdown,
  HEALTH_SCOPE_TOOLTIP,
} from '@/src/app/ppm/_components/project-breakdowns'
import { ProjectListDto } from '@/src/services/wayd-api'
import { Col, Collapse, Row, theme } from 'antd'
import { FC } from 'react'
import styles from '../projects-dashboard.module.css'

export interface BreakdownStripProps {
  projects: ProjectListDto[]
  isLoading: boolean
  expanded: boolean
  onExpandedChange: (expanded: boolean) => void
}

const EMPTY = 'No projects match the selected statuses.'

/**
 * The three project breakdowns from the portfolio and program overviews, for
 * a scope wide enough to make them mean something. Collapsed by default so
 * the attention tiles and the list stay above the fold; the choice sticks.
 */
const BreakdownStrip: FC<BreakdownStripProps> = ({
  projects,
  isLoading,
  expanded,
  onExpandedChange,
}) => {
  const { token } = theme.useToken()

  return (
    <Collapse
      className={styles.breakdowns}
      size="small"
      activeKey={expanded ? ['breakdowns'] : []}
      onChange={(keys) => onExpandedChange(keys.includes('breakdowns'))}
      items={[
        {
          key: 'breakdowns',
          label: 'Breakdowns',
          children: (
            <Row gutter={[16, 16]} align="stretch">
              <Col xs={24} lg={8}>
                <BreakdownPie
                  title="Projects by Status"
                  data={getStatusBreakdown(projects, token)}
                  isLoading={isLoading}
                  emptyMessage={EMPTY}
                />
              </Col>
              <Col xs={24} lg={8}>
                <BreakdownPie
                  title="Projects by Health"
                  data={getHealthBreakdown(projects, token)}
                  isLoading={isLoading}
                  tooltip={HEALTH_SCOPE_TOOLTIP}
                  emptyMessage="No open projects match the selected statuses."
                />
              </Col>
              <Col xs={24} lg={8}>
                <BreakdownPie
                  title="Projects by Strategic Theme"
                  data={getThemeBreakdown(projects)}
                  isLoading={isLoading}
                  emptyMessage={EMPTY}
                />
              </Col>
            </Row>
          ),
        },
      ]}
    />
  )
}

export default BreakdownStrip
