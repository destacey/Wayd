import EditRiskForm from '@/src/components/common/planning/edit-risk-form'
import { RiskListDto } from '@/src/services/wayd-api'
import { EditOutlined } from '@ant-design/icons'
import { Button, Card, Space, Tag, Typography } from 'antd'
import WaydList from '@/src/components/common/wayd-list'
import Link from 'next/link'
import { useState } from 'react'

const { Item } = WaydList
const { Meta } = Item
const { Text } = Typography

export interface RiskListItemProps {
  risk: RiskListDto
  canUpdateRisks: boolean
  refreshRisks: () => void
}

const RiskListItem = ({
  risk,
  canUpdateRisks,
  refreshRisks,
}: RiskListItemProps) => {
  const [openUpdateRiskForm, setOpenUpdateRiskForm] = useState<boolean>(false)

  const title = () => {
    return (
      <Link href={`/planning/risks/${risk.key}`}>
        {risk.key} - {risk.summary}
      </Link>
    )
  }
  const description = () => {
    const content = `Exposure: ${risk.exposure}`
    const assigneeInfo = risk.assignee ? (
      <>
        {' | Assignee: '}
        <Link href={`/organizations/employees/${risk.assignee.key}`}>
          {risk.assignee.name}
        </Link>
      </>
    ) : null

    return (
      <Space wrap>
        <Tag>{risk.category}</Tag>
        <Text>
          {content}
          {assigneeInfo}
        </Text>
      </Space>
    )
  }

  const onEditRiskFormClosed = (wasSaved: boolean) => {
    setOpenUpdateRiskForm(false)
    if (wasSaved) {
      refreshRisks()
    }
  }

  return (
    <>
      <Card
        size="small"
        style={{ marginBottom: 4 }}
        styles={{ body: { padding: 0 } }}
      >
        <Item key={risk.key}>
          <Meta title={title()} description={description()} />
          {canUpdateRisks && (
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              onClick={() => setOpenUpdateRiskForm(true)}
            />
          )}
        </Item>
      </Card>
      {openUpdateRiskForm && (
        <EditRiskForm
          riskKey={risk.key}
          onFormSave={() => onEditRiskFormClosed(true)}
          onFormCancel={() => onEditRiskFormClosed(false)}
        />
      )}
    </>
  )
}

export default RiskListItem
