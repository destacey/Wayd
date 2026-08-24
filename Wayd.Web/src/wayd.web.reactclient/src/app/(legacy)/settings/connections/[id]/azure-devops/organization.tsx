'use client'

import {
  AzureDevOpsWorkProcessDto,
  AzureDevOpsWorkspaceDto,
} from '@/src/services/wayd-api'
import { Alert, Space, Tabs, Typography } from 'antd'
import AzdoProcess from './process'

const { Text, Title } = Typography

interface AzdoOrganizationProps {
  workProcesses: AzureDevOpsWorkProcessDto[]
  workspaces: AzureDevOpsWorkspaceDto[]
}

const AzdoOrganization = (props: AzdoOrganizationProps) => {
  if (!props.workProcesses || !props.workspaces) return null

  const getProcessWorkspaces = (process: AzureDevOpsWorkProcessDto) => {
    return props.workspaces.filter(
      (x) => x.workProcessId === process.externalId,
    )
  }

  return (
    <>
      <Space vertical size="large" style={{ display: 'flex' }}>
        <Text>
          The work processes and workspaces below represent processes and
          projects pulled from Azure DevOps.
        </Text>
        {props.workProcesses.length === 0 ? (
          <Alert
            title="No work processes were found for this connection."
            type="error"
          />
        ) : (
          <>
            <Title level={5}>Work Processes</Title>
            <Tabs
              tabPlacement="start"
              items={props.workProcesses
                .slice() // array is likely in strict mode. clone the array to avoid sorting the original
                .sort((a, b) => a.name.localeCompare(b.name))
                .map((p) => {
                  return {
                    label: p.name,
                    key: p.externalId,
                    children: (
                      <AzdoProcess
                        workProcess={p}
                        workspaces={getProcessWorkspaces(p)}
                      />
                    ),
                  }
                })}
            />
          </>
        )}
      </Space>
    </>
  )
}

export default AzdoOrganization
