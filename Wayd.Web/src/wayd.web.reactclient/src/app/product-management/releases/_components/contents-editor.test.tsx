import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  ManifestEntryKind,
  ReleasePackageDto,
  VersionDto,
} from '@/src/services/wayd-api'
import ContentsEditor, { type ContentsDraft } from './contents-editor'

const version = (id: string, number: string, product: string): VersionDto =>
  ({
    id,
    key: 1,
    number,
    product: { id: `p-${id}`, key: 2, name: product },
  }) as VersionDto

const pkg = (
  id: string,
  packageVersion: string,
  componentVersionIds: (string | undefined)[],
): ReleasePackageDto =>
  ({
    id,
    key: 3,
    version: packageVersion,
    components: componentVersionIds.map((versionId) => ({
      product: { id: 'p', key: 4, name: 'Wayd API' },
      versionRecord: versionId
        ? { id: versionId, key: 5, name: '4.10.0' }
        : undefined,
      version: '4.10.0',
      kind: ManifestEntryKind.Changed,
    })),
  }) as ReleasePackageDto

const versions = [
  version('v1', '4.10.0', 'Wayd API'),
  version('v2', '1.2.0', '@wayd/mcp'),
]

const renderEditor = (
  value: ContentsDraft,
  packages: ReleasePackageDto[],
  onChange = jest.fn(),
) => {
  render(
    <ContentsEditor
      value={value}
      onChange={onChange}
      versions={versions}
      packages={packages}
    />,
  )
  return onChange
}

/** The version picker is the second combobox; packages come first in the form. */
const openVersionPicker = async (user: ReturnType<typeof userEvent.setup>) => {
  const comboboxes = screen.getAllByRole('combobox')
  await user.click(comboboxes[1])
}

describe('ContentsEditor', () => {
  it('disables a version that a selected package already ships', async () => {
    // Arrange
    const user = userEvent.setup()
    renderEditor({ versionIds: [], packageIds: ['pk1'] }, [
      pkg('pk1', 'WAYD-2026.09.1', ['v1']),
    ])

    // Act
    await openVersionPicker(user)

    // Assert
    // The rule is surfaced before submit rather than arriving as a 400, and the covering package is
    // named so "why not?" needs no second lookup.
    const covered = await screen.findByTitle(/Wayd API 4\.10\.0 — in WAYD-2026\.09\.1/)
    expect(covered).toHaveAttribute('aria-disabled', 'true')
  })

  it('leaves a version selectable when no package covering it is selected', async () => {
    // Arrange
    // The same package exists but is not part of this release, so there is nothing to double-count.
    const user = userEvent.setup()
    renderEditor({ versionIds: [], packageIds: [] }, [
      pkg('pk1', 'WAYD-2026.09.1', ['v1']),
    ])

    // Act
    await openVersionPicker(user)

    // Assert
    const option = await screen.findByTitle('Wayd API 4.10.0')
    expect(option).not.toHaveAttribute('aria-disabled', 'true')
  })

  it('ignores a manifest line that names no version record', async () => {
    // Arrange
    // A carried-forward line holding only a version string covers nothing, so the API would accept
    // the version being carried directly — disabling it here would refuse something legitimate.
    const user = userEvent.setup()
    renderEditor({ versionIds: [], packageIds: ['pk1'] }, [
      pkg('pk1', 'WAYD-2026.09.1', [undefined]),
    ])

    // Act
    await openVersionPicker(user)

    // Assert
    const option = await screen.findByTitle('Wayd API 4.10.0')
    expect(option).not.toHaveAttribute('aria-disabled', 'true')
  })

  it('keeps an already-carried version selectable so it can be removed', async () => {
    // Arrange
    // Both routes name the version, which is the conflict. Locking the option would strand the form
    // in a state it could not leave.
    const user = userEvent.setup()
    renderEditor({ versionIds: ['v1'], packageIds: ['pk1'] }, [
      pkg('pk1', 'WAYD-2026.09.1', ['v1']),
    ])

    // Act
    await openVersionPicker(user)

    // Assert
    // Queried by role rather than title: a selected value renders its label twice, once in the
    // selection pill and once in the dropdown, and only the latter is the option.
    const option = await screen.findByRole('option', {
      name: /Wayd API 4\.10\.0 — in WAYD-2026\.09\.1/,
    })
    expect(option).toHaveAttribute('aria-disabled', 'false')
  })

  it('reports the conflict when both routes carry one version', () => {
    // Arrange / Act
    renderEditor({ versionIds: ['v1'], packageIds: ['pk1'] }, [
      pkg('pk1', 'WAYD-2026.09.1', ['v1']),
    ])

    // Assert
    // Named on both sides, so either can be dropped to clear it.
    expect(screen.getByText('A version is carried twice')).toBeInTheDocument()
    expect(
      screen.getByText(/is carried directly and also ships inside WAYD-2026\.09\.1/),
    ).toBeInTheDocument()
  })

  it('reports no conflict once the covering package is dropped', () => {
    // Arrange / Act
    // Coverage recomputes from the current selection, so deselecting the package frees the version
    // within the same form — which is what makes moving between routes one change of mind.
    renderEditor({ versionIds: ['v1'], packageIds: [] }, [
      pkg('pk1', 'WAYD-2026.09.1', ['v1']),
    ])

    // Assert
    expect(screen.queryByText('A version is carried twice')).not.toBeInTheDocument()
  })
})
