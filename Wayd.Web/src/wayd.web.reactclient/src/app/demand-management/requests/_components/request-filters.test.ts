import { IntakeRequestSummaryDto } from '@/src/services/wayd-api'
import {
  BUILT_IN_VIEWS,
  DEFAULT_REQUEST_FILTERS,
  MY_DESKS,
  RequestFilters,
  activeFilterCount,
  applyRequestFilters,
  fromSearchParams,
  matchingView,
  resolveDeskIds,
  toSearchParams,
} from './request-filters'
import { NOT_RECORDED_CHANNEL } from '../../_components/channel'
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
    tags: [],
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
  it('keeps requests carrying any of the chosen tags', () => {
    // Arrange
    const tag = (id: string, categoryId: string) => ({
      id,
      name: id,
      categoryId,
      categoryName: categoryId,
    })
    const tagged = [
      request('payments', { tags: [tag('t-payments', 'area')] }),
      request('enterprise', { tags: [tag('t-enterprise', 'segment')] }),
      request('untagged'),
    ]

    // Act
    const result = applyRequestFilters(
      tagged,
      { ...DEFAULT_REQUEST_FILTERS, tagIds: ['t-payments', 't-enterprise'] },
      'e-me',
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(['payments', 'enterprise'])
  })

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

  it('keeps requests with no channel for Not recorded', () => {
    // Arrange
    const unrecorded = request('unrecorded', { channel: undefined })

    // Act
    const result = applyRequestFilters(
      [...requests, unrecorded],
      {
        ...DEFAULT_REQUEST_FILTERS,
        channelIds: [NOT_RECORDED_CHANNEL, 'c-sales'],
      },
      'e-me',
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(['nobodys', 'unrecorded'])
  })
})

describe('My desks', () => {
  it('keeps the requests on the desks the reader works', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      { ...DEFAULT_REQUEST_FILTERS, deskIds: [MY_DESKS] },
      'e-me',
      ['d-it'],
    )

    // Assert
    expect(result.map((r) => r.id)).toEqual(['theirs'])
  })

  it('keeps nothing for a reader who works no desk', () => {
    // Act
    const result = applyRequestFilters(
      requests,
      { ...DEFAULT_REQUEST_FILTERS, deskIds: [MY_DESKS] },
      'e-me',
      [],
    )

    // Assert
    expect(result).toEqual([])
  })

  it('expands to the reader’s desks alongside any desk named outright', () => {
    // Act
    const ids = resolveDeskIds([MY_DESKS, 'd-other', 'd-it'], ['d-it', 'd-hr'])

    // Assert
    expect(ids).toEqual(['d-it', 'd-hr', 'd-other'])
  })

  it('travels in the address as "mine", so a shared link means each reader’s own desks', () => {
    // Act
    const query = toSearchParams(
      { ...DEFAULT_REQUEST_FILTERS, deskIds: [MY_DESKS] },
      'list',
    ).toString()

    // Assert
    expect(query).toBe('desk=mine')
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
      tagIds: ['t-1', 't-2'],
      needBy: 'chosen',
      order: 'score',
    }

    // Act
    const read = fromSearchParams(toSearchParams(filters, 'board'))

    // Assert
    expect(read).toEqual({ filters, view: 'board' })
  })

  it('round-trips Not recorded alongside a channel', () => {
    // Arrange
    const filters: RequestFilters = {
      ...DEFAULT_REQUEST_FILTERS,
      channelIds: [NOT_RECORDED_CHANNEL, 'c-1'],
    }

    // Act
    const query = toSearchParams(filters, 'list')
    const read = fromSearchParams(query)

    // Assert
    expect(query.getAll('channel')).toEqual(['none', 'c-1'])
    expect(read.filters.channelIds).toEqual([NOT_RECORDED_CHANNEL, 'c-1'])
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
