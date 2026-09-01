import { ReleaseDto } from '@/src/services/wayd-api'
import dayjs from 'dayjs'
import { countReleasedWithin } from './release-cadence'

// The global setup mocks dayjs down to formatting, so subtract and startOf are absent — and date
// arithmetic across a window boundary is the whole of what this covers. Use the real dayjs.
jest.unmock('dayjs')

const release = (overrides: Partial<ReleaseDto> = {}): ReleaseDto =>
  ({
    id: 'r',
    key: 1,
    product: { id: 'p', key: 2, name: 'Checkout' },
    version: '1.0',
    ...overrides,
  }) as ReleaseDto

const daysAgo = (days: number) =>
  dayjs().subtract(days, 'day').format('YYYY-MM-DD') as unknown as Date

describe('countReleasedWithin', () => {
  it('counts a release shipped inside the window', () => {
    // Arrange / Act
    const count = countReleasedWithin([release({ releasedDate: daysAgo(10) })], 90)

    // Assert
    expect(count).toBe(1)
  })

  it('excludes a release shipped before the window', () => {
    // Arrange / Act
    const count = countReleasedWithin([release({ releasedDate: daysAgo(120) })], 90)

    // Assert
    expect(count).toBe(0)
  })

  it('includes a release shipped exactly at the window edge', () => {
    // Arrange / Act — ninety days means the last ninety days, not eighty-nine.
    const count = countReleasedWithin([release({ releasedDate: daysAgo(90) })], 90)

    // Assert
    expect(count).toBe(1)
  })

  it('ignores releases that have not shipped', () => {
    // Arrange — planned and cut are both real states that carry no released date, and cadence
    // measures what reached people rather than what was intended to.
    const releases = [
      release({ releasedDate: undefined }),
      release({ cutDate: daysAgo(5), releasedDate: undefined }),
      release({ releasedDate: daysAgo(5) }),
    ]

    // Act
    const count = countReleasedWithin(releases, 90)

    // Assert
    expect(count).toBe(1)
  })

  it('returns zero when there are no releases at all', () => {
    // Arrange / Act
    expect(countReleasedWithin(undefined, 90)).toBe(0)
    expect(countReleasedWithin([], 90)).toBe(0)
  })
})
