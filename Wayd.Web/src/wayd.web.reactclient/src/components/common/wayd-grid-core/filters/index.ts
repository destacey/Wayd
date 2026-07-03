// Filter model + helpers
export type {
  ColumnFilterModel,
  DateCondition,
  DateFilterModel,
  DateSetFilterModel,
  DateTimeCondition,
  DateTimeFilterModel,
  FilterJoin,
  FilterType,
  NumberCondition,
  NumberFilterModel,
  OperatorOption,
  SetFilterModel,
  TextCondition,
  TextFilterModel,
} from './filter-model'
export {
  CONDITION_LIMIT,
  DEFAULT_MAX_CONDITIONS,
  addCondition,
  createEmptyCondition,
  createEmptyFilterModel,
  defaultOperatorFor,
  isConditionActive,
  isFilterModelEmpty,
  operatorNeedsSecondValue,
  operatorNeedsValue,
  operatorOptionsFor,
  removeConditionAt,
  resolveFilterType,
  updateConditionAt,
} from './filter-model'

// Filter engine
export {
  createMultiValueSetFilter,
  evaluateFilterModel,
  toDayKey,
  waydColumnFilter,
} from './filter-engine'

// Floating-row filter summaries (read-only chip for complex date filters)
export { canFloatingEditDate, describeDateFilter } from './filter-summary'

// Relative date quick-filters (Today, This Week, Last Month, …)
export {
  RELATIVE_DATE_OPTIONS,
  buildRelativeDateFilter,
} from './date-filter-relative'
export type {
  RelativeDateOption,
  RelativeDatePeriod,
} from './date-filter-relative'

// Filter popup UI
export { default as FilterPopup } from './filter-popup'
export type { FilterPopupProps } from './filter-popup'

// Floating (single-condition, inline) filter UI
export { default as FloatingFilter } from './floating-filter'
export type { FloatingFilterProps } from './floating-filter'

// Excel/AG Grid-style set filter panel (search + Select All + checkboxes)
export { default as SetFilterPanel } from './set-filter-panel'
export type { SetFilterPanelProps } from './set-filter-panel'

// Combined text + set filter panel (Text Filter expander over the set list)
export { default as CombinedFilterPanel } from './combined-filter-panel'
export type { CombinedFilterPanelProps } from './combined-filter-panel'

// Excel-style date filter panel (date tree + relative filters + condition builder)
export { default as DateFilterPanel } from './date-filter-panel'
export type { DateFilterPanelProps } from './date-filter-panel'

// Date tree grouping (Year → Month → Day) used by the date filter panel
export { buildDateTree, dayKeysOf } from './date-tree'
export type { DayNode, MonthNode, YearNode } from './date-tree'
