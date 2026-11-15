'use client'

import {
  FC,
  memo,
  useEffect,
  useRef,
  useState,
} from 'react'
import {
  Divider,
  Empty,
  Flex,
  Input,
  Modal,
  Radio,
  Segmented,
  Spin,
  Tabs,
  Typography,
} from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { useRouter } from 'next/navigation'
import { useLazyGlobalSearchQuery } from '@/src/store/features/search/search-api'
import {
  GlobalSearchCategoryDto,
  GlobalSearchResultDto,
  GlobalSearchResultItemDto,
} from '@/src/services/api/wayd.schemas'
import { getSearchResultUrl } from './search-result-url'
import styles from './global-search-modal.module.css'

const { Title, Text } = Typography
const { Group: RadioGroup } = Radio

type SearchScope = 'app' | 'docs'

interface DocSearchEntry {
  slug: string
  title: string
  description: string
  content: string
}

function searchDocsLocally(
  entries: DocSearchEntry[],
  query: string,
  maxResults: number,
): GlobalSearchResultDto {
  if (!query || query.length < 2) return { categories: [] }

  const terms = query.toLowerCase().split(/\s+/).filter(Boolean)
  const scored: { entry: DocSearchEntry; score: number }[] = []

  for (const entry of entries) {
    const titleLower = entry.title.toLowerCase()
    const descLower = entry.description.toLowerCase()
    const contentLower = entry.content.toLowerCase()

    let score = 0
    for (const term of terms) {
      if (titleLower.includes(term)) score += 10
      if (descLower.includes(term)) score += 5
      if (contentLower.includes(term)) score += 1
    }

    if (score > 0) {
      scored.push({ entry, score })
    }
  }

  scored.sort((a, b) => b.score - a.score)

  const items: GlobalSearchResultItemDto[] = scored
    .slice(0, maxResults)
    .map(({ entry }) => ({
      title: entry.title,
      subtitle: entry.description,
      key: entry.slug,
      entityType: 'Doc',
    }))

  if (items.length === 0) return { categories: [] }

  return {
    categories: [
      {
        name: 'Documentation',
        slug: 'docs',
        items,
        totalCount: scored.length,
      },
    ],
  }
}

interface GlobalSearchModalProps {
  open: boolean
  onClose: () => void
  consumeRequestedScope?: () => SearchScope | null
}

