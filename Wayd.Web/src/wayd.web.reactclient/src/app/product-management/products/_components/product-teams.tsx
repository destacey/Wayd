'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  renderProductLink,
  renderTeamLink,
} from '@/src/components/common/wayd-grid-core'
import WaydList from '@/src/components/common/wayd-list'
import {
  ProductTeamLinkDto,
  ProductTeamNavigationDto,
  TeamType,
} from '@/src/services/wayd-api'
import { Card, Empty, Flex, Spin, Tag, Typography } from 'antd'
import { Fragment } from 'react'
import styles from './product-teams.module.css'

const { Title, Text } = Typography

export interface ProductTeamsProps {
  productId: string
  /** The product's own teams, from the product record. */
  teams: ProductTeamNavigationDto[]
  /** The product's rolled-up links, of which those on products beneath it are listed. */
  links: ProductTeamLinkDto[] | undefined
  isLoading: boolean
}

/** A team linked to products beneath the page's product, with the products it is on. */
export interface TeamBeneath {
  team: ProductTeamNavigationDto
  products: ProductTeamLinkDto['product'][]
}

/**
 * Groups the links on products beneath the page's product by team, so each team is listed once with the
 * products it is on — by team name, then product name.
 */
export const teamsBeneath = (
  productId: string,
  links: ProductTeamLinkDto[],
): TeamBeneath[] => {
  const byTeam = new Map<string, TeamBeneath>()

  for (const link of links) {
    if (link.product.id === productId) continue
    const entry = byTeam.get(link.team.id) ?? { team: link.team, products: [] }
    entry.products.push(link.product)
    byTeam.set(link.team.id, entry)
  }

  return [...byTeam.values()]
    .map((entry) => ({
      ...entry,
      products: [...entry.products].sort((a, b) =>
        caseInsensitiveCompare(a.name, b.name),
      ),
    }))
    .sort((a, b) => caseInsensitiveCompare(a.team.name, b.team.name))
}

/**
 * How many distinct teams the section shows: the product's own and those beneath it, a team in both counted once.
 */
export const productTeamCount = (
  teams: ProductTeamNavigationDto[],
  links: ProductTeamLinkDto[] | undefined,
): number =>
  new Set([...teams.map((t) => t.id), ...(links ?? []).map((l) => l.team.id)])
    .size

const TeamCard = ({ team }: { team: ProductTeamNavigationDto }) => (
  <Card size="small" className={styles.card}>
    <Flex vertical gap={4}>
      <Text strong>{renderTeamLink(team)}</Text>
      <Flex gap={4} wrap>
        <Tag>
          {team.type === TeamType.TeamOfTeams ? 'Team of Teams' : 'Team'}
        </Tag>
        {!team.isActive && <Tag>Inactive</Tag>}
      </Flex>
    </Flex>
  </Card>
)

/**
 * The teams on a product: its own as cards, which Manage Teams changes, and those linked to the products
 * beneath it as a read-only list naming the products each is on.
 */
const ProductTeams = ({
  productId,
  teams,
  links,
  isLoading,
}: ProductTeamsProps) => {
  const own = [...teams].sort((a, b) => caseInsensitiveCompare(a.name, b.name))
  const beneath = teamsBeneath(productId, links ?? [])

  return (
    <Flex vertical gap="middle">
      {own.length > 0 ? (
        <Flex gap="small" wrap>
          {own.map((team) => (
            <TeamCard key={team.id} team={team} />
          ))}
        </Flex>
      ) : (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description="No team is linked to this product."
        />
      )}

      {isLoading ? (
        <Spin />
      ) : (
        beneath.length > 0 && (
          <div>
            <Title level={5}>Teams on products beneath</Title>
            <WaydList
              size="small"
              dataSource={beneath}
              renderItem={({ team, products }) => (
                <WaydList.Item>
                  <Flex gap="small" wrap>
                    <Text strong>{renderTeamLink(team)}</Text>
                    <Text type="secondary">
                      {products.map((product, index) => (
                        <Fragment key={product.id}>
                          {index > 0 && ', '}
                          {renderProductLink(product)}
                        </Fragment>
                      ))}
                    </Text>
                  </Flex>
                </WaydList.Item>
              )}
            />
          </div>
        )
      )}
    </Flex>
  )
}

export default ProductTeams
