'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useLinkIncidentTeamsChatMutation } from '@/src/store/features/product-management/incidents-api'
import { IncidentDto } from '@/src/services/wayd-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Button, Divider, Flex, Form, Input, Modal, Tabs, Typography } from 'antd'
import type { BaseOptionType } from 'antd/es/select'
import { useState } from 'react'

const { Text, Link } = Typography

export interface IncidentTeamsChatProps {
  incident: IncidentDto
  canUpdate: boolean
  employees: BaseOptionType[]
}

/**
 * The incident's one Teams chat. Hidden unless a Teams connector is configured.
 * Linking stores a pasted link. Creating a chat is shown and does not call Graph.
 */
const IncidentTeamsChat = ({
  incident,
  canUpdate,
  employees,
}: IncidentTeamsChatProps) => {
  const [open, setOpen] = useState(false)
  const [extraPeople, setExtraPeople] = useState<string[]>([])
  const [form] = Form.useForm<{ link: string; displayName?: string }>()
  const messageApi = useMessage()
  const [linkChat, { isLoading }] = useLinkIncidentTeamsChatMutation()

  if (
    !incident.teamsConnectorConfigured ||
    (!incident.teamsChatLink && !canUpdate)
  ) {
    return null
  }

  const chatName = incident.teamsChatDisplayName || 'Teams chat'
  const createName = `INC-${incident.key} ${incident.title}`
  const preselected = preselectedPeople(incident)

  const close = () => {
    setOpen(false)
    setExtraPeople([])
    form.resetFields()
  }

  const storeLink = async (values: { link: string; displayName?: string }) => {
    try {
      await linkChat({
        id: incident.id,
        link: values.link,
        displayName: values.displayName,
      }).unwrap()
      messageApi.success('Teams chat linked.')
      close()
    } catch (error) {
      const apiError = toApiError(error)
      if (apiError.errors) {
        form.setFields(toFormErrors(apiError.errors))
      }
      messageApi.error(apiError.detail ?? 'Could not link the Teams chat.')
    }
  }

  return (
    <Flex vertical gap={8}>
      {incident.teamsChatLink ? (
        <Flex align="center" gap={8}>
          <TeamsMark />
          <Link href={incident.teamsChatLink} target="_blank" rel="noreferrer">
            {chatName}
          </Link>
        </Flex>
      ) : null}
      {canUpdate ? (
        <Button icon={<TeamsMark />} onClick={() => setOpen(true)}>
          {incident.teamsChatLink
            ? 'Change Teams chat'
            : 'Add Teams chat for this incident'}
        </Button>
      ) : null}
      <Modal
        title="Teams chat"
        open={open}
        onCancel={close}
        footer={null}
        destroyOnHidden
      >
        <Tabs
          items={[
            {
              key: 'create',
              label: 'Create',
              children: (
                <Flex vertical gap={12}>
                  <Text>The chat name is {createName}.</Text>
                  <Text type="secondary">
                    Reported by, submitted by, and the commander are included
                    {preselected.length > 0
                      ? `: ${preselected.join(', ')}`
                      : '.'}
                  </Text>
                  <EmployeeSelect
                    employees={employees}
                    allowMultiple
                    placeholder="Add other people for this chat only"
                    value={extraPeople}
                    onChange={(value) =>
                      setExtraPeople(Array.isArray(value) ? value : [value])
                    }
                  />
                  <Alert
                    type="info"
                    showIcon
                    title="Creating a Teams chat from Wayd is not available yet."
                    description="The chat-approval write is not in place, so this does not create a chat or call Microsoft Graph. People added here are not stored."
                  />
                </Flex>
              ),
            },
            {
              key: 'link',
              label: 'Link existing',
              children: (
                <Form
                  form={form}
                  layout="vertical"
                  requiredMark={false}
                  onFinish={storeLink}
                >
                  <Form.Item
                    name="link"
                    label="Teams link"
                    rules={[
                      { required: true, message: 'Paste the Teams link.' },
                      {
                        validator: (_, value: string | undefined) =>
                          !value || value.trim().startsWith('https://')
                            ? Promise.resolve()
                            : Promise.reject(
                                new Error('The link must start with https://'),
                              ),
                      },
                    ]}
                  >
                    <Input placeholder="https://teams.microsoft.com/..." />
                  </Form.Item>
                  <Form.Item name="displayName" label="Name">
                    <Input placeholder="Optional name shown beside the link" />
                  </Form.Item>
                  <Button type="primary" htmlType="submit" loading={isLoading}>
                    Link chat
                  </Button>
                </Form>
              ),
            },
          ]}
        />
      </Modal>
      <Divider size="small" style={{ margin: 0 }} />
    </Flex>
  )
}

function preselectedPeople(incident: IncidentDto) {
  const people = [
    incident.submittedBy,
    incident.reportedBy,
    incident.commander,
  ].filter((person) => person != null)
  const seen = new Set<string>()
  return people.flatMap((person) => {
    if (seen.has(person.id)) {
      return []
    }
    seen.add(person.id)
    return [person.name]
  })
}

function TeamsMark() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
      <rect width="16" height="16" rx="3" fill="currentColor" />
      <path
        fill="var(--ant-color-bg-container)"
        d="M3.2 5.1h4.2v1.3H6.1V11H4.6V6.4H3.2V5.1Zm5 0h2.4c1 0 1.7.6 1.7 1.6 0 .7-.4 1.2-1 1.4.8.2 1.3.8 1.3 1.6 0 1.1-.8 1.8-2 1.8H8.2V5.1Zm1.5 2.2h.8c.4 0 .7-.2.7-.6s-.3-.6-.7-.6h-.8v1.2Zm0 1.1v1.4h1c.5 0 .8-.3.8-.7s-.3-.7-.8-.7h-1Z"
      />
    </svg>
  )
}

export default IncidentTeamsChat
