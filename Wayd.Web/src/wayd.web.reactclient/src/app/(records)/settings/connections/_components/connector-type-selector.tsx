import {
  ConnectorType,
  CAPABILITY_CATEGORY_DESCRIPTIONS,
  CAPABILITY_CATEGORY_ORDER,
  CONNECTOR_DESCRIPTIONS,
  CONNECTOR_NAMES,
  getCapabilityCategories,
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
 * Group a list of connectors by capability category. A connector can appear in more than one
 * category when its capabilities span several.
 */
const groupByCategory = <T extends { categories: string[] }>(items: T[]) => {
  const grouped = new Map<string, T[]>()
  for (const item of items) {
    for (const category of item.categories) {
      const list = grouped.get(category) ?? []
      list.push(item)
      grouped.set(category, list)
    }
  }
  return grouped
}

interface ConnectorOption {
  type: ConnectorType
  name: string
  description: string
  categories: string[]
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

  const connectedCategories = new Set(
    (connections ?? []).filter((c) => c.isActive).flatMap(getCapabilityCategories),
  )

  const options: ConnectorOption[] = connectors.map((c) => ({
      type: c.id as ConnectorType,
      // Prefer the frontend's friendlier name + description when we have one, so we control wording.
      name: CONNECTOR_NAMES[c.id as ConnectorType] ?? c.name,
      description:
        CONNECTOR_DESCRIPTIONS[c.id as ConnectorType] ?? c.description ?? '',
      categories: getCapabilityCategories(c),
      isConnected: false, // resolved per-card below if there's an active connection for the type
    }))

  const grouped = groupByCategory(options)

  // Sort categories using our preferred display order; any unrecognized goes at the end.
  const orderedCategories = [
    ...CAPABILITY_CATEGORY_ORDER.filter((cat) => grouped.has(cat)),
    ...[...grouped.keys()].filter(
      (cat) => !CAPABILITY_CATEGORY_ORDER.includes(cat),
    ),
  ]

  return (
    <>
      <Title level={4} style={{ marginBottom: 16 }}>
        Select Connector Type
      </Title>

      {orderedCategories.map((category, index) => {
        const categoryConnectors = (grouped.get(category) ?? []).sort(
          (a, b) => a.name.localeCompare(b.name),
        )
        const categoryHasActive = connectedCategories.has(category)
        const categoryLabel = category
        const categoryDescription =
          CAPABILITY_CATEGORY_DESCRIPTIONS[category] ?? ''

        return (
          <div
            key={category}
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
