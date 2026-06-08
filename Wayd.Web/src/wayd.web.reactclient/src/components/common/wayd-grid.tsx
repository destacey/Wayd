'use client'

import './grid/ag-grid-init'
import { AgGridReact, AgGridReactProps } from 'ag-grid-react'
import {
  ChangeEvent,
  forwardRef,
  useImperativeHandle,
  useRef,
  useState,
} from 'react'
import {
  Button,
  Col,
  Divider,
  Input,
  Row,
  Space,
  Spin,
  Tooltip,
  Typography,
} from 'antd'
import {
  ClearOutlined,
  DownloadOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { ItemType } from 'antd/es/menu/interface'
import useTheme from '../contexts/theme'
import WaydEmpty from './wayd-empty'
import { ControlItemsMenu } from './control-items-menu'
import { useRemainingHeight } from '@/src/hooks'

const { Text } = Typography

interface ModaGridProps extends AgGridReactProps {
  height?: number
  width?: number
  includeGlobalSearch?: boolean
  includeExportButton?: boolean
  actions?: React.ReactNode | null
  gridControlMenuItems?: ItemType[]
  toolbarActions?: React.ReactNode | null
  loadData?: () => Promise<void> | void
  emptyMessage?: string
}

const modaDefaultColDef = {
  sortable: true,
  filter: true,
  resizable: true,
  floatingFilter: true,
}

const WaydGrid = forwardRef<AgGridReact, ModaGridProps>(
  (
    {
      height,
      width,
      includeGlobalSearch,
      includeExportButton,
      actions,
      gridControlMenuItems,
      toolbarActions,
      defaultColDef,
      rowData,
      loadData,
      emptyMessage,
      ...props
    },
    ref,
  ) => {
    const { agGridTheme } = useTheme()
    const [displayedRowCount, setDisplayedRowCount] = useState(0)
    const [searchValue, setSearchValue] = useState('')
    const showGlobalSearch = includeGlobalSearch ?? true
    const showExportButton = includeExportButton ?? true
    const showGridControls = (gridControlMenuItems?.length ?? 0) > 0

    const gridRef = useRef<AgGridReact>(null)
    const [gridContainerRef, autoHeight] = useRemainingHeight()

    // Use explicit height if provided, otherwise fill remaining viewport space
    const resolvedHeight = height ?? autoHeight

    // Forward the ref to parent components
    useImperativeHandle(ref, () => gridRef.current as AgGridReact, [])

    const rowCount = rowData?.length ?? 0

    const onGridReady = () => {
      if (!props.loading && rowCount === 0 && loadData) {
        loadData()
      }
    }

    const onModelUpdated = () => {
      setDisplayedRowCount(gridRef.current?.api.getDisplayedRowCount() ?? 0)
    }

    const onGlobalSearchChange = (e: ChangeEvent<HTMLInputElement>) => {
      const value = e.target.value
      setSearchValue(value)
      gridRef.current?.api.setGridOption('quickFilterText', value)
    }

    const onBtnExport = () => {
      gridRef.current?.api.exportDataAsCsv()
    }

    const onClearSortAndFilters = () => {
      gridRef.current?.api.applyColumnState({
        defaultState: { sort: null, sortIndex: null },
      })
      gridRef.current?.api.setFilterModel(null)
      setSearchValue('')
      gridRef.current?.api.setGridOption('quickFilterText', undefined)
    }

    return (
      <div style={{ width: width }}>
        <Space vertical style={{ width: '100%' }}>
          <Row>
            {actions && (
              <Col xs={24} sm={24} md={10}>
                {actions}
              </Col>
            )}
            <Col xs={24} sm={24} md={actions ? 14 : 24}>
              <Space
                style={{ display: 'flex', justifyContent: 'flex-end' }}
                wrap
              >
                <Text>
                  {displayedRowCount} of {rowCount}
                </Text>
                {showGlobalSearch && (
                  <Input
                    placeholder="Search"
                    allowClear={true}
                    value={searchValue}
                    onChange={onGlobalSearchChange}
                    suffix={<SearchOutlined />}
                  />
                )}
                <Space style={{ display: 'flex', justifyContent: 'flex-end' }}>
                  {showGridControls && (
                    <ControlItemsMenu items={gridControlMenuItems} />
                  )}
                  {loadData && (
                    <Tooltip title="Refresh Grid">
                      <Button
                        type="text"
                        shape="circle"
                        icon={<ReloadOutlined />}
                        onClick={loadData}
                      />
                    </Tooltip>
                  )}
                  <Tooltip title="Clear Sort and Filters">
                    <Button
                      type="text"
                      shape="circle"
                      icon={<ClearOutlined />}
                      onClick={onClearSortAndFilters}
                    />
                  </Tooltip>
                  {(showExportButton || toolbarActions) && (
                    <Divider vertical style={{ height: '30px' }} />
                  )}
                  {showExportButton && (
                    <Tooltip title="Export to CSV">
                      <Button
                        type="text"
                        shape="circle"
                        icon={<DownloadOutlined />}
                        onClick={onBtnExport}
                      />
                    </Tooltip>
                  )}
                  {toolbarActions && toolbarActions}
                </Space>
              </Space>
            </Col>
          </Row>

          <div ref={gridContainerRef} {...(resolvedHeight !== -1 && { style: { height: resolvedHeight } })}>
            <AgGridReact
              ref={gridRef}
              theme={agGridTheme}
              defaultColDef={defaultColDef ?? modaDefaultColDef}
              onGridReady={onGridReady}
              animateRows={false} // animation has to be off for cell text selection to work correctly
              rowData={rowData}
              onModelUpdated={onModelUpdated}
              multiSortKey="ctrl"
              loading={props.loading}
              loadingOverlayComponent={() => <Spin size="large" />}
              noRowsOverlayComponent={() => (
                <WaydEmpty message={emptyMessage ?? 'No records found.'} />
              )}
              enableCellTextSelection={true}
              ensureDomOrder={true}
              {...(resolvedHeight === -1 && { domLayout: 'autoHeight' })}
              {...props}
            ></AgGridReact>
          </div>
        </Space>
      </div>
    )
  },
)

WaydGrid.displayName = 'WaydGrid'

export default WaydGrid
