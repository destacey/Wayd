import { VersionDto } from '@/src/services/wayd-api'
import dayjs from 'dayjs'
import { countReleasedWithin } from './version-cadence'

// The global setup mocks dayjs down to formatting, so subtract and startOf are absent — and date
// arithmetic across a window boundary is the whole of what this covers. Use the real dayjs.
jest.unmock('dayjs')

const version = (overrides: Partial<VersionDto> = {}): VersionDto =>
  ({
    id: 'r',
    key: 1,
    product: { id: 'p', key: 2, name: 'Checkout' },
    version: '1.0',
    ...overrides,
  }) as VersionDto

const daysAgo = (days: number) =>
  dayjs().subtract(days, 'day').format('YYYY-MM-DD') as unknown as Date

describe('countReleasedWithin', () => {
  it('counts a version shipped inside the window', () => {
    // Arrange / Act
    const count = countReleasedWithin([version({ releasedDate: daysAgo(10) })], 90)

    // Assert
    expect(count).toBe(1)
  })

  it('excludes a version shipped before the window', () => {
    // Arrange / Act
    const count = countReleasedWithin([version({ releasedDate: daysAgo(120) })], 90)

    // Assert
    expect(count).toBe(0)
  })

  it('includes a version shipped exactly at the window edge', () => {
    // Arrange / Act — ninety days means the last ninety days, not eighty-nine.
    const count = countReleasedWithin([version({ releasedDate: daysAgo(90) })], 90)

    // Assert
    expect(count).toBe(1)
  })

  it('ignores versions that have not shipped', () => {
    // Arrange — planned and cut are both real states that carry no released date, and cadence
    // measures what reached people rather than what was intended to.
    const versions = [
      version({ releasedDate: undefined }),
      version({ cutDate: daysAgo(5), releasedDate: undefined }),
      version({ releasedDate: daysAgo(5) }),
    ]

    // Act
    const count = countReleasedWithin(versions, 90)

    // Assert
    expect(count).toBe(1)
  })

  it('returns zero when there are no versions at all', () => {
    // Arrange / Act
    expect(countReleasedWithin(undefined, 90)).toBe(0)
    expect(countReleasedWithin([], 90)).toBe(0)
  })
})
