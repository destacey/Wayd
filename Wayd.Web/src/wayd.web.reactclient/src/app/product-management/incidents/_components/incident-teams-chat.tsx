'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useLinkIncidentTeamsChatMutation } from '@/src/store/features/product-management/incidents-api'
import { IncidentDto } from '@/src/services/wayd-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { CloseOutlined, EditOutlined } from '@ant-design/icons'
import { Alert, Button, Divider, Flex, Form, Input, Modal, Tabs, Typography } from 'antd'
import type { FormInstance } from 'antd'
import type { BaseOptionType } from 'antd/es/select'
import { useState } from 'react'
import styles from './incident-teams-chat.module.css'

const { Text, Link } = Typography

export interface IncidentTeamsChatProps {
  incident: IncidentDto
  canUpdate: boolean
  employees: BaseOptionType[]
}

/**
 * The incident's one Teams chat. Hidden unless a Teams connector is configured.
 * Linking stores a pasted link, and an empty link removes it. Creating a chat is
 * shown and does not call Graph.
 */
const IncidentTeamsChat = ({
  incident,
  canUpdate,
  employees,
}: IncidentTeamsChatProps) => {
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState(false)
  const [removing, setRemoving] = useState(false)
  const [extraPeople, setExtraPeople] = useState<string[]>([])
  const [form] = Form.useForm<{ link: string; displayName?: string }>()
  const [editForm] = Form.useForm<{ link: string; displayName?: string }>()
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

  const storeLink = async (
    values: { link: string; displayName?: string },
    target: FormInstance<{ link: string; displayName?: string }>,
    done: () => void,
    success: string,
  ) => {
    try {
      await linkChat({
        id: incident.id,
        link: values.link,
        displayName: values.displayName,
      }).unwrap()
      messageApi.success(success)
      done()
    } catch (error) {
      const apiError = toApiError(error)
      if (apiError.errors) {
        target.setFields(toFormErrors(apiError.errors))
      }
      messageApi.error(apiError.detail ?? 'Could not link the Teams chat.')
    }
  }

  const removeChat = async () => {
    try {
      await linkChat({ id: incident.id, link: '' }).unwrap()
      messageApi.success('Teams chat removed.')
      setRemoving(false)
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ?? 'Could not remove the Teams chat.',
      )
    }
  }

  return (
    <Flex vertical gap={8}>
      {incident.teamsChatLink ? (
        <Flex align="center" gap={8} className={styles.row}>
          <TeamsMark />
          <Link
            className={styles.name}
            href={incident.teamsChatLink}
            target="_blank"
            rel="noopener noreferrer"
          >
            {chatName}
          </Link>
          {canUpdate ? (
            <Flex gap={4} className={styles.actions}>
              <Button
                type="text"
                size="small"
                className={styles.action}
                aria-label="Edit Teams chat"
                icon={<EditOutlined />}
                onClick={() => setEditing(true)}
              />
              <Button
                type="text"
                size="small"
                className={styles.action}
                aria-label="Remove Teams chat"
                icon={<CloseOutlined />}
                onClick={() => setRemoving(true)}
              />
            </Flex>
          ) : null}
        </Flex>
      ) : null}
      {canUpdate && !incident.teamsChatLink ? (
        <Button icon={<TeamsMark />} onClick={() => setOpen(true)}>
          Add Teams chat for this incident
        </Button>
      ) : null}
      <Modal
        title={
          <span className={styles.title}>
            <TeamsMark />
            <span>Teams chat</span>
          </span>
        }
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
                    style={{ width: '100%' }}
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
                  onFinish={(values) =>
                    storeLink(values, form, close, 'Teams chat linked.')
                  }
                >
                  <img
                    src="/images/teams-copy-link-annotated.png"
                    alt="Teams menu with Copy link under Mute"
                    width={340}
                    height={374}
                    className={styles.menuShot}
                  />
                  <Text className={styles.copySteps}>
                    Open the group chat. The three dots are at the top right. Copy link
                    is in that menu, under Mute.
                  </Text>
                  <Form.Item
                    name="link"
                    label="Teams link"
                    rules={[
                      { required: true, message: 'Paste the Teams link.' },
                      {
                        validator: (_, value: string | undefined) =>
                          !value || value.trim().toLowerCase().startsWith('https://')
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
      <Modal
        title="Edit Teams chat"
        open={editing}
        onCancel={() => setEditing(false)}
        footer={null}
        destroyOnHidden
      >
        <Form
          form={editForm}
          layout="vertical"
          requiredMark={false}
          initialValues={{
            link: incident.teamsChatLink ?? '',
            displayName: incident.teamsChatDisplayName ?? undefined,
          }}
          onFinish={(values) =>
            storeLink(values, editForm, () => setEditing(false), 'Teams chat updated.')
          }
        >
          <Form.Item
            name="link"
            label="Teams link"
            rules={[
              { required: true, message: 'Paste the Teams link.' },
              {
                validator: (_, value: string | undefined) =>
                  !value || value.trim().toLowerCase().startsWith('https://')
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
            Save
          </Button>
        </Form>
      </Modal>
      <Modal
        title="Remove Teams chat"
        open={removing}
        onCancel={() => setRemoving(false)}
        okText="Remove"
        okButtonProps={{ danger: true, loading: isLoading }}
        onOk={() => void removeChat()}
      >
        This removes the stored link. The chat in Teams is left as it is.
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
    <img
      className={styles.mark}
      src="/icons/teams-logo.svg"
      alt=""
      width={16}
      height={16}
    />
  )
}

export default IncidentTeamsChat
