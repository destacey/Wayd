'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  KpiHealth,
  KpiTrend,
  StrategicInitiativeKpiCheckpointDetailsDto,
  StrategicInitiativeKpiMeasurementDto,
} from '@/src/services/wayd-api'
import {
  useGetStrategicInitiativeKpiCheckpointPlanQuery,
  useGetStrategicInitiativeKpiMeasurementsQuery,
  useGetStrategicInitiativeKpiQuery,
  useRemoveStrategicInitiativeKpiMeasurementMutation,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { getDrawerWidthPixels, isApiError} from '@/src/utils'
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  DeleteOutlined,
  ExclamationCircleOutlined,
  MinusOutlined,
  MoreOutlined,
} from '@ant-design/icons'
import {
  Button,
  Descriptions,
  Divider,
  Drawer,
  Dropdown,
  Flex,
  Popconfirm,
  Table,
  Tag,
  Typography,
} from 'antd'
import dayjs from 'dayjs'
import { CSSProperties, FC, useEffect, useState } from 'react'
import {
  AddStrategicInitiativeKpiMeasurementForm,
  DeleteStrategicInitiativeKpiForm,
  EditStrategicInitiativeKpiForm,
  ManageStrategicInitiativeKpiCheckpointPlanForm,
} from '.'
import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'

const { Item } = Descriptions
const { Text } = Typography

export interface StrategicInitiativeKpiDetailsDrawerProps {
  strategicInitiativeId: string
  kpiId: string
  drawerOpen: boolean
  onDrawerClose: () => void
  canManageKpis: boolean
  onRefresh: () => void
}

const formatValue = (
  value: number | undefined,
  prefix?: string,
  suffix?: string,
): string => {
  if (value === undefined || value === null) return '-'
  return `${prefix ?? ''}${value.toLocaleString()}${suffix ?? ''}`
}

const TrendTag: FC<{ trend: KpiTrend | undefined }> = ({ trend }) => {
  if (!trend || trend === KpiTrend.NoData) return undefined
  if (trend === KpiTrend.Improving)
    return (
      <Tag icon={<ArrowUpOutlined />} color="success">
        Improving
      </Tag>
    )
  if (trend === KpiTrend.Worsening)
    return (
      <Tag icon={<ArrowDownOutlined />} color="error">
        Worsening
      </Tag>
    )
  return (
    <Tag icon={<MinusOutlined />} color="warning">
      Stable
    </Tag>
  )
}

const KpiHealthTag: FC<{ health: KpiHealth | undefined }> = ({ health }) => {
  if (!health) return undefined
  if (health === KpiHealth.Healthy)
    return (
      <Tag icon={<CheckCircleOutlined />} color="success">
        Healthy
      </Tag>
    )
  if (health === KpiHealth.AtRisk)
    return (
      <Tag icon={<ExclamationCircleOutlined />} color="warning">
        At Risk
      </Tag>
    )
  return (
    <Tag icon={<CloseCircleOutlined />} color="error">
      Unhealthy
    </Tag>
  )
}

const StrategicInitiativeKpiDetailsDrawer: FC<
  StrategicInitiativeKpiDetailsDrawerProps
