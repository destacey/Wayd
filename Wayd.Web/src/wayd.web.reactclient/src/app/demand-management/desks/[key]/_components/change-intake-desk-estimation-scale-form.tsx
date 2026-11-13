'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  IntakeDeskDto,
  IntakeDeskOpenEstimateCountDto,
  ScaleChangeEstimateHandling,
} from '@/src/services/wayd-api'
import {
  useChangeIntakeDeskEstimationScaleMutation,
  useGetIntakeDeskOpenEstimatesQuery,
  useGetIntakeEstimationScaleChoicesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import {
  Alert,
  Button,
  Flex,
  Form,
  Modal,
  Radio,
  Select,
  Skeleton,
  Typography,
} from 'antd'
import { FC, useState } from 'react'
import styles from './change-intake-desk-estimation-scale-form.module.css'

const { Item } = Form
const { Paragraph, Text } = Typography

interface OpenEstimatesOnVersion {
  versionId: string
  label: string
  values: string
}

/** The open estimates grouped by the version they were given on, in the order the server sends them. */
const groupByVersion = (
  counts: IntakeDeskOpenEstimateCountDto[],
): OpenEstimatesOnVersion[] => {
  const groups = new Map<string, OpenEstimatesOnVersion>()
  for (const c of counts) {
    const value = `${c.value} (${c.requestCount})`
    const group = groups.get(c.scale.versionId)
    if (group) group.values = `${group.values}, ${value}`
    else
      groups.set(c.scale.versionId, {
        versionId: c.scale.versionId,
        label: `${c.scale.name}, version ${c.scale.versionNumber}`,
        values: value,
      })
  }
  return [...groups.values()]
}

export interface ChangeIntakeDeskEstimationScaleFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeEstimationScaleFormValues {
  estimationScaleId?: string
  openEstimates: ScaleChangeEstimateHandling
}

/**
 * Chooses the estimation scale a desk's team sizes its requests on, or stops estimating them. Offered where
 * the desk's `canManage` says so: to its Desk Admins and the Demand Management Admin.
 */
const ChangeIntakeDeskEstimationScaleForm: FC<
  ChangeIntakeDeskEstimationScaleFormProps
> = ({ desk, onFormComplete, onFormCancel }) => {
  const messageApi = useMessage()
  const [changeEstimationScale] = useChangeIntakeDeskEstimationScaleMutation()
  const { data: choices, isLoading } = useGetIntakeEstimationScaleChoicesQuery()
  const {
    data: openEstimates,
    isFetching: isLoadingOpenEstimates,
    error: openEstimatesError,
    refetch: refetchOpenEstimates,
  } = useGetIntakeDeskOpenEstimatesQuery(desk.id)

  // A deactivated scale stays pinned until changed, but is no longer a choice; keep it showing by name.
  const current = desk.estimationScale
  const [chosenId, setChosenId] = useState(current?.id)
  // Choosing the pinned scale again moves the desk only when the scale has a newer version.
  const moves =
    chosenId !== current?.id ||
    // Loose, since a desk with no scale arrives from the API with null rather than nothing.
    (current != null && !current.isCurrentVersion)
  const estimated = openEstimates ?? []
  const estimatedRequestCount = estimated.reduce(
    (total, c) => total + c.requestCount,
    0,
  )
  const decides = moves && estimatedRequestCount > 0
  // A move must not go ahead until the estimates it would keep or clear have been shown.
  const awaitingEstimates =
    moves && (isLoadingOpenEstimates || !!openEstimatesError)

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeEstimationScaleFormValues>({
      onSubmit: async (values, form) => {
        try {
          await changeEstimationScale({
            deskId: desk.id,
            estimationScaleId: values.estimationScaleId,
            // A choice made and then hidden by switching back is not a decision.
            openEstimates: decides
              ? (values.openEstimates ?? ScaleChangeEstimateHandling.Keep)
              : ScaleChangeEstimateHandling.Keep,
          }).unwrap()
          messageApi.success('Estimation scale changed.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to change the estimation scale.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the estimation scale.',
    })

  const options = [
    ...(current && !(choices ?? []).some((c) => c.id === current.id)
      ? [
          {
            value: current.id,
            label: `${current.name} (Inactive)`,
            values: undefined,
          },
        ]
      : []),
    ...(choices ?? []).map((s) => ({
      value: s.id,
      label: s.name,
      values: s.estimateValues.join(', '),
    })),
  ]

  return (
    <Modal
      title="Change Estimation Scale"
      open={isOpen}
      onOk={handleOk}
      okText="Change"
      okButtonProps={{ disabled: awaitingEstimates }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        What {desk.name}&apos;s team sizes its requests on. The desk uses the
        scale&apos;s current version; choose the scale it uses again to move to
        a newer version. Done and removed requests keep their estimates. Scales
        are built in Settings, under Estimation Scales.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-intake-desk-estimation-scale-form"
        initialValues={{
          estimationScaleId: current?.id,
          openEstimates: ScaleChangeEstimateHandling.Keep,
        }}
      >
        <Item name="estimationScaleId" label="Estimation Scale">
          <Select
            allowClear
            onChange={(id?: string) => setChosenId(id)}
            placeholder="Do not estimate requests"
            loading={isLoading}
            options={options}
            // Each scale's values under its name, so a scale is chosen by what it sizes with.
            optionRender={(option) => (
              <Flex vertical>
                <Text>{option.data.label}</Text>
                {option.data.values && (
                  <Text type="secondary">{option.data.values}</Text>
                )}
              </Flex>
            )}
          />
        </Item>
        {moves && isLoadingOpenEstimates ? (
          <Skeleton active paragraph={{ rows: 2 }} title={false} />
        ) : moves && openEstimatesError ? (
          <Alert
            type="error"
            showIcon
            title={
              toApiError(openEstimatesError).detail ??
              "The open requests' estimates could not be loaded."
            }
            action={
              <Button size="small" onClick={() => refetchOpenEstimates()}>
                Retry
              </Button>
            }
          />
        ) : (
          decides && (
            <>
              <Flex vertical gap={4} className={styles.openEstimates}>
                <Text>
                  {estimatedRequestCount === 1
                    ? '1 open request has an estimate:'
                    : `${estimatedRequestCount} open requests have estimates:`}
                </Text>
                {groupByVersion(estimated).map((g) => (
                  <Text key={g.versionId} type="secondary">
                    {g.label}: {g.values}
                  </Text>
                ))}
              </Flex>
              <Item
                name="openEstimates"
                label="Their Estimates"
                rules={[{ required: true }]}
              >
                <Radio.Group>
                  <Flex vertical gap={4}>
                    <Radio value={ScaleChangeEstimateHandling.Keep}>
                      Keep each on the version it was given on until it is
                      estimated again
                    </Radio>
                    <Radio value={ScaleChangeEstimateHandling.Clear}>
                      Clear every one
                    </Radio>
                  </Flex>
                </Radio.Group>
              </Item>
            </>
          )
        )}
      </Form>
    </Modal>
  )
}

export default ChangeIntakeDeskEstimationScaleForm
