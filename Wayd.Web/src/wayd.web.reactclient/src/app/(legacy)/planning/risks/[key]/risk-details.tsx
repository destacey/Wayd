import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RiskDetailsDto } from '@/src/services/wayd-api'
import { Col, Descriptions, Row } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Item } = Descriptions

interface RiskDetailsProps {
  risk: RiskDetailsDto
}

const RiskDetails = ({ risk }: RiskDetailsProps) => {
  if (!risk) return null

  const teamLink =
    risk.team?.type === 'Team'
      ? `/organizations/teams/${risk.team?.key}`
      : `/organizations/team-of-teams/${risk.team?.key}`
  return (
    <>
      <Row>
        <Col xs={24} md={10}>
          <Descriptions layout="vertical">
            <Item label="Description">
              <MarkdownRenderer markdown={risk?.description} />
            </Item>
          </Descriptions>
          <Descriptions layout="vertical">
            <Item label="Response">
              <MarkdownRenderer markdown={risk?.response} />
            </Item>
          </Descriptions>
        </Col>
        <Col xs={24} md={14}>
          <Descriptions column={2}>
            <Item label="Status">{risk.status?.name}</Item>
            <Item label="Team">
              <Link href={teamLink}>{risk.team?.name}</Link>
            </Item>
            <Item label="Category">{risk.category?.name}</Item>
            <Item label="Follow-Up Date">
              {risk.followUpDate && dayjs(risk.followUpDate).format('M/D/YYYY')}
            </Item>
            <Item label="Assignee">
              <Link href={`/organizations/employees/${risk.assignee?.key}`}>
                {risk.assignee?.name}
              </Link>
            </Item>
            <Item label="Exposure">{risk.exposure?.name}</Item>
            <Item label="Impact">{risk.impact?.name}</Item>
            <Item label="Likelihood">{risk.likelihood?.name}</Item>
          </Descriptions>
        </Col>
      </Row>
      <Descriptions>
        <Item label="Reported By">
          <Link href={`/organizations/employees/${risk.reportedBy?.key}`}>
            {risk.reportedBy?.name}
          </Link>
        </Item>
        <Item label="Reported On">
          {risk.reportedOn && dayjs(risk.reportedOn).format('M/D/YYYY')}
        </Item>
      </Descriptions>
      <LinksCard objectId={risk.id} />
    </>
  )
}

export default RiskDetails
