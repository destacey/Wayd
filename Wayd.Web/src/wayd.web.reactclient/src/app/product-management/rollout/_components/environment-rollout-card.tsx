'use client'

import {
  EnvironmentCategory,
  EnvironmentRolloutDto,
  RolloutItemDto,
} from '@/src/services/wayd-api'
import { WarningOutlined } from '@ant-design/icons'
import { Card, Empty, Flex, Space, Tag, Tooltip, Typography } from 'antd'
import Link from 'next/link'

const { Text } = Typography

/**
 * The category's preset tag colour. Preset names rather than literals so both themes resolve them.
 *
 * Production is the only one the delivery measures count, so it is the only one given a colour that
 * reads as significant; the rest stay quiet so a wall of cards does not compete for attention.
 */
const categoryColor: Record<EnvironmentCategory, string> = {
  [EnvironmentCategory.Development]: 'default',
  [EnvironmentCategory.Testing]: 'blue',
  [EnvironmentCategory.Staging]: 'gold',
  [EnvironmentCategory.Production]: 'green',
  [EnvironmentCategory.Other]: 'default',
}

/**
 * One product and the version of it that is live.
 *
 * Every row is a product, however the version got here. Where it arrived inside a package, the
 * package is named as provenance rather than as a second thing running.
 */
const RunningItem = ({ item }: { item: RolloutItemDto }) => (
  <Flex align="baseline" gap={8} wrap>
    <Text>{item.product.name}</Text>
    <Link href={`/product-management/deployments/${item.deploymentKey}`}>
      <Tag>{item.versionLabel}</Tag>
    </Link>
    {item.package && (
      <Tooltip title="Shipped inside this package, so the package is the deployment the version arrived on.">
        <Text type="secondary" style={{ fontSize: 12 }}>
          in {item.package.name}
        </Text>
      </Tooltip>
    )}
    {item.artifactId && (
      <Text type="secondary" style={{ fontSize: 12 }}>
        {item.artifactId}
      </Text>
    )}
    {item.hasFailedAttemptSince && (
      <Tooltip title="A later attempt here failed or was rolled back. This version is still what is running.">
        <WarningOutlined aria-label="A later attempt failed" />
      </Tooltip>
    )}
  </Flex>
)

export interface EnvironmentRolloutCardProps {
  environment: EnvironmentRolloutDto
}

/**
 * One environment and everything live in it.
 *
 * An empty card is a real answer — nothing has ever succeeded here — rather than missing data, so it
 * says so instead of being left out of the page.
 */
const EnvironmentRolloutCard = ({ environment }: EnvironmentRolloutCardProps) => (
  <Card
    size="small"
    title={
      <Flex align="center" gap={8} wrap>
        <Text strong>{environment.name}</Text>
        <Tag color={categoryColor[environment.category]}>
          {environment.category}
        </Tag>
        {!environment.isActive && <Tag>Retired</Tag>}
      </Flex>
    }
    extra={
      <Tooltip title="Position in the rollout order. Environments sharing a ring go out together.">
        <Text type="secondary">Ring {environment.ringOrder}</Text>
      </Tooltip>
    }
  >
    {environment.running.length === 0 ? (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description="Nothing running"
        styles={{ image: { height: 32 } }}
      />
    ) : (
      <Space orientation="vertical" size={4} style={{ width: '100%' }}>
        {/* Keyed on the product, not the deployment: one package deployment expands into a row per
            component, so several entries legitimately share a deployment id. */}
        {environment.running.map((item) => (
          <RunningItem key={item.product.id} item={item} />
        ))}
      </Space>
    )}
  </Card>
)

export default EnvironmentRolloutCard
