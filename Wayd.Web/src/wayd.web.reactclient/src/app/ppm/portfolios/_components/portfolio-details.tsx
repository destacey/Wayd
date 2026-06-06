'use client'

import { ResponsiveFlex } from '@/src/components/common'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { ProjectPortfolioDetailsDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import { Descriptions, Flex } from 'antd'
import Link from 'next/link'

const { Item } = Descriptions

export interface PortfolioDetailsProps {
  portfolio: ProjectPortfolioDetailsDto
}

const PortfolioDetails: React.FC<PortfolioDetailsProps> = ({
  portfolio,
}: PortfolioDetailsProps) => {
  if (!portfolio) return null

  const sponsorNames =
    portfolio?.portfolioSponsors.length > 0
      ? getSortedNames(portfolio.portfolioSponsors)
      : 'No sponsor assigned'
  const ownerNames =
    portfolio?.portfolioOwners.length > 0
      ? getSortedNames(portfolio.portfolioOwners)
      : 'No owner assigned'
  const managerNames =
    portfolio?.portfolioManagers.length > 0
      ? getSortedNames(portfolio.portfolioManagers)
      : 'No manager assigned'

  return (
    <Flex vertical gap="middle">
      <ResponsiveFlex gap="middle" align="start">
        <Descriptions column={1} size="small">
          <Item label="Sponsors">{sponsorNames}</Item>
          <Item label="Owners">{ownerNames}</Item>
          <Item label="PMs">{managerNames}</Item>
          {portfolio.scoringModel && (
            <Item label="Scoring Model">
              <Link
                href={`/settings/scoring/scoring-models/${portfolio.scoringModel.key}`}
              >
                {portfolio.scoringModel.name}
              </Link>
            </Item>
          )}
        </Descriptions>
        <Descriptions layout="vertical" size="small">
          <Item label="Description">
            <MarkdownRenderer markdown={portfolio.description} />
          </Item>
        </Descriptions>
      </ResponsiveFlex>
      <LinksCard objectId={portfolio.id} />
    </Flex>
  )
}

export default PortfolioDetails
