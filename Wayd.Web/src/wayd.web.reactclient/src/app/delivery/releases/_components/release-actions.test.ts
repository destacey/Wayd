import { ReleaseDto, StatusCategory } from '@/src/services/wayd-api'
import { releaseActionAvailability } from './release-actions'

const release = (overrides: Partial<ReleaseDto> = {}): ReleaseDto =>
  ({
    id: '11111111-1111-1111-1111-111111111111',
    key: 1,
    product: { id: 'p', key: 2, name: 'Checkout' },
    version: '1.0',
    status: {
      id: 's',
      name: 'Planned',
      category: StatusCategory.Proposed,
      alias: 0,
    },
    ...overrides,
  }) as ReleaseDto

const planned = () => release()

const cut = () =>
  release({
    cutDate: '2026-04-01' as unknown as Date,
    status: { id: 's', name: 'Ready', category: StatusCategory.Active, alias: 10 },
  })

const released = () =>
  release({
    cutDate: '2026-04-01' as unknown as Date,
    releasedDate: '2026-04-02' as unknown as Date,
    status: { id: 's', name: 'Released', category: StatusCategory.Done, alias: 11 },
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

describe('releaseActionAvailability', () => {
  it('offers every move on a planned release', () => {
    // Arrange / Act
    const available = releaseActionAvailability(planned())

    // Assert
    expect(available).toEqual({
      canCut: true,
      canRelease: true,
      canWithdraw: true,
      canMoveTargetDate: true,
      // Offered even with nothing recorded: setting a target date is a correction, not a move.
      canCorrectDates: true,
      // Nothing has shipped, so there is no release to take back.
      canRevert: false,
    })
  })

  it('refuses a second cut', () => {
    // Arrange / Act — cutting is one-way; the aggregate refuses a release already cut.
    const available = releaseActionAvailability(cut())

    // Assert
    expect(available.canCut).toBe(false)
    expect(available.canRelease).toBe(true)
  })

  it('refuses cutting or re-releasing a released release, but still allows withdrawing it', () => {
    // Arrange / Act — pulling something after it shipped is the case Withdraw exists for.
    const available = releaseActionAvailability(released())

    // Assert
    expect(available.canCut).toBe(false)
    expect(available.canRelease).toBe(false)
    expect(available.canMoveTargetDate).toBe(false)
    expect(available.canWithdraw).toBe(true)
  })

  it('offers nothing on a withdrawn release', () => {
    // Arrange / Act
    const available = releaseActionAvailability(withdrawn())

    // Assert
    expect(available).toEqual({
      canCut: false,
      canRelease: false,
      canWithdraw: false,
      canMoveTargetDate: false,
      canCorrectDates: false,
      canRevert: false,
    })
  })

  it('refuses moving the target date once a release is done, even with no cut date', () => {
    // Arrange — a release can reach Done without Wayd holding a cut date, since cutting is not a
    // prerequisite for releasing.
    const doneWithoutCut = release({
      releasedDate: '2026-04-02' as unknown as Date,
      status: { id: 's', name: 'Released', category: StatusCategory.Done, alias: 11 },
    })

    // Act
    const available = releaseActionAvailability(doneWithoutCut)

    // Assert
    expect(available.canMoveTargetDate).toBe(false)
    expect(available.canCut).toBe(false)
  })

  it('offers correcting dates on a released release, which every other action refuses', () => {
    // Arrange / Act -- a typo outlives the lifecycle, and the alternative was to withdraw the
    // release and release it again, writing two status changes that never happened.
    const available = releaseActionAvailability(released())

    // Assert
    expect(available.canCorrectDates).toBe(true)
  })

  it('offers correcting dates on a cut release', () => {
    // Arrange / Act
    const available = releaseActionAvailability(cut())

    // Assert
    expect(available.canCorrectDates).toBe(true)
  })

  it('refuses correcting dates on a withdrawn release', () => {
    // Arrange -- withdrawn is the one terminal state the aggregate refuses a correction in.
    const withdrawnAfterRelease = release({
      cutDate: '2026-04-01' as unknown as Date,
      releasedDate: '2026-04-02' as unknown as Date,
      status: {
        id: 's',
        name: 'Withdrawn',
        category: StatusCategory.Removed,
        alias: 12,
      },
    })

    // Act
    const available = releaseActionAvailability(withdrawnAfterRelease)

    // Assert
    expect(available.canCorrectDates).toBe(false)
  })

  it('offers correcting dates on a planned release with nothing recorded', () => {
    // Arrange / Act — setting a target date is a correction rather than a lifecycle move, so there
    // is nothing to refuse. Requiring an existing date left no way to record one at all.
    const available = releaseActionAvailability(planned())

    // Assert
    expect(available.canCorrectDates).toBe(true)
  })

  it('offers reverting only once a release has shipped', () => {
    // Arrange / Act / Assert — reverting takes back a released date, so there must be one.
    expect(releaseActionAvailability(released()).canRevert).toBe(true)
    expect(releaseActionAvailability(cut()).canRevert).toBe(false)
    expect(releaseActionAvailability(planned()).canRevert).toBe(false)
  })

  it('refuses reverting a withdrawn release', () => {
    // Arrange — withdrawing is terminal. A release pulled after shipping is not the mistaken-record
    // case reverting exists for, and the aggregate refuses it.
    const withdrawnAfterRelease = release({
      cutDate: '2026-04-01' as unknown as Date,
      releasedDate: '2026-04-02' as unknown as Date,
      status: {
        id: 's',
        name: 'Withdrawn',
        category: StatusCategory.Removed,
        alias: 12,
      },
    })

    // Act
    const available = releaseActionAvailability(withdrawnAfterRelease)

    // Assert
    expect(available.canRevert).toBe(false)
  })

  it('offers both withdrawing and reverting on a released release', () => {
    // Arrange / Act — they answer different questions, so a reader has to be able to choose:
    // withdrawing says a real release was pulled, reverting says it never shipped.
    const available = releaseActionAvailability(released())

    // Assert
    expect(available.canWithdraw).toBe(true)
    expect(available.canRevert).toBe(true)
  })
})
