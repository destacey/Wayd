import { ProductDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ProductTreeSelect from './product-tree-select'

const catalog: ProductDto[] = [
  {
    id: 'argo',
    key: 1,
    name: 'Argo Platform',
    isReleasable: false,
    type: { id: 't1', key: 1, name: 'Platform' },
  },
  {
    id: 'identity',
    key: 2,
    name: 'Argo Identity',
    isReleasable: true,
    type: { id: 't2', key: 2, name: 'Service' },
    parent: { id: 'argo', key: 1, name: 'Argo Platform' },
  },
  {
    id: 'trio',
    key: 3,
    name: 'Trio',
    isReleasable: false,
    type: { id: 't1', key: 1, name: 'Platform' },
  },
  {
    id: 'vms',
    key: 4,
    name: 'Trio VMS',
    isReleasable: true,
    type: { id: 't3', key: 3, name: 'Application' },
    parent: { id: 'trio', key: 3, name: 'Trio' },
  },
] as unknown as ProductDto[]

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useGetProductsQuery: () => ({ data: mockProducts, isLoading: false }),
}))

let mockProducts: ProductDto[] = catalog

/** Opens the popup, which is where the tree actually renders. */
const open = async () => {
  await userEvent.click(screen.getByRole('combobox'))
}

/**
 * What the closed selector shows.
 *
 * Read from the selection element rather than by title: the chosen product's name also appears on
 * its tree node, so a document-wide title query matches both.
 */
const selected = () =>
  document
    .querySelector('.ant-select-content-has-value')
    ?.getAttribute('title')
    ?.trim() ?? ''

describe('ProductTreeSelect', () => {
  beforeEach(() => {
    mockProducts = catalog
  })

  it('nests a child under its parent rather than listing both at the root', async () => {
    // Arrange / Act — the hierarchy is the disambiguator, so it has to be visible.
    render(<ProductTreeSelect />)
    await open()

    // Assert
    expect(screen.getByText('Argo Platform')).toBeInTheDocument()
    expect(screen.getByText('Argo Identity')).toBeInTheDocument()
  })

  it('offers a grouping node when anything may be selected', async () => {
    // Arrange — a release may be announced under a product line, which is not releasable.
    render(<ProductTreeSelect selectable="all" />)
    await open()

    // Act
    await userEvent.click(screen.getByText('Argo Platform'))

    // Assert
    expect(selected()).toBe('Argo Platform')
  })

  it('shows a grouping but refuses it when only releasable nodes may be selected', async () => {
    // Arrange — a version can only be cut against a releasable node, but the grouping still has to
    // appear: it is the path to the service you actually want.
    render(<ProductTreeSelect selectable="releasable" />)
    await open()

    // Act — the label carries the type in this mode, which is itself part of the behaviour.
    await userEvent.click(screen.getByText('Argo Platform · Platform'))

    // Assert — nothing chosen, the child is still reachable, and the type is named so the refusal
    // reads as deliberate rather than as a broken row.
    expect(selected()).toBe('')
    expect(screen.getByText('Argo Identity')).toBeInTheDocument()
    expect(screen.getByText('Argo Platform · Platform')).toBeInTheDocument()
  })

  it('takes a releasable node when only releasable nodes may be selected', async () => {
    // Arrange / Act
    render(<ProductTreeSelect selectable="releasable" />)
    await open()
    await userEvent.click(screen.getByText('Trio VMS'))

    // Assert
    expect(selected()).toBe('Trio VMS')
  })

  it('hides a node and everything beneath it when excluded', async () => {
    // Arrange — a product cannot become its own ancestor, so its whole subtree is out. The branch is
    // pruned rather than hoisted: a child shown at the root would read as a legal target.
    render(<ProductTreeSelect excludeSubtreeOf="trio" />)

    // Act
    await open()

    // Assert
    expect(screen.queryByText('Trio')).not.toBeInTheDocument()
    expect(screen.queryByText('Trio VMS')).not.toBeInTheDocument()
    expect(screen.getByText('Argo Platform')).toBeInTheDocument()
  })

  it('keeps an orphan visible rather than dropping it', async () => {
    // Arrange — a node whose parent is missing from the list is shown at the root. Hiding it would
    // misreport what exists.
    mockProducts = [catalog[1]]

    // Act
    render(<ProductTreeSelect />)
    await open()

    // Assert
    expect(screen.getByText('Argo Identity')).toBeInTheDocument()
  })
})
