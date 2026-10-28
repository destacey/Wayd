'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import useAuth from '@/src/components/contexts/auth'
import {
  CreateIncidentSeveritySetRequest,
  IncidentSeveritySetDto,
  UpdateIncidentSeveritySetRequest,
} from '@/src/services/wayd-api'
import {
  useCreateIncidentSeveritySetMutation,
  useDeleteIncidentSeveritySetMutation,
  useSetIncidentSeveritySetActiveStatusMutation,
  useUpdateIncidentSeveritySetMutation,
} from '@/src/store/features/product-management/incident-severity-sets-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Button, Form, Input, Modal, Space, Typography } from 'antd'
import { useState } from 'react'

type Props = {
  open: boolean
  onClose: () => void
  onCreated: () => void
}

export function CreateIncidentSeveritySetForm({ open, onClose, onCreated }: Props) {
  const [form] = Form.useForm<CreateIncidentSeveritySetRequest>()
  const messageApi = useMessage()
  const [create, { isLoading }] = useCreateIncidentSeveritySetMutation()

  return (
    <Modal
      title="New severity set"
      open={open}
      onCancel={onClose}
      onOk={() => form.submit()}
      confirmLoading={isLoading}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ levelNames: ['', ''] }}
        onFinish={async (values) => {
          try {
            await create({
              ...values,
              levelNames: values.levelNames.filter((n) => n.trim()),
            }).unwrap()
            messageApi.success('Severity set created.')
            form.resetFields()
            onCreated()
          } catch (error) {
            const apiError = toApiError(error)
            if (apiError.errors) {
              form.setFields(toFormErrors(apiError.errors))
            } else {
              messageApi.error(apiError.detail ?? 'Could not create severity set')
            }
          }
        }}
      >
        <Form.Item name="name" label="Name" rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="description" label="Description">
          <Input.TextArea rows={2} />
        </Form.Item>
        <Typography.Text type="secondary">
          Enter levels from most severe to least severe.
        </Typography.Text>
        <Form.List name="levelNames">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Form.Item
                  {...field}
                  key={field.key}
                  rules={[{ required: true, message: 'Level name is required' }]}
                >
                  <Input
                    placeholder="Level name"
                    addonAfter={
                      fields.length > 2 ? (
                        <Button type="link" onClick={() => remove(field.name)}>
                          Remove
                        </Button>
                      ) : null
                    }
                  />
                </Form.Item>
              ))}
              <Button type="dashed" onClick={() => add('')} block>
                Add level
              </Button>
            </>
          )}
        </Form.List>
      </Form>
    </Modal>
  )
}

type PanelProps = {
  set: IncidentSeveritySetDto
  onChanged: () => void
  onDeleted: () => void
}

export function IncidentSeveritySetPanel({ set, onChanged, onDeleted }: PanelProps) {
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.IncidentSeveritySets.Update')
  const canDelete = hasPermissionClaim('Permissions.IncidentSeveritySets.Delete')

  const [update, { isLoading: saving }] = useUpdateIncidentSeveritySetMutation()
  const [setActive] = useSetIncidentSeveritySetActiveStatusMutation()
  const [deleteSet, { isLoading: deleting }] = useDeleteIncidentSeveritySetMutation()

  const [editedNames, setEditedNames] = useState(() =>
    set.levels.map((l) => l.name),
  )

  const saveLevels = async () => {
    if (set.isSystem) {
      return
    }

    const request: UpdateIncidentSeveritySetRequest = {
      id: set.id,
      name: set.name,
      description: set.description,
      levels: set.levels.map((level, index) => ({
        id: level.id,
        name: (editedNames[index] ?? level.name).trim(),
      })),
    }

    try {
      await update(request).unwrap()
      messageApi.success('Severity set updated.')
      onChanged()
    } catch (error) {
      messageApi.error(toApiError(error).detail ?? 'Could not update severity set')
    }
  }

  return (
    <Space direction="vertical" style={{ width: '100%' }} size="middle">
      <Typography.Title level={5}>{set.name}</Typography.Title>
      {set.description ? <Typography.Paragraph>{set.description}</Typography.Paragraph> : null}
      <Typography.Text>
        Levels (most severe first): {set.levels.map((l) => l.name).join(' → ')}
      </Typography.Text>

      {!set.isSystem && canUpdate ? (
        <>
          {editedNames.map((name, index) => (
            <Input
              key={set.levels[index]?.id ?? index}
              value={name}
              style={{ marginBottom: 8 }}
              onChange={(e) => {
                const next = [...editedNames]
                next[index] = e.target.value
                setEditedNames(next)
              }}
            />
          ))}
          <Button type="primary" loading={saving} onClick={() => void saveLevels()}>
            Save levels
          </Button>
        </>
      ) : null}

      {canUpdate && !set.isSystem ? (
        <Button
          onClick={() =>
            void setActive({ id: set.id, isActive: !set.isActive })
              .unwrap()
              .then(onChanged)
              .catch((err) =>
                messageApi.error(toApiError(err).detail ?? 'Could not change status'),
              )
          }
        >
          {set.isActive ? 'Deactivate' : 'Activate'}
        </Button>
      ) : null}

      {canDelete && !set.isSystem && !set.isSelected ? (
        <Button
          danger
          loading={deleting}
          onClick={() =>
            void deleteSet(set.id)
              .unwrap()
              .then(onDeleted)
              .catch((err) =>
                messageApi.error(toApiError(err).detail ?? 'Could not delete severity set'),
              )
          }
        >
          Delete set
        </Button>
      ) : null}
    </Space>
  )
}
