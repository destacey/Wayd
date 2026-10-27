import { IntakeRequestSummaryDto } from '@/src/services/wayd-api'
import {
  DEFAULT_REQUEST_FILTERS,
  activeFilterCount,
  applyRequestFilters,
} from './request-filters'

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