> = (props) => {
  const {
    strategicInitiativeId,
    kpiId,
    drawerOpen,
    onDrawerClose,
    canManageKpis,
    onRefresh,
  } = props

  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const [openEditKpiForm, setOpenEditKpiForm] = useState(false)
  const [openDeleteKpiForm, setOpenDeleteKpiForm] = useState(false)
  const [openAddMeasurementForm, setOpenAddMeasurementForm] = useState(false)
  const [openManageCheckpointPlanForm, setOpenManageCheckpointPlanForm] =
    useState(false)

  const messageApi = useMessage()

  const {
    data: kpiData,
    isLoading: kpiIsLoading,
    error: kpiError,
    refetch: refetchKpi,
  } = useGetStrategicInitiativeKpiQuery(
    { strategicInitiativeId, kpiId },
    { skip: !drawerOpen },
  )

  const {
    data: checkpointData,
    isLoading: checkpointsIsLoading,
    error: checkpointsError,
    refetch: refetchCheckpoints,
  } = useGetStrategicInitiativeKpiCheckpointPlanQuery(
    { strategicInitiativeId, kpiId },
    { skip: !drawerOpen },
  )

  const {
    data: measurementData,
    isLoading: measurementsIsLoading,
    error: measurementsError,
    refetch: refetchMeasurements,
  } = useGetStrategicInitiativeKpiMeasurementsQuery(
    { strategicInitiativeId, kpiId },
    { skip: !drawerOpen },
  )

  const [removeMeasurement] =
    useRemoveStrategicInitiativeKpiMeasurementMutation()

  useEffect(() => {
    if (kpiError) {
      messageApi.error(
        (isApiError(kpiError) ? kpiError.detail : undefined) ??
          'An error occurred while loading KPI data. Please try again.',
      )
    }
  }, [kpiError, messageApi])

  useEffect(() => {
    if (checkpointsError) {
      messageApi.error(
        (isApiError(checkpointsError) ? checkpointsError.detail : undefined) ??
          'An error occurred while loading checkpoint data. Please try again.',
      )
    }
  }, [checkpointsError, messageApi])

  useEffect(() => {
    if (measurementsError) {
      messageApi.error(
        (isApiError(measurementsError) ? measurementsError.detail : undefined) ??
          'An error occurred while loading measurement data. Please try again.',
      )
    }
  }, [measurementsError, messageApi])

  const prefix = kpiData?.prefix ?? undefined
  const suffix = kpiData?.suffix ?? undefined

  const onEditFormClosed = (wasSaved: boolean) => {
    setOpenEditKpiForm(false)
    if (wasSaved) {
      refetchKpi()
      onRefresh()
    }
  }

  const onAddMeasurementFormClosed = (wasSaved: boolean) => {
    setOpenAddMeasurementForm(false)
    if (wasSaved) {
      refetchCheckpoints()
      refetchMeasurements()
      onRefresh()
    }
  }

  const onDeleteMeasurement = async (measurementId: string) => {
    try {
      await removeMeasurement({
        strategicInitiativeId,
        kpiId,
        measurementId,
      }).unwrap()
      messageApi.success('Measurement deleted successfully.')
      onRefresh()
    } catch {
      messageApi.error(
        'An error occurred while deleting the measurement. Please try again.',
      )
    }
  }

  const onDeleteKpiFormClosed = (wasSaved: boolean) => {
    setOpenDeleteKpiForm(false)
    if (wasSaved) {
      onDrawerClose()
      onRefresh()
    }
  }

  const onManageCheckpointPlanFormClosed = (wasSaved: boolean) => {
    setOpenManageCheckpointPlanForm(false)
    if (wasSaved) {
      refetchCheckpoints()
      onRefresh()
    }
  }

  const checkpointColumns = [
    {
      title: 'Label',
      dataIndex: 'dateLabel',
      key: 'dateLabel',
      width: 90,
    },
    {
      title: 'Date',
      dataIndex: 'checkpointDate',
      key: 'checkpointDate',
      width: 130,
      render: (date: Date) =>
        date ? dayjs(date).format('YYYY-MM-DD h:mm A') : '-',
    },
    {
      title: 'Target',
      dataIndex: 'targetValue',
      key: 'targetValue',
      width: 90,
      align: 'right' as const,
      render: (value: number) => formatValue(value, prefix, suffix),
    },
    {
      title: 'Actual',
      key: 'actual',
      width: 90,
      align: 'right' as const,
      render: (_: unknown, record: StrategicInitiativeKpiCheckpointDetailsDto) =>
        record.measurement
          ? formatValue(record.measurement.actualValue, prefix, suffix)
          : '-',
    },
    {
      title: 'Health',
      dataIndex: 'health',
      key: 'health',
      width: 110,
      render: (health: KpiHealth | undefined) => (
        <KpiHealthTag health={health} />
      ),
    },
    {
      title: 'Trend',
      dataIndex: 'trend',
      key: 'trend',
      width: 110,
      render: (trend: KpiTrend | undefined) => <TrendTag trend={trend} />,
    },
  ]

  const measurementColumns = [
    {
      title: 'Date',
      dataIndex: 'measurementDate',
      key: 'measurementDate',
      width: 160,
      render: (date: Date) =>
        date ? dayjs(date).format('YYYY-MM-DD h:mm A') : '-',
    },
    {
      title: 'Actual Value',
      dataIndex: 'actualValue',
      key: 'actualValue',
      width: 110,
      align: 'right' as const,
      render: (value: number) => formatValue(value, prefix, suffix),
    },
    {
      title: 'Measured By',
      key: 'measuredBy',
      width: 140,
      render: (_: unknown, record: StrategicInitiativeKpiMeasurementDto) => record.measuredBy?.name ?? '-',
    },
    {
      title: 'Note',
      dataIndex: 'note',
      key: 'note',
      render: (note: string | undefined) => note ?? '-',
    },
    ...(canManageKpis
      ? [
          {
            key: 'actions',
            width: 50,
            align: 'center' as const,
            render: (_: unknown, record: StrategicInitiativeKpiMeasurementDto) => (
              <Popconfirm
                title="Delete measurement?"
                description="This action cannot be undone."
                onConfirm={() => onDeleteMeasurement(record.id)}
                okText="Delete"
                okButtonProps={{ danger: true }}
              >
                <Button
                  size="small"
                  type="text"
                  danger
                  icon={<DeleteOutlined />}
                />
              </Popconfirm>
            ),
          },
        ]
      : []),
  ]

  const extraMenu = canManageKpis ? (
    <Dropdown
      menu={{
        items: [
          {
            key: 'edit',
            label: 'Edit',
            onClick: () => setOpenEditKpiForm(true),
          },
          {
            key: 'delete',
            label: 'Delete',
            danger: true,
            onClick: () => setOpenDeleteKpiForm(true),
          },
          { key: 'divider', type: 'divider' },
          {
            key: 'checkpoint-plan',
            label: 'Checkpoint Plan',
            onClick: () => setOpenManageCheckpointPlanForm(true),
          },
          {
            key: 'add-measurement',
            label: 'Add Measurement',
            onClick: () => setOpenAddMeasurementForm(true),
          },
        ],
      }}
      trigger={['click']}
    >
      <Button type="text" size="small" icon={<MoreOutlined />} />
    </Dropdown>
  ) : undefined

  return (
    <>
      <Drawer
        title={kpiData ? `KPI ${kpiData.key} - ${kpiData.name}` : 'KPI Details'}
        placement="right"
        onClose={onDrawerClose}
        open={drawerOpen}
        loading={kpiIsLoading}
        size={size}
        resizable={{ onResize: (newSize) => setSize(newSize) }}
        destroyOnHidden={true}
        styles={{ body: { scrollbarWidth: 'auto' } as CSSProperties }}
        className="custom-drawer"
        extra={extraMenu}
      >
        <Flex vertical gap="middle">
          <Flex vertical gap={10}>
            <LabeledContent label="Starting Value">
              {formatValue(kpiData?.startingValue, prefix, suffix)}
            </LabeledContent>
            <LabeledContent label="Target Value">
              {formatValue(kpiData?.targetValue, prefix, suffix)}
            </LabeledContent>
            <LabeledContent label="Actual Value">
              {kpiData?.actualValue !== undefined
                ? formatValue(kpiData.actualValue, prefix, suffix)
                : '-'}
            </LabeledContent>
            {prefix && <LabeledContent label="Prefix">{prefix}</LabeledContent>}
            {suffix && <LabeledContent label="Suffix">{suffix}</LabeledContent>}
            <LabeledContent label="Target Direction">
              {kpiData?.targetDirection as unknown as string}
            </LabeledContent>
            {kpiData?.description && (
              <LabeledContent label="Description">
                <ExpandableContent background="var(--ant-color-bg-elevated)">
                  <MarkdownRenderer markdown={kpiData.description} />
                </ExpandableContent>
              </LabeledContent>
            )}
          </Flex>
          <Divider titlePlacement="start">
            <Text type="secondary" style={{ fontSize: 12 }}>
              Checkpoints
            </Text>
          </Divider>
          <Table
            dataSource={checkpointData ?? []}
            columns={checkpointColumns}
            rowKey="id"
            size="small"
            loading={checkpointsIsLoading}
            pagination={false}
            locale={{ emptyText: 'No checkpoints defined' }}
            scroll={{ x: 'max-content' }}
          />
          <Divider titlePlacement="start">
            <Text type="secondary" style={{ fontSize: 12 }}>
              Measurements
            </Text>
          </Divider>
          <Table
            dataSource={measurementData ?? []}
            columns={measurementColumns}
            rowKey="id"
            size="small"
            loading={measurementsIsLoading}
            pagination={false}
            locale={{ emptyText: 'No measurements recorded' }}
            scroll={{ x: 'max-content' }}
          />
        </Flex>
      </Drawer>
      {openEditKpiForm && (
        <EditStrategicInitiativeKpiForm
          strategicInitiativeId={strategicInitiativeId}
          kpiId={kpiId}
          onFormComplete={() => onEditFormClosed(true)}
          onFormCancel={() => onEditFormClosed(false)}
        />
      )}
      {openAddMeasurementForm && (
        <AddStrategicInitiativeKpiMeasurementForm
          strategicInitiativeId={strategicInitiativeId}
          kpiId={kpiId}
          onFormComplete={() => onAddMeasurementFormClosed(true)}
          onFormCancel={() => onAddMeasurementFormClosed(false)}
        />
      )}
      {openDeleteKpiForm && kpiData && (
        <DeleteStrategicInitiativeKpiForm
          strategicInitiativeId={strategicInitiativeId}
          kpi={kpiData}
          onFormComplete={() => onDeleteKpiFormClosed(true)}
          onFormCancel={() => onDeleteKpiFormClosed(false)}
        />
      )}
      {openManageCheckpointPlanForm && (
        <ManageStrategicInitiativeKpiCheckpointPlanForm
          strategicInitiativeId={strategicInitiativeId}
          kpiId={kpiId}
          onFormComplete={() => onManageCheckpointPlanFormClosed(true)}
          onFormCancel={() => onManageCheckpointPlanFormClosed(false)}
        />
      )}
    </>
  )
}

export default StrategicInitiativeKpiDetailsDrawer
