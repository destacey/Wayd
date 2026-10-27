import { IntakeRequestSummaryDto } from '@/src/services/wayd-api'
import {
  BUILT_IN_VIEWS,
  DEFAULT_REQUEST_FILTERS,
  RequestFilters,
  activeFilterCount,
  applyRequestFilters,
  fromSearchParams,
  matchingView,
  toSearchParams,
} from './request-filters'
import { NeedByBasis, NeedByBasisFamily } from '@/src/services/wayd-api'

const request = (
  id: string,
  overrides: Partial<IntakeRequestSummaryDto> = {},
): IntakeRequestSummaryDto =>
  ({
    id,
    key: 1,
    title: id,
    status: { id: 's-1', name: 'Submitted', category: 'Proposed' },
    desk: { id: 'd-product', key: 1, name: 'Product' },
    requester: { id: 'e-r', key: 1, name: 'Requester' },
    channel: { id: 'c-support', name: 'Support' },
    submittedOn: new Date('2026-10-01T00:00:00Z'),
    statusChangedOn: new Date('2026-10-01T00:00:00Z'),
    ...overrides,
  }) as IntakeRequestSummaryDto

const requests = [
  request('mine', { assignee: { id: 'e-me', key: 1, name: 'Me' } }),
  request('theirs', {
    assignee: { id: 'e-other', key: 2, name: 'Other' },
    desk: { id: 'd-it', key: 2, name: 'IT' },
  }),
  request('nobodys', {
    status: { id: 's-2', name: 'In Review', category: 'Proposed' } as any,
    channel: { id: 'c-sales', name: 'Sales' },
    needBy: {
      date: '2026-12-31',
      basis: NeedByBasis.Regulatory,
      family: NeedByBasisFamily.FixedForUs,
      reference: 'PCI DSS',
    },
  }),
]

describe('applyRequestFilters', () => {
  it('keeps everything with the default filters', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      DEFAULT_REQUEST_FILTERS,
      'e-me',
    )

    // Assert
    expect(result).toHaveLength(3)
  })

  it('keeps only the reader’s requests for Assigned to me', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      { ...DEFAULT_REQUEST_FILTERS, assignee: 'me' },
      'e-me',
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(['mine'])
  })

  it('keeps nothing for Assigned to me when the reader has no employee record', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      { ...DEFAULT_REQUEST_FILTERS, assignee: 'me' },
      null,
    )

    // Assert
    expect(result).toEqual([])
  })

  it('keeps unassigned requests for Not assigned', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      { ...DEFAULT_REQUEST_FILTERS, assignee: 'unassigned' },
      'e-me',
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(['nobodys'])
  })

  it('combines desk, status and channel filters', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      {
        ...DEFAULT_REQUEST_FILTERS,
        deskIds: ['d-product'],
        statusNames: ['In Review'],
        channelIds: ['c-sales'],
      },
      'e-me',
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(['nobodys'])
  })
})

describe('the need-by filter', () => {
  it.each([
    ['fixed', ['nobodys']],
    ['chosen', []],
    ['none', ['mine', 'theirs']],
  ] as const)('keeps %s dates', (needBy, expected) => {
    // Act
    const result = applyRequestFilters(
      requests,
      { ...DEFAULT_REQUEST_FILTERS, needBy },
      'e-me',
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(expected)
  })
})

describe('matchingView', () => {
  it('names the built-in view the filters are', () => {
    // Act
    const view = matchingView({ ...DEFAULT_REQUEST_FILTERS, assignee: 'me' })

    // Assert
    expect(view?.name).toBe('Assigned to me')
  })

  it('names none once a filter has been changed', () => {
    // Act
    const view = matchingView({
      ...DEFAULT_REQUEST_FILTERS,
      assignee: 'me',
      deskIds: ['d-1'],
    })

    // Assert
    expect(view).toBeUndefined()
  })

  it('has a distinct filter set for every built-in view', () => {
    // Act
    const matched = BUILT_IN_VIEWS.map((v) => matchingView(v.filters)?.id)

    // Assert
    expect(matched).toEqual(BUILT_IN_VIEWS.map((v) => v.id))
  })
})

describe('the address', () => {
  it('round-trips filters and the layout', () => {
    // Arrange
    const filters: RequestFilters = {
      scope: 'all',
      deskIds: ['d-1', 'd-2'],
      statusNames: ['In Review'],
      assignee: 'unassigned',
      channelIds: ['c-1'],
      needBy: 'chosen',
    }

    // Act
    const read = fromSearchParams(toSearchParams(filters, 'board'))

    // Assert
    expect(read).toEqual({ filters, view: 'board' })
  })

  it('leaves defaults out of the address', () => {
    // Act
    const query = toSearchParams(DEFAULT_REQUEST_FILTERS, 'list').toString()

    // Assert
    expect(query).toBe('')
  })

  it('falls back to the defaults for values it does not know', () => {
    // Act
    const read = fromSearchParams(
      new URLSearchParams('scope=nope&assignee=everyone&view=grid'),
    )

    // Assert
    expect(read).toEqual({ filters: DEFAULT_REQUEST_FILTERS, view: 'list' })
  })
})

describe('activeFilterCount', () => {
  it('counts each filter that differs from the default', () => {
    // Act
    const count = activeFilterCount({
      ...DEFAULT_REQUEST_FILTERS,
      scope: 'all',
      deskIds: ['d-1', 'd-2'],
      assignee: 'me',
    })

    // Assert
    expect(count).toBe(3)
  })
})
