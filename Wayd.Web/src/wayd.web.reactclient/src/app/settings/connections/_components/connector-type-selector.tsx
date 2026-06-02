import {
  ConnectorCategory,
  ConnectorType,
  CONNECTOR_CATEGORY_DESCRIPTIONS,
  CONNECTOR_CATEGORY_LABELS,
  CONNECTOR_CATEGORY_ORDER,
  CONNECTOR_DESCRIPTIONS,
  CONNECTOR_NAMES,
} from '@/src/types/connectors'
import {
  useGetConnectionsQuery,
  useGetConnectorsQuery,
} from '@/src/store/features/app-integration/connections-api'
import { Card, Col, Row, Skeleton, Tag, Typography } from 'antd'

const { Title, Text } = Typography

interface ConnectorTypeSelectorProps {
  onSelect: (type: ConnectorType) => void
}

/**
 * Group a list of connectors by category id. The id is the backend ConnectorCategory enum value.
 */
const groupByCategory = <T extends { categoryId: number }>(items: T[]) => {
  const grouped = new Map<number, T[]>()
  for (const item of items) {
    const list = grouped.get(item.categoryId) ?? []
    list.push(item)
    grouped.set(item.categoryId, list)
  }
  return grouped
}

interface ConnectorOption {
  type: ConnectorType
  name: string
  description: string
  categoryId: ConnectorCategory
  isConnected: boolean
}

export const ConnectorTypeSelector: React.FC<ConnectorTypeSelectorProps> = ({
  onSelect,
}) => {
  const { data: connectors, isLoading: connectorsLoading } =
    useGetConnectorsQuery()
  // Pull the existing connections to mark which categories already have something connected.
  const { data: connections } = useGetConnectionsQuery(true)

  if (connectorsLoading || !connectors) {
    return (
      <>
        <Title level={4} style={{ marginBottom: 16 }}>
          Select Connector Type
        </Title>
        <Skeleton active />
      </>
    )
  }

  const connectedCategoryIds = new Set(
    (connections ?? [])
      .filter((c) => c.isActive)
      .map((c) => c.category?.id)
      .filter((id): id is number => typeof id === 'number'),
  )

  const options: ConnectorOption[] = connectors
    // OpenAI ships as a known enum value but no Create/Update support yet — hide it.
    .filter((c) => c.id !== ConnectorType.OpenAI)
    .map((c) => ({
      type: c.id as ConnectorType,
      // Prefer the frontend's friendlier name + description when we have one, so we control wording.
      name: CONNECTOR_NAMES[c.id as ConnectorType] ?? c.name,
      description:
        CONNECTOR_DESCRIPTIONS[c.id as ConnectorType] ?? c.description ?? '',
      categoryId: (c.category?.id as ConnectorCategory) ?? ConnectorCategory.Unknown,
      isConnected: false, // resolved per-card below if there's an active connection for the type
    }))

  const grouped = groupByCategory(options)

  // Sort categories using our preferred display order; any uncategorized goes at the end.
  const orderedCategories = [
    ...CONNECTOR_CATEGORY_ORDER.filter((cat) => grouped.has(cat)),
    ...[...grouped.keys()].filter(
      (cat) => !CONNECTOR_CATEGORY_ORDER.includes(cat as ConnectorCategory),
    ),
  ]

  return (
    <>
      <Title level={4} style={{ marginBottom: 16 }}>
        Select Connector Type
      </Title>

      {orderedCategories.map((categoryId, index) => {
        const categoryConnectors = (grouped.get(categoryId) ?? []).sort(
          (a, b) => a.name.localeCompare(b.name),
        )
        const categoryHasActive = connectedCategoryIds.has(categoryId)
        const categoryLabel =
          CONNECTOR_CATEGORY_LABELS[categoryId as ConnectorCategory] ?? 'Other'
        const categoryDescription =
          CONNECTOR_CATEGORY_DESCRIPTIONS[categoryId as ConnectorCategory] ?? ''

        return (
          <div
            key={categoryId}
            style={{ marginBottom: index === orderedCategories.length - 1 ? 0 : 24 }}
          >
            <div style={{ marginBottom: 8 }}>
              <Title level={5} style={{ marginTop: 0, marginBottom: 4 }}>
                {categoryLabel}
                {categoryHasActive && (
                  <Tag color="success" style={{ marginLeft: 8, fontWeight: 400 }}>
                    Connected
                  </Tag>
                )}
              </Title>
              {categoryDescription && (
                <Text type="secondary" style={{ fontSize: 13 }}>
                  {categoryDescription}
                </Text>
              )}
            </div>
            <Row gutter={[16, 16]}>
              {categoryConnectors.map((option) => (
                <Col key={option.type} span={12}>
                  <Card
                    hoverable
                    onClick={() => onSelect(option.type)}
                    style={{ cursor: 'pointer', height: '100%' }}
                    styles={{ body: { padding: 16 } }}
                  >
                    <Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
                      {option.name}
                    </Title>
                    <Text type="secondary" style={{ fontSize: 13 }}>
                      {option.description}
                    </Text>
                  </Card>
                </Col>
              ))}
            </Row>
          </div>
        )
      })}
    </>
  )
}
