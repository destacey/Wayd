/**
 * WaydGrid filter model.
 *
 * One filter descriptor per column, stored as the column's TanStack filter
 * value. The floating-filter row and the filter popup are two UIs over this
 * same descriptor — the floating row edits `conditions[0]`, the popup edits the
 * full descriptor. This mirrors AG Grid's model where floating filters "do not
 * have their own state, but rather display the state of the main filter".
 *
 * Kept UI-free and pure so it is unit-testable in isolation.
 */

// ─── Operators ─────────────────────────────────────────────

export type TextOperator =
  | 'contains'
  | 'notContains'
  | 'equals'
  | 'notEqual'
  | 'startsWith'
  | 'endsWith'
  | 'blank'
  | 'notBlank'

export type NumberOperator =
  | 'equals'
  | 'notEqual'
  | 'greaterThan'
  | 'greaterThanOrEqual'
  | 'lessThan'
  | 'lessThanOrEqual'
  | 'inRange'
  | 'blank'
  | 'notBlank'

export type DateOperator =
  | 'equals'
  | 'notEqual'
  | 'before'
  | 'after'
  | 'inRange'
  | 'blank'
  | 'notBlank'

/** Date-time uses the same operator set as date; granularity differs at eval time. */
export type DateTimeOperator = DateOperator

export type FilterJoin = 'AND' | 'OR'

// ─── Conditions ────────────────────────────────────────────

export interface TextCondition {
  op: TextOperator
  value: string
}

export interface NumberCondition {
  op: NumberOperator
  /** Primary operand. Null while the user is still entering. */
  value: number | null
  /** Second operand for `inRange`. */
  valueTo?: number | null
}

export interface DateCondition {
  op: DateOperator
  /** ISO date string (yyyy-MM-dd) or null. */
  value: string | null
  /** Second operand for `inRange`. */
  valueTo?: string | null
}

export interface DateTimeCondition {
  op: DateTimeOperator
  /** ISO date-time string or null. */
  value: string | null
  /** Second operand for `inRange`. */
  valueTo?: string | null
}

// ─── Descriptors ───────────────────────────────────────────

export interface TextFilterModel {
  type: 'text'
  conditions: TextCondition[]
  join: FilterJoin
}

export interface NumberFilterModel {
  type: 'number'
  conditions: NumberCondition[]
  join: FilterJoin
}

export interface DateFilterModel {
  type: 'date'
  conditions: DateCondition[]
  join: FilterJoin
}

export interface DateTimeFilterModel {
  type: 'dateTime'
  conditions: DateTimeCondition[]
  join: FilterJoin
}

export interface SetFilterModel {
  type: 'set'
  /** Selected values; empty array means "no filter" (all pass). */
  values: string[]
}

/**
 * Sentinel entry representing blank cells (null/undefined/'') in a set
 * filter's value list — the Excel/AG Grid "(Blanks)" row. It is only offered
 * when the column's data actually contains blanks; selecting it keeps blank
 * rows visible, deselecting it hides them. The engine maps blank cell values
 * to this sentinel when checking membership.
 */
export const SET_FILTER_BLANK = '__wayd-blank__'

/** Display label for {@link SET_FILTER_BLANK}. */
export const SET_FILTER_BLANK_LABEL = '(Blanks)'

/**
 * Set-style selection over the distinct *days* present in a date column — the
 * Excel date tree. Values are `YYYY-MM-DD` day keys; the engine normalizes each
 * cell to a day before checking membership, so it matches regardless of the raw
 * cell shape (Date, ISO timestamp, date string). Empty ⇒ "no filter".
 *
 * This is the date counterpart to {@link SetFilterModel}; it is kept distinct so
 * the string-set path (raw `includes`) and the date-set path (day-normalized
 * membership) don't get conflated.
 */
export interface DateSetFilterModel {
  type: 'dateSet'
  /** Selected day keys (`YYYY-MM-DD`); empty means "no filter" (all pass). */
  values: string[]
}

