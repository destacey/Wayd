'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import useAuth from '@/src/components/contexts/auth'
import WaydColorPicker from '@/src/components/common/wayd-color-picker'
import {
  CreateIncidentSeveritySetRequest,
  IncidentSeveritySetDto,
  IncidentSeveritySetLevelRequest,
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
import { useEffect, useState } from 'react'
import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'

const DEFAULT_LEVEL_COLORS = ['#E07A4C', '#D4A017', '#7FA3BF', '#8E9E98']

type LevelFormValue = IncidentSeveritySetLevelRequest

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
        initialValues={{
          levels: [
            { name: '', color: DEFAULT_LEVEL_COLORS[0] },
            { name: '', color: DEFAULT_LEVEL_COLORS[1] },
          ],
        }}
        onFinish={async (values) => {
          try {
            await create({
              ...values,
              levels: values.levels.filter((l) => l.name.trim()),
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
        <Form.List name="levels">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field, index) => (
                <Space.Compact
                  key={field.key}
                  block
                  style={{ width: '100%', marginBottom: 8, marginTop: 8 }}
                >
                  <Form.Item
                    name={[field.name, 'name']}
                    noStyle
                    rules={[{ required: true, message: 'Level name is required' }]}
                  >
                    <Input placeholder="Level name" style={{ flex: 1 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'color']} noStyle>
                    <WaydColorPicker />
                  </Form.Item>
                  {fields.length > 2 ? (
                    <Button type="link" onClick={() => remove(field.name)}>
                      Remove
                    </Button>
                  ) : null}
                </Space.Compact>
              ))}
              <Button
                type="dashed"
                onClick={() =>
                  add({
                    name: '',
                    color:
                      DEFAULT_LEVEL_COLORS[
                        Math.min(fields.length, DEFAULT_LEVEL_COLORS.length - 1)
                      ],
                  })
                }
                block
              >
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

  const [editedLevels, setEditedLevels] = useState<LevelFormValue[]>(() =>
    set.levels.map((l) => ({ id: l.id, name: l.name, color: l.color })),
  )

  useEffect(() => {
    setEditedLevels(
      set.levels.map((l) => ({ id: l.id, name: l.name, color: l.color })),
    )
  }, [set])

  const saveLevels = async () => {
    if (set.isSystem) {
      return
    }

    const request: UpdateIncidentSeveritySetRequest = {
      id: set.id,
      name: set.name,
      description: set.description,
      levels: editedLevels.map((level, index) => ({
        id: set.levels[index]?.id ?? level.id,
        name: level.name.trim(),
        color: level.color,
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
      <Space direction="vertical" size={4}>
        {set.levels.map((level) => (
          <IncidentSeverityRank
            key={level.id}
            name={level.name}
            color={level.color}
          />
        ))}
      </Space>

      {!set.isSystem && canUpdate ? (
        <>
          {editedLevels.map((level, index) => (
            <Space.Compact key={set.levels[index]?.id ?? index} block style={{ width: '100%' }}>
              <Input
                value={level.name}
                style={{ flex: 1 }}
                onChange={(e) => {
                  const next = [...editedLevels]
                  next[index] = { ...next[index]!, name: e.target.value }
                  setEditedLevels(next)
                }}
              />
              <WaydColorPicker
                value={level.color}
                onChange={(color) => {
                  const next = [...editedLevels]
                  next[index] = { ...next[index]!, color: color ?? level.color }
                  setEditedLevels(next)
                }}
              />
            </Space.Compact>
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
          Delete
        </Button>
      ) : null}
    </Space>
  )
}
