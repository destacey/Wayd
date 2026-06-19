'use client'

import {
  AzureDevOpsWorkProcessDto,
  AzureDevOpsWorkspaceDto,
} from '@/src/services/wayd-api'
import { ExportOutlined } from '@ant-design/icons'
import { Button, Flex, Space, Typography } from 'antd'
import WaydList from '@/src/components/common/wayd-list'
import Link from 'next/link'
import { useContext, useState } from 'react'
import InitWorkProcessIntegrationForm from './init-work-process-integration-form'
import AzdoWorkspaceCard from './workspace-card'
import { AzdoConnectionContext } from './connection-context'
import { useGetWorkProcessQuery } from '@/src/store/features/work-management/work-process-api'

const { Title, Text } = Typography
const { Item } = WaydList

interface AzdoProcessProps {
  workProcess: AzureDevOpsWorkProcessDto
  workspaces: AzureDevOpsWorkspaceDto[]
}

const AzdoProcess = (props: AzdoProcessProps) => {
  const [
    openInitWorkProcessIntegrationForm,
    setOpenInitWorkProcessIntegrationForm,
  ] = useState<boolean>(false)

  const azdoConnection = useContext(AzdoConnectionContext)

  const skip = !props?.workProcess?.integrationState?.internalId
  const { data: workProcessData, isSuccess: workProcessLoaded } =
    useGetWorkProcessQuery(
      props.workProcess.integrationState?.internalId ?? '',
      { skip },
    )

  const integrationExists = !!props.workProcess.integrationState
  const processIntegrationIsActive =
    props.workProcess.integrationState?.isActive
  // The connection holds an IntegrationState pointer to a Wayd.Work.WorkProcess
  // that no longer exists. Sync Org Config will clear the dangling pointer.
  const integrationLinkIsStale =
    integrationExists && workProcessLoaded && !workProcessData

  const onInitWorkProcessIntegrationFormClosed = (wasSaved: boolean) => {
    setOpenInitWorkProcessIntegrationForm(false)
    if (wasSaved) {
      // TODO: make this a better experience
      azdoConnection?.reloadConnectionData
    }
  }

  const workspaceSection = (
    processWorkspaces: AzureDevOpsWorkspaceDto[],
    processIntegrationIsActive: boolean | undefined,
  ) => (
    <>
      <Title level={5}>Workspaces</Title>
      <WaydList
        grid={{
          gutter: 16,
          xs: 1,
          sm: 1,
          md: 1,
          lg: 2,
          xl: 3,
          xxl: 4,
        }}
        locale={{ emptyText: 'No workspaces found.' }}
        dataSource={processWorkspaces.sort((a, b) =>
          a.name.localeCompare(b.name),
        )}
        renderItem={(item) => (
          <Item>
            <AzdoWorkspaceCard
              workspace={item}
              enableInit={
                !(item.integrationState?.isActive ?? false) && (processIntegrationIsActive ?? false)
              }
            />
          </Item>
        )}
      />
    </>
  )

  const integrationContent = () => {
    if (!integrationExists) {
      return (
        <Button
          title="Setup Work Process Integration"
          onClick={() => setOpenInitWorkProcessIntegrationForm(true)}
        >
          Initialize
        </Button>
      )
    }

    if (integrationLinkIsStale) {
      return (
        <Text type="warning" title="Run Sync Org Config to clear the stale link, then re-initialize.">
          Integration link is stale
        </Text>
      )
    }

    if (workProcessData) {
      const integrationState = processIntegrationIsActive
        ? 'Active Integration'
        : 'Inactive Integration'

      return (
        <Link
          href={`/settings/work-management/work-processes/${workProcessData?.key}`}
        >
          {integrationState}
        </Link>
      )
    }
  }

  return (
    <>
      <Flex vertical>
        <Flex justify="space-between" gap="middle" wrap="wrap-reverse">
          <Flex vertical>
            <Flex gap="small" align="start">
              <Title level={5} style={{ marginTop: '0px' }}>
                {props.workProcess.name}
              </Title>
              {azdoConnection?.organizationUrl && (
                <Link
                  href={`${azdoConnection.organizationUrl}/_settings/process?process-name=${props.workProcess.name}&_a=workitemtypes`}
                  target="_blank"
                  title="Open in Azure DevOps"
                >
                  <ExportOutlined style={{ width: '12px' }} />
                </Link>
              )}
            </Flex>
          </Flex>
          <Space>{integrationContent()}</Space>
        </Flex>
        <Text>{props.workProcess.description}</Text>
        {workspaceSection(props.workspaces, processIntegrationIsActive)}
      </Flex>
      {openInitWorkProcessIntegrationForm && (
        <InitWorkProcessIntegrationForm
          connectionId={azdoConnection!.connectionId}
          externalId={props.workProcess.externalId}
          onFormSave={() => onInitWorkProcessIntegrationFormClosed(true)}
          onFormCancel={() => onInitWorkProcessIntegrationFormClosed(false)}
        />
      )}
    </>
  )
}

export default AzdoProcess