export type ColumnFilterModel =
  | TextFilterModel
  | NumberFilterModel
  | DateFilterModel
  | DateTimeFilterModel
  | SetFilterModel
  | DateSetFilterModel

/**
 * Column-facing filter types — what a column can *declare* / be inferred as.
 * Excludes `dateSet`, which is an internal sub-mode of a `date` column's panel
 * (the date tree), not a type a column resolves to on its own.
 */
export type FilterType = Exclude<ColumnFilterModel['type'], 'dateSet'>

// ─── Operators that need no value ──────────────────────────

const VALUELESS_OPERATORS = new Set<string>(['blank', 'notBlank'])

export const operatorNeedsValue = (op: string): boolean =>
  !VALUELESS_OPERATORS.has(op)

export const operatorNeedsSecondValue = (op: string): boolean =>
  op === 'inRange'

// ─── Emptiness ─────────────────────────────────────────────

/**
 * Whether a single condition is "active" (contributes to filtering).
 * Valueless operators (blank/notBlank) are always active; value operators
 * are active only once they have their required operand(s).
 */
export const isConditionActive = (
  condition:
    | TextCondition
    | NumberCondition
    | DateCondition
    | DateTimeCondition,
): boolean => {
  if (!operatorNeedsValue(condition.op)) return true

  const primaryMissing =
    condition.value === null ||
    condition.value === undefined ||
    condition.value === ''
  if (primaryMissing) return false

  if (operatorNeedsSecondValue(condition.op)) {
    const secondary = (condition as NumberCondition | DateCondition).valueTo
    if (secondary === null || secondary === undefined || secondary === '') {
      return false
    }
  }

  return true
}

/**
 * Whether a whole descriptor is empty (no active conditions / no selected set
 * values). An empty descriptor should be removed from filter state so the
 * column reads as "not filtered".
 */
export const isFilterModelEmpty = (model: ColumnFilterModel): boolean => {
  if (model.type === 'set' || model.type === 'dateSet') {
    return model.values.length === 0
  }
  return !model.conditions.some(isConditionActive)
}

// ─── Defaults ──────────────────────────────────────────────

/** Hard ceiling on conditions per column filter. */
export const CONDITION_LIMIT = 5
/**
 * Default max conditions when a column doesn't set `maxFilterConditions`.
 * Defaults to the hard ceiling; columns may opt down via meta.
 */
export const DEFAULT_MAX_CONDITIONS = CONDITION_LIMIT

export const defaultOperatorFor = (type: FilterType): string => {
  switch (type) {
    case 'text':
      return 'contains'
    case 'number':
      return 'equals'
    case 'date':
    case 'dateTime':
      return 'equals'
    case 'set':
      return 'contains'
  }
}

// ─── Operator options (for popup operator dropdowns) ───────

export interface OperatorOption {
  value: string
  label: string
}

const TEXT_OPERATORS: OperatorOption[] = [
  { value: 'contains', label: 'Contains' },
  { value: 'notContains', label: 'Does not contain' },
  { value: 'equals', label: 'Equals' },
  { value: 'notEqual', label: 'Not equal' },
  { value: 'startsWith', label: 'Starts with' },
  { value: 'endsWith', label: 'Ends with' },
  { value: 'blank', label: 'Blank' },
  { value: 'notBlank', label: 'Not blank' },
]

const NUMBER_OPERATORS: OperatorOption[] = [
  { value: 'equals', label: 'Equals' },
  { value: 'notEqual', label: 'Not equal' },
  { value: 'greaterThan', label: 'Greater than' },
  { value: 'greaterThanOrEqual', label: 'Greater than or equal' },
  { value: 'lessThan', label: 'Less than' },
  { value: 'lessThanOrEqual', label: 'Less than or equal' },
  { value: 'inRange', label: 'In range' },
  { value: 'blank', label: 'Blank' },
  { value: 'notBlank', label: 'Not blank' },
]

