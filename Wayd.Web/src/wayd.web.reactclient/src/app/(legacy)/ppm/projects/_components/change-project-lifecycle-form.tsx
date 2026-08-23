'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ProjectDetailsDto,
  ProjectLifecycleState,
  ProjectStageListDto,
} from '@/src/services/wayd-api'
import { getProjectsClient } from '@/src/services/clients'
import {
  useGetProjectLifecycleQuery,
  useGetProjectLifecyclesQuery,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { useChangeProjectLifecycleMutation } from '@/src/store/features/ppm/projects-api'
import { toFormErrors } from '@/src/utils'
import {
  Card,
  Flex,
  Form,
  Modal,
  Select,
  Table,
  Timeline,
  Typography,
} from 'antd'
import { useEffect, useState } from 'react'

const { Item } = Form
const { Text } = Typography

export interface ChangeProjectLifecycleFormProps {
  project: ProjectDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProjectLifecycleFormValues {
  lifecycleId: string
  stageMapping: Record<string, string>
}

const ChangeProjectLifecycleForm = ({
  project,
  onFormComplete,
  onFormCancel,
}: ChangeProjectLifecycleFormProps) => {
  const messageApi = useMessage()
  const [changeProjectLifecycle] = useChangeProjectLifecycleMutation()
  const [currentStages, setCurrentStages] = useState<ProjectStageListDto[]>([])

  // Load current project stages
  useEffect(() => {
    if (!project?.id) return
    getProjectsClient()
      .getProjectStages(project.id)
      .then((stages) => setCurrentStages(stages ?? []))
      .catch(() => setCurrentStages([]))
  }, [project?.id])

  // Load active lifecycles (exclude current)
  const { data: lifecycleData, isLoading: lifecyclesLoading } =
    useGetProjectLifecyclesQuery(ProjectLifecycleState.Active)

  const lifecycleOptions = !lifecycleData ? [] : [...lifecycleData]
      .filter((lc) => lc.id !== project?.projectLifecycle?.id)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((lc) => ({
        label: lc.name,
        value: lc.id,
      }))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProjectLifecycleFormValues>({
      onSubmit: async (values: ChangeProjectLifecycleFormValues, form) => {
          try {
            // Build stageMapping from the form's mapping fields
            const stageMapping: Record<string, string> = {}
            for (const stage of currentStages) {
              const targetId = values.stageMapping?.[stage.id]
              if (targetId) {
                stageMapping[stage.id] = targetId
              }
            }

            const response = await changeProjectLifecycle({
              projectId: project.id,
              request: {
                lifecycleId: values.lifecycleId,
                stageMapping,
              },
            })
            if (response.error) throw response.error

            messageApi.success('Project lifecycle changed successfully.')
            return true
          } catch (error: any) {
            if (error?.status === 422 && error?.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                error?.detail ??
                  error?.data?.detail ??
                  'An error occurred while changing the lifecycle. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the lifecycle. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  // Watch for lifecycle selection
  const selectedLifecycleId = Form.useWatch('lifecycleId', form)

  const { data: selectedLifecycle } = useGetProjectLifecycleQuery(
    selectedLifecycleId,
    { skip: !selectedLifecycleId },
  )

  // New lifecycle stage options for mapping dropdowns
  const newStageOptions = !selectedLifecycle?.stages ? [] : [...selectedLifecycle.stages]
      .sort((a, b) => a.order - b.order)
      .map((stage) => ({
        label: stage.name,
        value: stage.id,
      }))

  // Auto-populate mapping when stage names match
  useEffect(() => {
    if (!selectedLifecycle?.stages || currentStages.length === 0) return

    const mapping: Record<string, string> = {}
    for (const currentStage of currentStages) {
      const match = selectedLifecycle.stages.find(
        (p) => p.name.toLowerCase() === currentStage.name.toLowerCase(),
      )
      if (match) {
        mapping[currentStage.id] = match.id
      }
    }

    if (Object.keys(mapping).length > 0) {
      form.setFieldValue('stageMapping', mapping)
    }
  }, [selectedLifecycle?.stages, currentStages, form])

  // Stage preview timeline
  const stageItems = !selectedLifecycle?.stages ? [] : [...selectedLifecycle.stages]
      .sort((a, b) => a.order - b.order)
      .map((stage) => ({
        content: (
          <>
            <Text strong>{stage.name}</Text>
            <br />
            <Text type="secondary">{stage.description}</Text>
          </>
        ),
      }))

  // Stage mapping table columns
  const mappingColumns = [
    {
      title: 'Current Stage',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record: ProjectStageListDto) => (
        <Text strong>{`${record.order}. ${name}`}</Text>
      ),
    },
    {
      title: 'Map To',
      key: 'mapping',
      render: (_: unknown, record: ProjectStageListDto) => (
        <Item
          name={['stageMapping', record.id]}
          rules={[{ required: true, message: 'Required' }]}
          style={{ margin: 0 }}
        >
          <Select
            options={newStageOptions}
            placeholder="Select target stage"
            size="small"
          />
        </Item>
      ),
    },
  ]

  return (
    <Modal
      title="Change Project Lifecycle"
      open={isOpen}
      width={'50vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Change Lifecycle"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Text type="secondary">
          Select a new lifecycle and map existing stages to the new
          lifecycle&apos;s stages. Tasks will be moved to the mapped stages.
        </Text>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="change-project-lifecycle-form"
        >
          <Item
            name="lifecycleId"
            label="New Lifecycle"
            rules={[{ required: true, message: 'Lifecycle is required' }]}
          >
            <Select
              options={lifecycleOptions}
              placeholder="Select Lifecycle"
              loading={lifecyclesLoading}
            />
          </Item>

          {selectedLifecycleId && stageItems.length > 0 && (
            <Card size="small" title="New Stages" style={{ marginBottom: 16 }}>
              <Timeline items={stageItems} />
            </Card>
          )}

          {selectedLifecycleId && currentStages.length > 0 && (
            <Card size="small" title="Stage Mapping">
              <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                Map each current stage to a stage in the new lifecycle.
              </Text>
              <Table
                dataSource={[...currentStages].sort(
                  (a, b) => a.order - b.order,
                )}
                columns={mappingColumns}
                pagination={false}
                rowKey="id"
                size="small"
              />
            </Card>
          )}
        </Form>
      </Flex>
    </Modal>
  )
}

export default ChangeProjectLifecycleForm
