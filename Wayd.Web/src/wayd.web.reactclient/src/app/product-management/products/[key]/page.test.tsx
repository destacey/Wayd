import React, { Suspense } from 'react'
import { act, render, screen } from '@testing-library/react'
import ProductDetailsPage from './page'

// The overview's version tile counts back from today, and the global setup mocks dayjs down to
// formatting — without the real one the section throws while rendering and takes every tile with it.
jest.unmock('dayjs')

const product = {
  id: 'product-1',
  key: 7,
  name: 'Trio VMS',
  description: 'The video management surface.',
  externalId: 'acme/trio-vms',
  type: { id: 'type-1', key: 1, name: 'Application' },
  status: { id: 'status-1', name: 'Concept', category: 1, alias: 0 },
  isReleasable: true,
  parent: { id: 'product-0', key: 1, name: 'Trio WFS' },
  tags: [
    {
      tagId: 'tag-1',
      tagName: 'ios',
      categoryId: 'cat-1',
      categoryName: 'Platform',
    },
  ],
}

const components = [
  {
    ...product,
    id: 'product-2',
    key: 9,
    name: 'Trio VMS Web',
    parent: { id: 'product-1', key: 7, name: 'Trio VMS' },
    tags: [],
  },
]

let mockSearchParams = new URLSearchParams()
const mockReplace = jest.fn((url: string) => {
  mockSearchParams = new URLSearchParams(url.split('?')[1] ?? '')
})

// jsdom has no matchMedia, so useBreakpoint reports nothing and RecordLayout
// renders its compact form. Pin a wide viewport so the rail and facts are shown.
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    Grid: {
      ...actual.Grid,
      useBreakpoint: () => ({ md: true, lg: true, xl: true }),
    },
  }
})

// The markdown editor pulls in ESM-only packages Jest cannot parse; the page only
// renders the read-only renderer, so the barrel is mocked as the other suites do.
jest.mock('@/src/components/common/markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown: string }) => (
    <div data-testid="markdown">{markdown}</div>
  ),
  MarkdownEditor: () => <textarea data-testid="markdown-editor" />,
}))

jest.mock('@/src/components/common/markdown/markdown-editor', () => ({
  __esModule: true,
  default: () => <textarea data-testid="markdown-editor" />,
}))

// The edit form imports antd's ESM TextArea path, as 26 other components do.
// Jest cannot parse it, and changing the app to suit the test would break a
// convention rather than fix anything.
jest.mock('antd/es/input/TextArea', () => ({
  __esModule: true,
  default: (props: Record<string, unknown>) => <textarea {...props} />,
}))

jest.mock('next/navigation', () => ({
  notFound: jest.fn(),
  usePathname: () => '/product-management/products/7',
  useRouter: () => ({ replace: mockReplace, push: jest.fn() }),
  useSearchParams: () => mockSearchParams,
}))

