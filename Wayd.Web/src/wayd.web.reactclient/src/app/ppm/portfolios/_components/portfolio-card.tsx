'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { ProjectPortfolioListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import { Card, Descriptions, Tag } from 'antd'
import { useRouter } from 'next/navigation'

const { Item } = Descriptions

export interface PortfolioCardProps {
  portfolio: ProjectPortfolioListDto
}

const PortfolioCard: React.FC<PortfolioCardProps> = ({
  portfolio,
}: PortfolioCardProps) => {
  const router = useRouter()

  return (
    <Card
      title={portfolio.name}
      size="small"
      style={{ width: '100%' }}
      hoverable
      onClick={() => router.push(`/ppm/portfolios/${portfolio.key}`)}
    >
      <Descriptions column={1} size="small">
        <Item label="Key">{portfolio.key}</Item>
        {portfolio?.portfolioOwners.length > 0 && (
          <Item label="Owners">
            {getSortedNames(portfolio.portfolioOwners)}
          </Item>
        )}
        {portfolio?.portfolioManagers.length > 0 && (
          <Item label="PMs">{getSortedNames(portfolio.portfolioManagers)}</Item>
        )}
        <Item>
          <MarkdownRenderer markdown={portfolio.description} />
        </Item>
      </Descriptions>
      {portfolio?.status.name && <Tag>{portfolio?.status.name}</Tag>}
    </Card>
  )
}

export default PortfolioCard