const GlobalSearchModal: FC<GlobalSearchModalProps> = memo(
  ({ open, onClose, consumeRequestedScope }) => {
    const router = useRouter()
    const [scope, setScope] = useState<SearchScope>('app')
    const [searchTerm, setSearchTerm] = useState('')
    const [activeTab, setActiveTab] = useState('all')
    const [activeIndex, setActiveIndex] = useState(-1)
    const [maxResults, setMaxResults] = useState(5)
    const inputRef = useRef<any>(null)
    const debounceTimerRef = useRef<ReturnType<typeof setTimeout>>(undefined)
    const activeItemRef = useRef<HTMLDivElement>(null)
    const isKeyboardNavRef = useRef(false)

    // App search (backend)
    const [triggerSearch, { data: appData, isFetching: appFetching }] =
      useLazyGlobalSearchQuery()

    // Docs search (client-side)
    const [docsIndex, setDocsIndex] = useState<DocSearchEntry[]>([])
    const [docsData, setDocsData] = useState<GlobalSearchResultDto | null>(null)
    const [docsFetching, setDocsFetching] = useState(false)
    const docsIndexLoadedRef = useRef(false)

    // Load docs index on first use
    useEffect(() => {
      if (scope === 'docs' && !docsIndexLoadedRef.current) {
        docsIndexLoadedRef.current = true
        setDocsFetching(true)
        fetch('/docs-search-index.json')
          .then((res) => res.json())
          .then((data: DocSearchEntry[]) => {
            setDocsIndex(data)
            setDocsFetching(false)
          })
          .catch(() => setDocsFetching(false))
      }
    }, [scope])

    // Active data based on scope
    const data = scope === 'app' ? appData : docsData
    const isFetching = scope === 'app' ? appFetching : docsFetching

    // Debounced search
    useEffect(() => {
      if (!searchTerm || searchTerm.length < 2) return

      debounceTimerRef.current = setTimeout(() => {
        if (scope === 'app') {
          triggerSearch({
            query: searchTerm,
            maxResultsPerCategory: maxResults,
          })
        } else {
          setDocsData(searchDocsLocally(docsIndex, searchTerm, maxResults))
        }
      }, 300)

      return () => clearTimeout(debounceTimerRef.current)
    }, [searchTerm, maxResults, scope, triggerSearch, docsIndex])

    // Reset state when opening
    useEffect(() => {
      if (open) {
        // If opened via keyboard shortcut, use the requested scope
        const requested = consumeRequestedScope?.()
        if (requested) {
          setScope(requested)
        }
        // Otherwise keep the current scope (remembers last selection)
        setSearchTerm('')
        setActiveTab('all')
        setActiveIndex(-1)
        setTimeout(() => inputRef.current?.focus(), 100)
      }
    }, [open, consumeRequestedScope])

    // Reset results when scope changes
    useEffect(() => {
      setActiveTab('all')
      setActiveIndex(-1)
      // Re-trigger search if there's a term
      if (searchTerm.length >= 2) {
        if (scope === 'app') {
          triggerSearch({
            query: searchTerm,
            maxResultsPerCategory: maxResults,
          })
        } else {
          setDocsData(searchDocsLocally(docsIndex, searchTerm, maxResults))
        }
      }
    }, [scope]) // eslint-disable-line react-hooks/exhaustive-deps

    // Compute visible items based on active tab
    const visibleItems = (() => {
      if (!data?.categories) return []

      if (activeTab === 'all') {
        return data.categories.flatMap((cat) =>
          cat.items.map((item) => ({ ...item, category: cat.name })),
        )
      }

      const category = data.categories.find((c) => c.slug === activeTab)
      return (
        category?.items.map((item) => ({
          ...item,
          category: category.name,
        })) ?? []
      )
    })()

    // Reset active index when search term or tab changes
    useEffect(() => {
      setActiveIndex(-1)
    }, [searchTerm, activeTab])

    // Scroll active item into view on keyboard navigation
    useEffect(() => {
      if (isKeyboardNavRef.current) {
        activeItemRef.current?.scrollIntoView({ block: 'nearest' })
        requestAnimationFrame(() => {
          isKeyboardNavRef.current = false
        })
      }
    }, [activeIndex])

    const handleNavigate = (item: GlobalSearchResultItemDto) => {
      router.push(getSearchResultUrl(item))
      onClose()
    }

    // Keyboard navigation
    const handleKeyDown = (e: React.KeyboardEvent) => {
      // Scope switching: Ctrl+D for docs, Ctrl+K for app (chord shortcut)
      if ((e.metaKey || e.ctrlKey) && e.key === 'd') {
        e.preventDefault()
        setScope('docs')
        return
      }
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setScope('app')
        return
      }

      if (e.key === 'ArrowDown' && visibleItems.length > 0) {
        e.preventDefault()
        isKeyboardNavRef.current = true
        setActiveIndex((prev) =>
          prev < visibleItems.length - 1 ? prev + 1 : 0,
        )
      } else if (e.key === 'ArrowUp' && visibleItems.length > 0) {
        e.preventDefault()
        isKeyboardNavRef.current = true
        setActiveIndex((prev) =>
          prev > 0 ? prev - 1 : visibleItems.length - 1,
        )
      } else if (e.key === 'Enter' && activeIndex >= 0 && visibleItems[activeIndex]) {
        e.preventDefault()
        handleNavigate(visibleItems[activeIndex])
      } else if (e.key === 'Escape') {
        e.preventDefault()
        onClose()
      }
    }

    // Build tab items
    const tabItems = (() => {
      const categories = data?.categories ?? []
      const tabs = [
        {
          key: 'all',
          label: `All${categories.length > 0 ? ` (${categories.reduce((sum, c) => sum + c.totalCount, 0)})` : ''}`,
        },
        ...categories
          .filter((c) => c.items.length > 0)
          .map((c) => ({
            key: c.slug,
            label: `${c.name} (${c.totalCount})`,
          })),
      ]
      return tabs
    })()

    // Group items by category for "All" tab
    const groupedResults =
      !data?.categories || activeTab !== 'all'
        ? null
        : data.categories.filter((c) => c.items.length > 0)

    const renderResultItem = (
      item: GlobalSearchResultItemDto & { category?: string },
      index: number,
    ) => (
      <Flex
        key={`${item.entityType}-${item.key}-${index}`}
        id={`search-result-${index}`}
        ref={index === activeIndex ? activeItemRef : undefined}
        align="center"
        gap={12}
        tabIndex={-1}
        className={`${styles.resultItem} ${index === activeIndex ? styles.resultItemActive : ''}`}
        onClick={() => handleNavigate(item)}
        onMouseEnter={() => {
          if (!isKeyboardNavRef.current) setActiveIndex(index)
        }}
      >
        {scope === 'app' && (
          <Text code className={styles.resultKey}>
            {item.key}
          </Text>
        )}
        <Flex vertical className={styles.resultItemContent}>
          <Text ellipsis>{item.title}</Text>
          {item.subtitle && (
            <Text type="secondary" ellipsis className={styles.resultSubtitle}>
              {item.subtitle}
            </Text>
          )}
        </Flex>
      </Flex>
    )

    const renderAllResults = (categories: GlobalSearchCategoryDto[]) => {
      let globalIndex = 0
      return categories.map((category) => (
        <div key={category.slug} aria-label={category.name}>
          <Title level={5} type="secondary" className={styles.categoryHeader}>
            {category.name}
          </Title>
          {category.items.map((item) => {
            const idx = globalIndex++
            return renderResultItem({ ...item, category: category.name }, idx)
          })}
        </div>
      ))
    }

    const renderTabResults = () => {
      const category = data?.categories?.find((c) => c.slug === activeTab)
      if (!category) return null
      return category.items.map((item, index) => renderResultItem(item, index))
    }

    return (
      <Modal
        open={open}
        onCancel={onClose}
        footer={null}
        closable
        mask={{ closable: true }}
        keyboard={false}
        destroyOnHidden
        width={600}
        className={styles.searchModal}
        styles={{
          container: { padding: '10px 12px' },
          body: { padding: 0 },
        }}
      >
        <div onKeyDown={handleKeyDown}>
          <Flex className={styles.scopeToggle}>
            <Segmented
              size="small"
              value={scope}
              onChange={(val) => setScope(val as SearchScope)}
              options={[
                { value: 'app', label: 'App' },
                { value: 'docs', label: 'Docs' },
              ]}
            />
          </Flex>

          <Input
            ref={inputRef}
            prefix={<SearchOutlined />}
            placeholder={
              scope === 'app' ? 'Search Wayd...' : 'Search documentation...'
            }
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className={styles.searchInput}
            variant="borderless"
            size="large"
            allowClear
            role="combobox"
            aria-expanded={visibleItems.length > 0}
            aria-controls="global-search-listbox"
            aria-activedescendant={
              visibleItems.length > 0
                ? `search-result-${activeIndex}`
                : undefined
            }
          />

          {isFetching && !data && (
            <Flex justify="center" style={{ padding: '32px 16px' }}>
              <Spin size="small" />
            </Flex>
          )}

          {!isFetching && searchTerm.length >= 2 && data && (
            <div className={styles.tabsContainer}>
              {data.categories.length > 1 && (
                <Tabs
                  activeKey={activeTab}
                  onChange={setActiveTab}
                  size="small"
                  items={tabItems}
                />
              )}
              <div className={styles.resultsList}>
                {visibleItems.length === 0 ? (
                  <Empty
                    description={`No results found for "${searchTerm}"`}
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                  />
                ) : (
                  <div id="global-search-listbox">
                    {activeTab === 'all' && groupedResults
                      ? renderAllResults(groupedResults)
                      : renderTabResults()}
                  </div>
                )}
              </div>
            </div>
          )}

          {!isFetching && searchTerm.length < 2 && (
            <Empty
              description="Type at least 2 characters to search"
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          )}

          <Divider style={{ margin: 0 }} />
          <Flex
            justify="space-between"
            align="center"
            gap={16}
            wrap
            style={{ padding: '6px 16px' }}
            className={styles.shortcutHint}
          >
            <Flex gap={16}>
              <Text type="secondary">
                <kbd className={styles.kbd}>↑</kbd>
                <kbd className={styles.kbd}>↓</kbd> navigate
              </Text>
              <Text type="secondary">
                <kbd className={styles.kbd}>↵</kbd> select
              </Text>
              <Text type="secondary">
                <kbd className={styles.kbd}>esc</kbd> close
              </Text>
            </Flex>
            {scope === 'app' && (
              <Flex align="center" gap={6}>
                <Text type="secondary">Per Category:</Text>
                <RadioGroup
                  size="small"
                  value={maxResults}
                  onChange={(e) => setMaxResults(e.target.value)}
                  optionType="button"
                  buttonStyle="outline"
                  options={[
                    { value: 5, label: '5' },
                    { value: 10, label: '10' },
                    { value: 20, label: '20' },
                  ]}
                  className={styles.resultsRadioGroup}
                />
              </Flex>
            )}
          </Flex>
        </div>
      </Modal>
    )
  },
)

GlobalSearchModal.displayName = 'GlobalSearchModal'

export default GlobalSearchModal