jest.mock('@/src/components/common', () => ({
  // Dividers are rendered as marker elements rather than skipped, so a test can
  // assert how the menu is grouped and not just what it contains.
  PageActions: ({ actionItems }: { actionItems: any[] }) => (
    <div data-testid="page-actions">
      {actionItems.map((item, index) =>
        item.type === 'divider' ? (
          <hr key={item.key ?? index} data-testid="action-divider" />
        ) : (
          <button key={item.key} type="button" onClick={item.onClick}>
            {item.label}
          </button>
        ),
      )}
    </div>
  ),
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasClaim: jest.fn(() => true),
    hasPermissionClaim: jest.fn(() => true),
  }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// Spread the real module: RecordLayout reaches for useLocalStorageState through
// the same barrel, and replacing it wholesale takes that with it.
jest.mock('@/src/hooks', () => ({
  ...jest.requireActual('@/src/hooks'),
  useDocumentTitle: jest.fn(),
  useModalForm: () => ({
    form: { setFieldsValue: jest.fn(), getFieldValue: jest.fn() },
    isOpen: false,
    isValid: true,
    isSaving: false,
    handleOk: jest.fn(),
    handleCancel: jest.fn(),
  }),
}))

jest.mock('@/src/components/hoc', () => ({
  authorizePage: (Component: React.ComponentType<any>) => Component,
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

jest.mock('@/src/store/features/product-management/versions-api', () => ({
  useGetVersionsQuery: () => ({
    data: [],
    isLoading: false,
    refetch: jest.fn(),
  }),
  usePlanVersionMutation: () => [jest.fn()],
  useCutVersionMutation: () => [jest.fn()],
  useMarkVersionReleasedMutation: () => [jest.fn()],
}))

jest.mock('@/src/store/features/product-management/releases-api', () => ({
  useGetReleasesQuery: () => ({
    data: [],
    isLoading: false,
    refetch: jest.fn(),
  }),
  usePlanReleaseMutation: () => [jest.fn()],
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useGetProductQuery: () => ({
    data: product,
    error: undefined,
    isLoading: false,
    refetch: jest.fn(),
  }),
  useGetProductsQuery: () => ({ data: components, isLoading: false }),
  useGetProductStatusOptionsQuery: () => ({ data: [], isLoading: false }),
  useChangeProductStatusMutation: () => [jest.fn()],
  useRetypeProductMutation: () => [jest.fn()],
  useReparentProductMutation: () => [jest.fn()],
  useCreateProductMutation: () => [jest.fn()],
  useUpdateProductMutation: () => [jest.fn()],
  useDeleteProductMutation: () => [jest.fn()],
}))

// The page reads its params with use(), which suspends on first render — so the
// render has to be awaited inside act, or the tree never resolves.
const renderPage = async () =>
  await act(async () =>
    render(
      <Suspense fallback={<div>Loading product</div>}>
        <ProductDetailsPage params={Promise.resolve({ key: '7' })} />
      </Suspense>,
    ),
  )

describe('ProductDetailsPage', () => {
  beforeEach(() => {
    mockSearchParams = new URLSearchParams()
  })

  it('shows the product name and key', async () => {
    // Arrange / Act
    await renderPage()

    // Assert
    expect(await screen.findByText('Trio VMS')).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()
  })

  it('links up to the parent rather than the list', async () => {
    // A component's parent is the more useful way back than the flat list, and
    // it is what makes the hierarchy navigable from either end.
    // Arrange / Act
    await renderPage()

    // Assert
    const parentLink = await screen.findByRole('link', { name: 'Trio WFS' })
    expect(parentLink).toHaveAttribute(
      'href',
      '/product-management/products/1',
    )
  })

  it('offers each guarded change as its own action', async () => {
    // Status, type and parent each carry a rule the API enforces. Folding them
    // into Edit would hide which one refused a change.
    // Arrange / Act
    await renderPage()

    // Assert
    for (const action of [
      'Edit',
      'Change Status',
      'Change Type',
      'Move',
      'Manage Tags',
      'Link Externally',
    ]) {
      expect(await screen.findByText(action)).toBeInTheDocument()
    }
  })

  it('shows the tags in the header, where they are read', async () => {
    // Behind a menu they are invisible until someone goes looking; the header is
    // where a reader already is.
    // Arrange / Act
    await renderPage()

    // Assert
    expect(await screen.findByText('ios')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Manage tags' }),
    ).toBeInTheDocument()
  })

  it('groups the actions by what a change means', async () => {
    // Record actions, then the three guarded changes, then labels — two dividers
    // between three groups, and none stranded at either edge.
    // Arrange / Act
    await renderPage()

    // Assert
    const menu = await screen.findByTestId('page-actions')
    expect(screen.getAllByTestId('action-divider')).toHaveLength(2)
    expect(menu.firstChild).not.toHaveAttribute('data-testid', 'action-divider')
    expect(menu.lastChild).not.toHaveAttribute('data-testid', 'action-divider')
  })

  it('summarises the child products on the overview', async () => {
    // The tile reads the same query the section does, so it cannot disagree with
    // the list it summarises.
    // Arrange / Act
    await renderPage()

    // Assert
    expect(await screen.findByText('Releasable Products')).toBeInTheDocument()
  })

  it('summarises recent versions on the overview', async () => {
    // The tile counts back from today, so it renders only with the real dayjs — the global mock
    // leaves it throwing mid-render and takes the whole section down with it.
    // Arrange / Act
    await renderPage()

    // Assert
    expect(await screen.findByText('Releases (90d)')).toBeInTheDocument()
  })

  it('closes the breadcrumb with the page name, not an ancestor', async () => {
    // Arrange / Act
    await renderPage()

    // Assert
    expect(await screen.findByText('Product Details')).toBeInTheDocument()
  })

  it('links back to the products list as the first crumb', async () => {
    // Outermost first: the list, then the parent. Replacing the list with the
    // parent would leave a nested product with no way back to the top.
    await renderPage()

    // Assert
    const listLink = await screen.findByRole('link', { name: 'Products' })
    expect(listLink).toHaveAttribute('href', '/product-management/products')
  })

  it('counts the child products on their section', async () => {
    // Whether a product has parts is the first thing a reader wants; opening an
    // empty section to find out is worse than carrying the count.
    // Arrange / Act
    await renderPage()

    // Assert
    // "Products" appears as a breadcrumb and an overview tile too, so the count
    // is read off the section entry rather than by text alone.
    const sectionEntry = await screen.findByRole('tab', { name: /Products/ })
    expect(sectionEntry).toHaveTextContent('1')
  })

  it('offers adding a child product from the products section', async () => {
    // Arrange
    mockSearchParams = new URLSearchParams('section=products')

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Add Product' }),
    ).toBeInTheDocument()
  })

  it('does not offer it on the overview', async () => {
    // sectionActions renders for whichever section is open, so an unconditional
    // action shows up on all of them.
    // Arrange / Act — no ?section=, so Overview is active
    await renderPage()

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Add Product' }),
    ).not.toBeInTheDocument()
  })

  it('offers a Releases section, ahead of the versions beneath it', async () => {
    // What customers were told about this product is the product-side question; versions are the
    // engineering record under it, so the announcement reads first.
    // Arrange / Act
    await renderPage()

    // Assert
    const tabs = await screen.findAllByRole('tab')
    const labels = tabs.map((tab) => tab.textContent ?? '')
    const releasesAt = labels.findIndex((label) => label.includes('Releases'))
    const versionsAt = labels.findIndex((label) => label.includes('Versions'))

    expect(releasesAt).toBeGreaterThan(-1)
    expect(versionsAt).toBeGreaterThan(releasesAt)
  })

  it('offers adding a release from the releases section', async () => {
    // Arrange
    mockSearchParams = new URLSearchParams('section=releases')

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Add Release' }),
    ).toBeInTheDocument()
  })
})
