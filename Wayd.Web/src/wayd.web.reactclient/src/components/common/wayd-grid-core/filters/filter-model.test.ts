import {
  createEmptyFilterModel,
  defaultOperatorFor,
  isConditionActive,
  isFilterModelEmpty,
  operatorNeedsSecondValue,
  operatorNeedsValue,
  resolveFilterType,
  type FilterType,
} from './filter-model'

describe('filter-model', () => {
  describe('resolveFilterType', () => {
    it('maps legacy inline-row aliases to canonical filter types', () => {
      // Arrange / Act / Assert
      expect(resolveFilterType('select')).toBe('set')
      expect(resolveFilterType('numericRange')).toBe('number')
    })

    it('passes canonical filter types through unchanged', () => {
      // Arrange / Act / Assert
      expect(resolveFilterType('text')).toBe('text')
      expect(resolveFilterType('number')).toBe('number')
      expect(resolveFilterType('date')).toBe('date')
      expect(resolveFilterType('dateTime')).toBe('dateTime')
      expect(resolveFilterType('set')).toBe('set')
    })

    it('defaults undefined / unknown values to text', () => {
      // Arrange / Act / Assert
      expect(resolveFilterType(undefined)).toBe('text')
      expect(resolveFilterType('bogus')).toBe('text')
    })
  })

  describe('operatorNeedsValue', () => {
    it('returns false for blank / notBlank and true otherwise', () => {
      // Arrange / Act / Assert
      expect(operatorNeedsValue('blank')).toBe(false)
      expect(operatorNeedsValue('notBlank')).toBe(false)
      expect(operatorNeedsValue('contains')).toBe(true)
      expect(operatorNeedsValue('inRange')).toBe(true)
    })
  })

  describe('operatorNeedsSecondValue', () => {
    it('is true only for inRange', () => {
      // Arrange / Act / Assert
      expect(operatorNeedsSecondValue('inRange')).toBe(true)
      expect(operatorNeedsSecondValue('equals')).toBe(false)
    })
  })

  describe('isConditionActive', () => {
    it('treats valueless operators as always active', () => {
      // Arrange / Act / Assert
      expect(isConditionActive({ op: 'blank', value: '' })).toBe(true)
      expect(isConditionActive({ op: 'notBlank', value: null })).toBe(true)
    })

    it('is inactive until a value operator has its operand', () => {
      // Arrange / Act / Assert
      expect(isConditionActive({ op: 'contains', value: '' })).toBe(false)
      expect(isConditionActive({ op: 'contains', value: 'x' })).toBe(true)
      expect(isConditionActive({ op: 'greaterThan', value: null })).toBe(false)
      expect(isConditionActive({ op: 'greaterThan', value: 5 })).toBe(true)
    })

    it('requires the second operand for inRange', () => {
      // Arrange / Act / Assert
      expect(isConditionActive({ op: 'inRange', value: 2, valueTo: null })).toBe(
        false,
      )
      expect(isConditionActive({ op: 'inRange', value: 2, valueTo: 6 })).toBe(
        true,
      )
    })
  })

  describe('isFilterModelEmpty', () => {
    it('is empty when a set has no selected values', () => {
      // Arrange / Act / Assert
      expect(isFilterModelEmpty({ type: 'set', values: [] })).toBe(true)
      expect(isFilterModelEmpty({ type: 'set', values: ['x'] })).toBe(false)
    })

    it('is empty when no condition is active', () => {
      // Arrange / Act / Assert
      expect(
        isFilterModelEmpty({
          type: 'text',
          conditions: [{ op: 'contains', value: '' }],
          join: 'AND',
        }),
      ).toBe(true)
      expect(
        isFilterModelEmpty({
          type: 'text',
          conditions: [{ op: 'contains', value: 'x' }],
          join: 'AND',
        }),
      ).toBe(false)
    })
  })

  describe('createEmptyFilterModel', () => {
    it('creates a typed empty descriptor with the default operator', () => {
      // Arrange
      const types: FilterType[] = ['text', 'number', 'date', 'dateTime', 'set']

      // Act / Assert
      types.forEach((type) => {
        const model = createEmptyFilterModel(type)
        expect(model.type).toBe(type)
        expect(isFilterModelEmpty(model)).toBe(true)
        if (model.type !== 'set' && model.type !== 'dateSet') {
          expect(model.conditions[0].op).toBe(defaultOperatorFor(type))
        }
      })
    })
  })
})
