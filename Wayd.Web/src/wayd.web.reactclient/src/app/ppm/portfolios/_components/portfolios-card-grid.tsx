'use client'

import { ProjectPortfolioListDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import { ReactElement } from 'react'
import PortfolioCard from './portfolio-card'
import { WaydEmpty, WaydList } from '@/src/components/common'

const { Item: ListItem } = WaydList

export interface PortfoliosCardGridProps {
  portfolios: ProjectPortfolioListDto[]
  viewSelector: ReactElement
  isLoading: boolean
}

const gridConfig = {
  gutter: 16,
  xs: 1,
  sm: 1,
  md: 2,
  lg: 2,
  xl: 3,
  xxl: 4,
}

const PortfoliosCardGrid: React.FC<PortfoliosCardGridProps> = (
  props: PortfoliosCardGridProps,
) => {
  const sortedPortfolios: ProjectPortfolioListDto[] = (() => {
    if (!props.portfolios || props.portfolios.length === 0) {
      return []
    }
    const activePortfolios = props.portfolios.filter(
      (portfolio) =>
        portfolio.status.name === 'Active' ||
        portfolio.status.name === 'On Hold',
    )
    return activePortfolios.sort((a, b) => a.name.localeCompare(b.name))
  })()

  return (
    <>
      <Flex justify="end" align="center" style={{ paddingBottom: '16px' }}>
        {props.viewSelector}
      </Flex>
      <WaydList
        grid={gridConfig}
        loading={{
          spinning: props.isLoading,
          description: 'Loading portfolios...',
          size: 'large',
        }}
        locale={{
          emptyText: <WaydEmpty message="No active portfolios found" />,
        }}
        dataSource={sortedPortfolios}
        renderItem={(item: ProjectPortfolioListDto) => (
          <ListItem>
            <PortfolioCard portfolio={item} />
          </ListItem>
        )}
      />
    </>
  )
}

export default PortfoliosCardGrid
