'use client'

import { Col, Divider, Row } from 'antd'
import { useState } from 'react'
import ActivePlanningIntervals from '../components/common/planning/active-planning-intervals'
import MyAssignedRisks from '../components/common/planning/my-assigned-risks'
import MyTeamSprints from '../components/common/planning/my-team-sprints'
import MyProjectsCard from './ppm/dashboards/my-projects/_components/my-projects-card'
import { useDocumentTitle } from '../hooks/use-document-title'

const HomePage = () => {
  useDocumentTitle('Home')

  const [hasTeamSprints, setHasTeamSprints] = useState(false)

  // TODO: have these load after the page is loaded
  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={hasTeamSprints ? 16 : 24}>
        <ActivePlanningIntervals />
        <Divider />
        <Row gutter={[16, 16]}>
          <Col xs={24} md={12}>
            <MyProjectsCard />
          </Col>
          <Col xs={24} md={12}>
            <MyAssignedRisks />
          </Col>
        </Row>
      </Col>
      <Col xs={hasTeamSprints ? 24 : 0} lg={hasTeamSprints ? 8 : 0}>
        <MyTeamSprints onHasSprintsChange={setHasTeamSprints} />
      </Col>
    </Row>
  )
}

export default HomePage
