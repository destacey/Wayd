'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  KpiCheckpointPlanItemRequest,
  KpiTargetDirection,
  ManageStrategicInitiativeKpiCheckpointPlanRequest,
} from '@/src/services/wayd-api'
import {
  useGetStrategicInitiativeKpiCheckpointsQuery,
  useGetStrategicInitiativeKpiQuery,
  useManageStrategicInitiativeKpiCheckpointPlanMutation,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import {
  Button,
  DatePicker,
  Descriptions,
  Divider,
  Flex,
  Form,
  Input,
  InputNumber,
  InputRef,
  Modal,
  Typography,
} from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useRef } from 'react'

type InputNumberRef = { focus: () => void } | null

const { Item } = Form
const { Item: DescriptionItem } = Descriptions
const { Text } = Typography

export interface ManageStrategicInitiativeKpiCheckpointPlanFormProps {
  strategicInitiativeId: string
  kpiId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CheckpointPlanItemFormValues {
  checkpointId?: string
  targetValue: number
  atRiskValue?: number
  checkpointDate: Dayjs
  dateLabel: string
}

interface ManageKpiCheckpointPlanFormValues {
  checkpoints: CheckpointPlanItemFormValues[]
}

const mapToRequestValues = (
  values: ManageKpiCheckpointPlanFormValues,
  strategicInitiativeId: string,
  kpiId: string,
): ManageStrategicInitiativeKpiCheckpointPlanRequest => {
  return {
    strategicInitiativeId,
    kpiId,
    checkpoints: values.checkpoints.map(
      (c): KpiCheckpointPlanItemRequest => ({
        checkpointId: c.checkpointId!,
        targetValue: c.targetValue,
        atRiskValue: c.atRiskValue ?? undefined,
        checkpointDate: c.checkpointDate?.toDate(),
        dateLabel: c.dateLabel,
      }),
    ),
  }
}

const ManageStrategicInitiativeKpiCheckpointPlanForm = ({
  strategicInitiativeId,
  kpiId,
  onFormComplete,
  onFormCancel,
}: ManageStrategicInitiativeKpiCheckpointPlanFormProps) => {
  const messageApi = useMessage()
  const newRowDateLabelRef = useRef<InputRef>(null)
  const targetValueRefs = useRef<InputNumberRef[]>([])

  const { data: kpiData } = useGetStrategicInitiativeKpiQuery({
    strategicInitiativeId,
    kpiId,
  })

  const targetValuePrefix = kpiData?.prefix ?? undefined
  const targetValueSuffix = kpiData?.suffix ?? undefined

  const [manageCheckpointPlan] =
    useManageStrategicInitiativeKpiCheckpointPlanMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ManageKpiCheckpointPlanFormValues>({
      onSubmit: async (values: ManageKpiCheckpointPlanFormValues, form) => {
        try {
          const request = mapToRequestValues(
            values,
            strategicInitiativeId,
            kpiId,
          )
          const response = await manageCheckpointPlan(request)
          if (response.error) throw response.error

          messageApi.success('KPI checkpoint plan updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the KPI checkpoint plan. Please try again.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the KPI checkpoint plan. Please try again.',
      permission: 'Permissions.StrategicInitiatives.Update',
    })

  const { data: checkpointPlanData } =
    useGetStrategicInitiativeKpiCheckpointsQuery(
      { strategicInitiativeId, kpiId },
      { skip: !isOpen },
    )

  // Initialize form values when checkpoint data is loaded
  useEffect(() => {
    if (isOpen && checkpointPlanData !== undefined) {
      form.setFieldsValue({
        checkpoints: (checkpointPlanData ?? []).map((c) => ({
          checkpointId: c.id,
          targetValue: c.targetValue,
          atRiskValue: c.atRiskValue ?? undefined,
          checkpointDate: dayjs(c.checkpointDate),
          dateLabel: c.dateLabel,
        })),
      })
    }
  }, [isOpen, checkpointPlanData, form])

  return (
    <Modal
      title="Manage KPI Checkpoint Plan"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={750}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="manage-strategic-initiative-kpi-checkpoint-plan-form"
      >
        <Descriptions size="small" column={1}>
          <DescriptionItem label="KPI">{`${kpiData?.key} - ${kpiData?.name}`}</DescriptionItem>
          <DescriptionItem label="Target Value">
            {kpiData?.targetValue}
          </DescriptionItem>
          {kpiData?.prefix && (
            <DescriptionItem label="Prefix">
              {kpiData.prefix}
            </DescriptionItem>
          )}
          {kpiData?.suffix && (
            <DescriptionItem label="Suffix">
              {kpiData.suffix}
            </DescriptionItem>
          )}
          <DescriptionItem label="Target Direction">
            {kpiData?.targetDirection as unknown as string}
          </DescriptionItem>
        </Descriptions>
        <br />
        <Divider titlePlacement="start">Checkpoints</Divider>
        <Form.List name="checkpoints">
          {(fields, { add, remove }) => (
            <>
              {fields.length > 0 && (
                <Flex style={{ width: '90%', marginBottom: 4 }}>
                  <Flex gap="small" wrap>
                    <Text
                      type="secondary"
                      style={{ width: 150, fontSize: 12 }}
                    >
                      Date Label
                    </Text>
                    <Text
                      type="secondary"
                      style={{ width: 200, fontSize: 12 }}
                    >
                      Checkpoint Date
                    </Text>
                    <Text
                      type="secondary"
                      style={{ width: 125, fontSize: 12 }}
                    >
                      Target Value
                    </Text>
                    <Text
                      type="secondary"
                      style={{ width: 130, fontSize: 12 }}
                    >
                      At Risk Value
                    </Text>
                  </Flex>
                </Flex>
              )}
              {fields.map(({ key, name, ...restField }, index) => (
                <div key={key}>
                  <Flex align="center" justify="space-between">
                    <Flex vertical style={{ width: '90%' }}>
                      <Item
                        {...restField}
                        name={[name, 'checkpointId']}
                        hidden={true}
                      >
                        <Input hidden={true} />
                      </Item>
                      <Flex gap="small" wrap>
                        <Item
                          {...restField}
                          style={{ margin: '0' }}
                          name={[name, 'dateLabel']}
                          rules={[
                            {
                              required: true,
                              message: 'Date label is required',
                            },
                            { max: 16, message: 'Max 16 characters' },
                          ]}
                        >
                          <Input
                            showCount
                            maxLength={16}
                            placeholder="Date Label"
                            style={{ width: 150 }}
                            ref={
                              index === fields.length - 1
                                ? newRowDateLabelRef
                                : null
                            }
                          />
                        </Item>
                        <Item
                          {...restField}
                          style={{ margin: '0' }}
                          name={[name, 'checkpointDate']}
                          rules={[
                            {
                              required: true,
                              message: 'Checkpoint date is required',
                            },
                          ]}
                        >
                          <DatePicker
                            showTime
                            format="YYYY-MM-DD h:mm A"
                            placeholder="Checkpoint Date"
                            onOk={() =>
                              setTimeout(
                                () => targetValueRefs.current[index]?.focus(),
                                100,
                              )
                            }
                          />
                        </Item>
                        <Item
                          {...restField}
                          style={{ margin: '0' }}
                          name={[name, 'targetValue']}
                          rules={[
                            {
                              required: true,
                              type: 'number',
                              message: 'Target value is required',
                            },
                          ]}
                        >
                          <InputNumber
                            ref={(el) => {
                              targetValueRefs.current[index] = el
                            }}
                            placeholder="Target Value"
                            prefix={targetValuePrefix}
                            suffix={targetValueSuffix}
                            style={{ width: 125 }}
                          />
                        </Item>
                        <Item
                          {...restField}
                          style={{ margin: '0' }}
                          name={[name, 'atRiskValue']}
                          rules={[
                            {
                              validator: (_, atRiskValue) => {
                                if (atRiskValue == null)
                                  return Promise.resolve()
                                const targetValue = form.getFieldValue([
                                  'checkpoints',
                                  name,
                                  'targetValue',
                                ]) as number | undefined
                                if (targetValue == null)
                                  return Promise.resolve()
                                if (atRiskValue === targetValue)
                                  return Promise.reject(
                                    'At risk value must differ from target value.',
                                  )
                                const direction =
                                  kpiData?.targetDirection as unknown as string
                                if (
                                  direction === KpiTargetDirection.Increase &&
                                  atRiskValue >= targetValue
                                )
                                  return Promise.reject(
                                    'At risk value must be less than target value when direction is Increase.',
                                  )
                                if (
                                  direction === KpiTargetDirection.Decrease &&
                                  atRiskValue <= targetValue
                                )
                                  return Promise.reject(
                                    'At risk value must be greater than target value when direction is Decrease.',
                                  )
                                return Promise.resolve()
                              },
                            },
                          ]}
                        >
                          <InputNumber
                            placeholder="At Risk Value"
                            prefix={targetValuePrefix}
                            suffix={targetValueSuffix}
                            style={{ width: 130 }}
                          />
                        </Item>
                      </Flex>
                    </Flex>
                    <MinusCircleOutlined
                      onClick={() => remove(name)}
                      title="Remove Checkpoint"
                    />
                  </Flex>
                  <Divider dashed />
                </div>
              ))}
              <Item>
                <Button
                  type="dashed"
                  onClick={() => {
                    add()
                    setTimeout(() => newRowDateLabelRef.current?.focus(), 0)
                  }}
                  block
                  icon={<PlusOutlined />}
                >
                  Add Checkpoint
                </Button>
              </Item>
            </>
          )}
        </Form.List>
      </Form>
    </Modal>
  )
}

export default ManageStrategicInitiativeKpiCheckpointPlanForm
