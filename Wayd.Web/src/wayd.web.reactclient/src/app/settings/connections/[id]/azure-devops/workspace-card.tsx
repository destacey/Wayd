'use client'

import { AzureDevOpsWorkspaceDto } from '@/src/services/wayd-api'
import { AppstoreAddOutlined, ExportOutlined } from '@ant-design/icons'
import { Button, Card, Descriptions, Typography } from 'antd'
import Link from 'next/link'
import { useContext, useState } from 'react'
import InitWorkspaceIntegrationForm from './init-workspace-integration-form'
import { AzdoConnectionContext } from './connection-context'
import MapAzdoWorkspaceTeamsForm from './map-workspace-teams-form'

const { Item } = Descriptions
const { Text } = Typography

interface AzdoWorkspaceCardProps {
  workspace: AzureDevOpsWorkspaceDto
  enableInit: boolean
}

const AzdoWorkspaceCard = (props: AzdoWorkspaceCardProps) => {
  const [
    openInitWorkspaceIntegrationForm,
    setOpenInitWorkspaceIntegrationForm,
  ] = useState<boolean>(false)
  const [openMapAzdoWorkspaceTeamsForm, setOpenMapAzdoWorkspaceTeamsForm] =
    useState<boolean>(false)

  const azdoConnection = useContext(AzdoConnectionContext)

  return (
    <>
      <Card
        data-testid={props.workspace.externalId}
        size="small"
        title={
          <>
            {props.workspace.name}{' '}
            {azdoConnection?.organizationUrl && (
              <Link
                href={`${azdoConnection.organizationUrl}/${props.workspace.name}`}
                target="_blank"
                title="Open in Azure DevOps"
              >
                <ExportOutlined style={{ width: '12px' }} />
              </Link>
            )}
          </>
        }
        extra={
          props.enableInit && (
            <Button
              type="text"
              title="Setup Workspace Integration"
              icon={<AppstoreAddOutlined />}
              onClick={() => setOpenInitWorkspaceIntegrationForm(true)}
            />
          )
        }
      >
        <Descriptions column={1} size="small">
          <Item>
            {props.workspace.description ?? (
              <Text type="secondary" italic>
                No description provided.
              </Text>
            )}
          </Item>
        </Descriptions>
        {!props.enableInit && (
          <Button
            size="small"
            onClick={() => setOpenMapAzdoWorkspaceTeamsForm(true)}
          >
            Team Mappings
          </Button>
        )}
      </Card>
      {openInitWorkspaceIntegrationForm && (
        <InitWorkspaceIntegrationForm
          connectionId={azdoConnection!.connectionId}
          externalId={props.workspace.externalId}
          workspaceName={props.workspace.name}
          onFormSave={() => setOpenInitWorkspaceIntegrationForm(false)}
          onFormCancel={() => setOpenInitWorkspaceIntegrationForm(false)}
        />
      )}
      {openMapAzdoWorkspaceTeamsForm && (
        <MapAzdoWorkspaceTeamsForm
          connectionId={azdoConnection!.connectionId}
          workspaceId={props.workspace.externalId}
          workspaceName={props.workspace.name}
          onFormSave={() => setOpenMapAzdoWorkspaceTeamsForm(false)}
          onFormCancel={() => setOpenMapAzdoWorkspaceTeamsForm(false)}
        />
      )}
    </>
  )
}

export default AzdoWorkspaceCard