const DATE_OPERATORS: OperatorOption[] = [
  { value: 'equals', label: 'Equals' },
  { value: 'notEqual', label: 'Not equal' },
  { value: 'before', label: 'Before' },
  { value: 'after', label: 'After' },
  { value: 'inRange', label: 'In range' },
  { value: 'blank', label: 'Blank' },
  { value: 'notBlank', label: 'Not blank' },
]

/**
 * Resolves a column's declared filter type — including the legacy inline-row
 * aliases (`select` → `set`, `numericRange` → `number`) — to a canonical
 * {@link FilterType}. Unknown/undefined values default to `text`.
 */
export const resolveFilterType = (
  type: string | undefined,
): FilterType => {
  switch (type) {
    case 'select':
    case 'set':
      return 'set'
    case 'numericRange':
    case 'number':
      return 'number'
    case 'date':
      return 'date'
    case 'dateTime':
      return 'dateTime'
    case 'text':
    default:
      return 'text'
  }
}

/** Returns the operator dropdown options for a filter type. */
export const operatorOptionsFor = (type: FilterType): OperatorOption[] => {
  switch (type) {
    case 'text':
      return TEXT_OPERATORS
    case 'number':
      return NUMBER_OPERATORS
    case 'date':
    case 'dateTime':
      return DATE_OPERATORS
    case 'set':
      return []
  }
}

/** Creates an empty descriptor for a given filter type. */
export const createEmptyFilterModel = (type: FilterType): ColumnFilterModel => {
  switch (type) {
    case 'text':
      return { type, conditions: [{ op: 'contains', value: '' }], join: 'AND' }
    case 'number':
      return {
        type,
        conditions: [{ op: 'equals', value: null }],
        join: 'AND',
      }
    case 'date':
      return {
        type,
        conditions: [{ op: 'equals', value: null }],
        join: 'AND',
      }
    case 'dateTime':
      return {
        type,
        conditions: [{ op: 'equals', value: null }],
        join: 'AND',
      }
    case 'set':
      return { type, values: [] }
  }
}

// ─── Condition helpers (pure, for the popup editor) ────────

/**
 * Descriptors driven by AND/OR conditions (text/number/date/dateTime) — i.e.
 * everything except the set-style variants ({@link SetFilterModel} and
 * {@link DateSetFilterModel}), which have `values` instead of `conditions`.
 */
export type ConditionModel = Exclude<
  ColumnFilterModel,
  SetFilterModel | DateSetFilterModel
>
type AnyCondition =
  | TextCondition
  | NumberCondition
  | DateCondition
  | DateTimeCondition

/** Creates a fresh empty condition for a type, using its default operator. */
export const createEmptyCondition = (
  type: ConditionModel['type'],
): AnyCondition => {
  const op = defaultOperatorFor(type)
  if (type === 'text') return { op, value: '' } as TextCondition
  if (type === 'number') return { op, value: null } as NumberCondition
  return { op, value: null } as DateCondition | DateTimeCondition
}

/** Immutably replaces the condition at `index`. */
export const updateConditionAt = (
  model: ConditionModel,
  index: number,
  patch: Partial<AnyCondition>,
): ConditionModel => {
  const conditions = model.conditions.map((c, i) =>
    i === index ? { ...c, ...patch } : c,
  )
  // `model` is a discriminated union; TS widens the spread's `conditions` to the
  // union of all member arrays, so re-narrow the whole object to ConditionModel.
  return { ...model, conditions } as ConditionModel
}

/** Immutably appends an empty condition, respecting `maxConditions`. */
export const addCondition = (
  model: ConditionModel,
  maxConditions: number,
): ConditionModel => {
  if (model.conditions.length >= maxConditions) return model
  const next = [...model.conditions, createEmptyCondition(model.type)]
  return { ...model, conditions: next } as ConditionModel
}

/** Immutably removes the condition at `index` (never below one condition). */
export const removeConditionAt = (
  model: ConditionModel,
  index: number,
): ConditionModel => {
  if (model.conditions.length <= 1) return model
  const next = model.conditions.filter((_, i) => i !== index)
  return { ...model, conditions: next } as ConditionModel
}
