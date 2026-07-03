'use client'

import styles from './grid-toolbar.module.css'
import { Button, Input, Popover, Typography } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import {
  ClearOutlined,
  DownloadOutlined,
  QuestionCircleOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons'

const { Text } = Typography

export interface GridToolbarProps {
  displayedRowCount: number
  totalRowCount: number
  searchValue: string
  onSearchChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  onRefresh?: () => Promise<any> | void
  onClearFilters: () => void
  hasActiveFilters: boolean
  onExportCsv?: () => void
  isLoading: boolean
  /** Whether to show the global search input. Default: true. */
  includeGlobalSearch?: boolean
  /** Slot for domain-specific actions rendered on the left. */
  leftSlot?: React.ReactNode
  /** Content rendered inside the help popover. */
  helpContent?: React.ReactNode
  /** Slot for actions rendered on the far right of the toolbar. */
  rightSlot?: React.ReactNode
}

/**
 * The one grid toolbar: search, row count, refresh, clear filters, export CSV,
 * and help popover. Domain-specific actions go in `leftSlot` / `rightSlot`;
 * `rightSlot` renders at the far right (after export/help), matching the
 * legacy ag-grid `toolbarActions` and TreeGridToolbar placement — view
 * selectors and control menus belong there.
 */
const GridToolbar = ({
  displayedRowCount,
  totalRowCount,
  searchValue,
  onSearchChange,
  onRefresh,
  onClearFilters,
  hasActiveFilters,
  onExportCsv,
  isLoading,
  includeGlobalSearch = true,
  leftSlot,
  helpContent,
  rightSlot,
}: GridToolbarProps) => {
  return (
    <div className={styles.toolbar}>
      <div>{leftSlot}</div>

      <div className={styles.toolbarRight}>
        <Text>
          {displayedRowCount} of {totalRowCount}
        </Text>
        {includeGlobalSearch && (
          <Input
            placeholder="Search"
            allowClear={true}
            value={searchValue}
            onChange={onSearchChange}
            suffix={<SearchOutlined />}
            className={styles.toolbarSearch}
          />
        )}
        {onRefresh && (
          <WaydTooltip title="Refresh">
            <Button
              type="text"
              shape="circle"
              icon={<ReloadOutlined />}
              onClick={onRefresh}
            />
          </WaydTooltip>
        )}
        <WaydTooltip title="Clear Filters and Sorting">
          <Button
            type="text"
            shape="circle"
            icon={<ClearOutlined />}
            onClick={onClearFilters}
            disabled={!hasActiveFilters}
          />
        </WaydTooltip>
        {onExportCsv && (
          <>
            <span className={styles.toolbarDivider} />
            <WaydTooltip title="Export to CSV">
              <Button
                type="text"
                shape="circle"
                icon={<DownloadOutlined />}
                onClick={onExportCsv}
                disabled={isLoading || displayedRowCount === 0}
              />
            </WaydTooltip>
          </>
        )}
        {helpContent && (
          <Popover
            content={helpContent}
            trigger="click"
            placement="bottomRight"
            getPopupContainer={() => document.body}
            overlayStyle={{ maxWidth: 'calc(100vw - 24px)' }}
          >
            <WaydTooltip title="Grid Actions Help">
              <Button
                type="text"
                shape="circle"
                icon={<QuestionCircleOutlined />}
              />
            </WaydTooltip>
          </Popover>
        )}
        {rightSlot}
      </div>
    </div>
  )
}

export default GridToolbar
