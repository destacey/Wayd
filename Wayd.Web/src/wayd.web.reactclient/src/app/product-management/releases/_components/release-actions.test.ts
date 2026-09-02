import { ReleaseDto, StatusCategory } from '@/src/services/wayd-api'
import { outstandingContents, releaseActionAvailability } from './release-actions'

const release = (overrides: Partial<ReleaseDto> = {}): ReleaseDto =>
  ({
    id: '11111111-1111-1111-1111-111111111111',
    key: 1,
    version: '2026.07',
    status: {
      id: 's',
      name: 'Planned',
      category: StatusCategory.Proposed,
      alias: 0,
    },
    versions: [],
    packages: [],
    ...overrides,
  }) as ReleaseDto

const planned = () => release()

const announced = () =>
  release({
    releasedDate: '2026-07-31' as unknown as Date,
    status: {
      id: 's',
      name: 'Released',
      category: StatusCategory.Done,
      alias: 11,
    },
  })

const withdrawn = () =>
  release({
    status: {
      id: 's',
      name: 'Withdrawn',
      category: StatusCategory.Removed,
      alias: 12,
    },
  })

const carriedVersion = (releasedDate?: string) => ({
  version: { id: 'v1', key: 5, name: '1.2.0' },
  product: { id: 'p1', key: 6, name: '@wayd/mcp' },
  releasedDate: releasedDate as unknown as Date | undefined,
})

const shippedPackage = (releasedDate?: string) => ({
  package: { id: 'pk1', key: 7, name: 'WAYD-2026.09.1' },
  releasedDate: releasedDate as unknown as Date | undefined,
})

describe('releaseActionAvailability', () => {
  it('offers every move on a planned release', () => {
    // Arrange / Act
    const available = releaseActionAvailability(planned())

    // Assert
    expect(available).toEqual({
      canEditContents: true,
      canRelease: true,
      canWithdraw: true,
      canMoveTargetDate: true,
      canCorrectDates: true,
      // Nothing has been announced, so there is no announcement to take back.
      canRevert: false,
    })
  })

  it('freezes the contents of an announced release', () => {
    // Arrange / Act
    const available = releaseActionAvailability(announced())

    // Assert
    // What was announced is a matter of record once customers have been told.
    expect(available.canEditContents).toBe(false)
    expect(available.canRelease).toBe(false)
    // Still retractable, and still revertible if the announcement never actually happened.
    expect(available.canWithdraw).toBe(true)
    expect(available.canRevert).toBe(true)
  })

  it('refuses everything but retraction on a withdrawn release', () => {
    // Arrange / Act
    const available = releaseActionAvailability(withdrawn())

    // Assert
    expect(available).toEqual({
      canEditContents: false,
      canRelease: false,
      // Already withdrawn: the domain refuses a second withdrawal.
      canWithdraw: false,
      canMoveTargetDate: false,
      canCorrectDates: false,
      canRevert: false,
    })
  })

  it('still allows editing contents in a Done status with no announced date', () => {
    // Arrange
    // The contents freeze keys on the announced date, not the status category — a release parked in a
    // Done status was never announced, so its contents are still a plan.
    const sut = release({
      status: { id: 's', name: 'Complete', category: StatusCategory.Done, alias: 0 },
    })

    // Act
    const available = releaseActionAvailability(sut)

    // Assert
    expect(available.canEditContents).toBe(true)
    expect(available.canRelease).toBe(true)
    // Moving the target date is the one action reading the category instead, matching the aggregate.
    expect(available.canMoveTargetDate).toBe(false)
  })
})

describe('outstandingContents', () => {
  it('finds nothing on an empty release', () => {
    // Arrange / Act
    const outstanding = outstandingContents(planned())

    // Assert
    // An empty release is legitimate and never blocks announcing: a repackaging or a pricing change
    // is announced with nothing deployed.
    expect(outstanding.total).toBe(0)
  })

  it('finds nothing when every entry has shipped', () => {
    // Arrange
    const sut = release({
      versions: [carriedVersion('2026-04-05')],
      packages: [shippedPackage('2026-04-01')],
    } as Partial<ReleaseDto>)

    // Act
    const outstanding = outstandingContents(sut)

    // Assert
    expect(outstanding.total).toBe(0)
  })

  it('names the entries that have not shipped, by route', () => {
    // Arrange
    const sut = release({
      versions: [carriedVersion(), carriedVersion('2026-04-05')],
      packages: [shippedPackage()],
    } as Partial<ReleaseDto>)

    // Act
    const outstanding = outstandingContents(sut)

    // Assert
    // Named rather than counted, so the form can say which entries are blocking instead of relaying
    // the API's generic refusal.
    expect(outstanding.total).toBe(2)
    expect(outstanding.packages.map((p) => p.label)).toEqual(['WAYD-2026.09.1'])
    // The product qualifies the number, which says little on its own.
    expect(outstanding.versions.map((v) => v.label)).toEqual(['@wayd/mcp 1.2.0'])
  })

  it('falls back to the version number when the entry carries no product', () => {
    // Arrange
    const sut = release({
      versions: [{ version: { id: 'v1', key: 5, name: '1.2.0' } }],
    } as Partial<ReleaseDto>)

    // Act
    const outstanding = outstandingContents(sut)

    // Assert
    expect(outstanding.versions.map((v) => v.label)).toEqual(['1.2.0'])
  })
})
