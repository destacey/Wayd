'use client'

import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { DataSet } from 'vis-data'
import { Timeline, TimelineOptions } from 'vis-timeline/standalone'
import { TimelineOptionsTemplateFunction } from '@/src/lib/vis-timeline'
import { Button, Dropdown, MenuProps, Spin } from 'antd'
import useTheme from '../../contexts/theme'
import { WaydEmpty } from '..'
import './wayd-timeline.css'
import {
  DefaultTimeLineColors,
  getDefaultTemplate,
  groupsStructurallyEqual,
  itemsVisuallyEqual,
} from './wayd-timeline.utils'
import {
  WaydDataGroup,
  WaydDataItem,
  WaydTimelineProps,
  TimelineTemplate,
} from '.'
import {
  FileImageOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
  MoreOutlined,
  UndoOutlined,
} from '@ant-design/icons'
import { saveElementAsImage } from '@/src/utils'
import { TimelineOptionsItemCallbackFunction } from 'vis-timeline'
import dayjs from 'dayjs'

const WaydTimeline = <TItem extends WaydDataItem, TGroup extends WaydDataGroup>(
  props: WaydTimelineProps<TItem, TGroup>,
) => {
  const [isTimelineLoading, setIsTimelineLoading] = useState(false)
  const [isFullScreen, setIsFullScreen] = useState(false)
  const [reinitTrigger, setReinitTrigger] = useState(0)

  // Store both container and root for cleanup
  const elementMapRef = useRef<
    Record<
      string | number,
      { container: HTMLElement; root: ReturnType<typeof createRoot> }
    >
  >({})
  const timelineRef = useRef<HTMLDivElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const timelineInstanceRef = useRef<Timeline | null>(null)
  const datasetItemsRef = useRef<DataSet<TItem> | null>(null)
  const datasetGroupsRef = useRef<DataSet<TGroup> | null>(null)
  const initialWindowRef = useRef<{ start: Date; end: Date } | null>(null)
  const preservedWindowRef = useRef<{ start: Date; end: Date } | null>(null)
  const isInitializedRef = useRef(false)
  const dynamicOptionsRef = useRef<TimelineOptions>({})
  const prevWindowBoundsRef = useRef<{ start: Date; end: Date } | null>(null)
  // Snapshot of the option values last applied via setOptions(), so we can skip
  // calling it again when only object/function identity changed (e.g. a refetch
  // produced a fresh Date with the same instant). setOptions() triggers a redraw
  // even for a no-op change, which the user perceives as a section-wide flicker.
  const prevAppliedOptionsRef = useRef<{
    maxHeight: number | undefined
    showCurrentTime: boolean | undefined
    groupOrder: string | undefined
    minTime: number | undefined
    maxTime: number | undefined
    editableUpdateTime: boolean
    template: unknown
  } | null>(null)
  const propsDataRef = useRef(props.data)
  propsDataRef.current = props.data
  const propsGroupsRef = useRef(props.groups)
  propsGroupsRef.current = props.groups
  const propsRef = useRef(props)
  propsRef.current = props

  const { currentThemeName, token } = useTheme()

  const enableFullScreenToggle = props.allowFullScreen ?? false
  const enableSaveAsImage = props.allowSaveAsImage ?? false

  const colors = DefaultTimeLineColors[currentThemeName]
  const colorsRef = useRef(colors)
  colorsRef.current = colors

  const itemTemplateManager: TimelineOptionsTemplateFunction<TItem> =
    useCallback(
      (item, element, _) => {
        if (!item) return ''

        const mapId = item.id ?? 0
        if (elementMapRef.current?.[mapId])
          return elementMapRef.current[mapId].container

        // Create a container for the React element (prevents DOM node errors)
        const container = document.createElement('div')
        if (element) element.appendChild(container)
        const root = createRoot(container)

        // Unfortunately, typescript doesn't seem to handle nested constrained generics very well (see: https://github.com/microsoft/TypeScript/issues/23132)
        //  so we must add the type annotation here to avoid the error
        const Template: TimelineTemplate<WaydDataItem> = getDefaultTemplate(
          item.type,
          propsRef.current,
        )

        if (Template)
          root.render(
            <Template
              item={item}
              fontColor={colorsRef.current.item.font}
              foregroundColor={colorsRef.current.item.foreground}
            />,
          )

        // Store the rendered element container and root to reference later
        elementMapRef.current[mapId] = { container, root }

        // Return the new container
        return container
      },
      [],
    )

  const groupTemplateManager: TimelineOptionsTemplateFunction<TGroup> =
    useCallback(
      (item, element, _) => {
        if (!item) return ''

        const mapId = item.id ?? 0
        if (elementMapRef.current?.[mapId])
          return elementMapRef.current[mapId].container

        // Create a container for the react element (prevents DOM node errors)
        const container = document.createElement('div')
        element?.appendChild(container)
        const root = createRoot(container)

        // Unfortunately, typescript doesn't seem to handle nested constrained generics very well (see: https://github.com/microsoft/TypeScript/issues/23132)
        //  so we must add the type annotation here to avoid the error
        const Template: TimelineTemplate<WaydDataGroup> = getDefaultTemplate(
          'group',
          propsRef.current,
        )

        if (Template) {
          root.render(
            <Template
              item={item}
              fontColor={colorsRef.current.item.font}
              foregroundColor={colorsRef.current.item.foreground}
              parentElement={element}
            />,
          )
        }

        // Store the rendered element container and root to reference later
        elementMapRef.current[mapId] = { container, root }

        // Return the new container
        return container
      },
      [],
    )

  const onMoveProp = props.onMove
  const onMove: TimelineOptionsItemCallbackFunction = useCallback(
    (item, callback) => {
      const original = datasetItemsRef.current?.get(item.id)

      if (!original) return

      if (
        dayjs(original.end).isSame(dayjs(item.end)) &&
        dayjs(original.start).isSame(dayjs(item.start))
      )
        return

      // TODO: Account for total days in a month when moving months

      // Call callback to confirm the move visually
      callback(item)

      // Notify parent if they have a handler
      onMoveProp?.(item)
    },
    [onMoveProp],
  )

  const baseOptions = useMemo<TimelineOptions>(
    () => ({
      editable: {
        updateTime: onMoveProp ? true : false,
        updateGroup: false,
        remove: false,
        add: false,
        overrideItems: false,
      },
      onMove,
      selectable: true,
      orientation: 'top',
      maxHeight: props.options.maxHeight ?? 650,
      minHeight: 200,
      moveable: true,
      showCurrentTime: props.options.showCurrentTime ?? true,
      verticalScroll: true,
      zoomKey: 'ctrlKey',
      // Day-level precision: snap items to day boundaries when dragging
      snap: (date) => {
        const d = new Date(date)
        d.setHours(0, 0, 0, 0)
        return d
      },
      // Prevent zooming to time level (minimum 1 day)
      zoomMin: 86400000,
      // Format axis to show only dates, not times
      format: {
        minorLabels: {
          day: 'D',
          weekday: 'ddd D',
          month: 'MMM',
          year: 'YYYY',
        },
        majorLabels: {
          day: 'MMMM YYYY',
          weekday: 'MMMM YYYY',
          month: 'YYYY',
          year: '',
        },
      },
      start: props.options.start,
      end: props.options.end,
      min: props.options.min,
      max: props.options.max,
      groupOrder: props.options.groupOrder ?? 'order',
      groupHeightMode: 'auto',
      xss: { disabled: false },
      template: props.options.template ?? itemTemplateManager,
      groupTemplate: groupTemplateManager,
    }),
    [
      onMove,
      onMoveProp,
      props.options.maxHeight,
      props.options.showCurrentTime,
      props.options.start,
      props.options.end,
      props.options.min,
      props.options.max,
      props.options.groupOrder,
      props.options.template,
      itemTemplateManager,
      groupTemplateManager,
    ],
  )

  useEffect(() => {
    // Always update dynamicOptionsRef based on fullscreen state
    const FULLSCREEN_VERTICAL_OFFSET = 40 // Space for header and padding
    const maxHeight = isFullScreen
      ? window.innerHeight - FULLSCREEN_VERTICAL_OFFSET
      : baseOptions.maxHeight
    const updatedOptions = {
      ...baseOptions,
      maxHeight,
    }
    dynamicOptionsRef.current = updatedOptions

    // Update existing timeline instance if already initialized
    if (timelineInstanceRef.current && isInitializedRef.current) {
      // Check if window bounds (start/end) have changed
      const currentStart = updatedOptions.start
        ? new Date(updatedOptions.start)
        : null
      const currentEnd = updatedOptions.end
        ? new Date(updatedOptions.end)
        : null

      const startChanged =
        !prevWindowBoundsRef.current ||
        !currentStart ||
        prevWindowBoundsRef.current.start.getTime() !== currentStart.getTime()
      const endChanged =
        !prevWindowBoundsRef.current ||
        !currentEnd ||
        prevWindowBoundsRef.current.end.getTime() !== currentEnd.getTime()

      if (startChanged || endChanged) {
        // Window bounds changed - update the visible window
        if (currentStart && currentEnd) {
          timelineInstanceRef.current.setWindow(currentStart, currentEnd, {
            animation: true,
          })
          prevWindowBoundsRef.current = {
            start: currentStart,
            end: currentEnd,
          }
        }
      }

      // Only call setOptions when an option value actually changed. baseOptions
      // is re-created on every render because consumers pass fresh Date instances
      // (e.g. dayjs().toDate() in roadmap timeline) — without this short-circuit,
      // setOptions() fires every refetch and the user sees a section-wide flicker.
      const nextSnapshot = {
        maxHeight:
          typeof updatedOptions.maxHeight === 'number'
            ? updatedOptions.maxHeight
            : undefined,
        showCurrentTime: updatedOptions.showCurrentTime,
        groupOrder:
          typeof updatedOptions.groupOrder === 'string'
            ? updatedOptions.groupOrder
            : undefined,
        minTime: updatedOptions.min ? new Date(updatedOptions.min).getTime() : undefined,
        maxTime: updatedOptions.max ? new Date(updatedOptions.max).getTime() : undefined,
        editableUpdateTime:
          typeof updatedOptions.editable === 'object' &&
          updatedOptions.editable !== null
            ? !!updatedOptions.editable.updateTime
            : false,
        // Track the consumer-provided template reference so we re-apply options
        // when the consumer swaps it out. Consumers are expected to memoize this
        // reference; if they don't, every render will count as a template change
        // and re-apply options, which is the correct safe-side behavior.
        template: propsRef.current.options.template,
      }
      const prev = prevAppliedOptionsRef.current
      const optionsChanged =
        !prev ||
        prev.maxHeight !== nextSnapshot.maxHeight ||
        prev.showCurrentTime !== nextSnapshot.showCurrentTime ||
        prev.groupOrder !== nextSnapshot.groupOrder ||
        prev.minTime !== nextSnapshot.minTime ||
        prev.maxTime !== nextSnapshot.maxTime ||
        prev.editableUpdateTime !== nextSnapshot.editableUpdateTime ||
        prev.template !== nextSnapshot.template

      if (optionsChanged) {
        const { start, end, ...optionsWithoutWindow } = updatedOptions
        timelineInstanceRef.current.setOptions(optionsWithoutWindow)
        prevAppliedOptionsRef.current = nextSnapshot
      }
    }

    // Add resize listener when in fullscreen
    if (isFullScreen) {
      const handleResize = () => {
        if (!timelineInstanceRef.current || !isInitializedRef.current) return
        const newMaxHeight = window.innerHeight - 100
        const resizedOptions = {
          ...baseOptions,
          maxHeight: newMaxHeight,
        }
        dynamicOptionsRef.current = resizedOptions
        // Exclude start/end to prevent window reset during resize
        const { start, end, ...optionsWithoutWindow } = resizedOptions
        timelineInstanceRef.current.setOptions(optionsWithoutWindow)
      }

      window.addEventListener('resize', handleResize)
      return () => {
        window.removeEventListener('resize', handleResize)
      }
    }
  }, [isFullScreen, baseOptions])

  // Initialize or reinitialize timeline when structure changes (item count or group count)
  useEffect(() => {
    // Don't initialize if loading or no data
    if (props.isLoading) return
    if (!timelineRef.current) return
    if (Object.keys(dynamicOptionsRef.current).length === 0) return

    // Don't initialize if no data
    if (props.data.length === 0) return

    // If already initialized, don't reinitialize - use DataSet updates instead
    if (isInitializedRef.current) return

    setIsTimelineLoading(true)

    // Read current data/groups from refs to avoid depending on the full arrays
    const data = propsDataRef.current
    const groups = propsGroupsRef.current

    const datasetItems = new DataSet([] as TItem[])
    data.forEach((item) => {
      const backgroundColor = item.itemColor ?? colorsRef.current.item.background
      const newItem: TItem = {
        ...item,
        itemColor: backgroundColor,
        style: item.style
          ? item.style
          : item.type === 'range'
            ? `background: ${backgroundColor}; border-color: ${backgroundColor};`
            : item.type === 'background'
              ? `background: ${colorsRef.current.background.background}; border-style: inset; border-width: 1px;`
              : undefined,
      }
      datasetItems.add(newItem)
    })

    datasetItemsRef.current = datasetItems

    if (groups?.length && groups.length > 0) {
      const datasetGroups = new DataSet(groups)
      datasetGroupsRef.current = datasetGroups
      timelineInstanceRef.current = new Timeline(
        timelineRef.current,
        datasetItems,
        datasetGroups,
        dynamicOptionsRef.current,
      )
    } else {
      timelineInstanceRef.current = new Timeline(
        timelineRef.current,
        datasetItems,
        dynamicOptionsRef.current,
      )
    }

    // Store initial window for reset functionality
    const opts = dynamicOptionsRef.current
    if (opts.start && opts.end) {
      const startDate = new Date(opts.start)
      const endDate = new Date(opts.end)

      initialWindowRef.current = {
        start: startDate,
        end: endDate,
      }
      // Also track as previous bounds for conditional updates
      prevWindowBoundsRef.current = {
        start: startDate,
        end: endDate,
      }
    }

    isInitializedRef.current = true

    // Restore window if a theme-change reinit preserved it
    if (preservedWindowRef.current) {
      timelineInstanceRef.current.setWindow(
        preservedWindowRef.current.start,
        preservedWindowRef.current.end,
        { animation: false },
      )
      preservedWindowRef.current = null
    }

    setTimeout(() => {
      setIsTimelineLoading(false)
    }, 800)
  }, [
    props.isLoading,
    props.data.length, // Reinit when item count changes
    props.groups?.length, // Reinit when group count changes
    reinitTrigger, // Reinit when explicitly triggered (e.g., groups removed)
    colors.item.background,
    colors.background.background,
  ])

  // Update data when it changes (after initialization)
  useEffect(() => {
    if (!isInitializedRef.current || !datasetItemsRef.current) return

    const processedItems = props.data.map((item) => {
      const backgroundColor = item.itemColor ?? colorsRef.current.item.background
      return {
        ...item,
        itemColor: backgroundColor,
        style: item.style
          ? item.style
          : item.type === 'range'
            ? `background: ${backgroundColor}; border-color: ${backgroundColor};`
            : item.type === 'background'
              ? `background: ${colorsRef.current.background.background}; border-style: inset; border-width: 1px;`
              : undefined,
      } as TItem
    })

    const currentIds = datasetItemsRef.current.getIds()
    const newIds = processedItems.map((item) => item.id)

    const toRemove = currentIds.filter((id) => !newIds.includes(id))
    if (toRemove.length > 0) {
      datasetItemsRef.current.remove(toRemove)
      toRemove.forEach((id) => {
        if (elementMapRef.current[id]) {
          const { root } = elementMapRef.current[id]
          setTimeout(() => {
            try {
              root.unmount()
            } catch (error) {
              console.error('Error unmounting root:', error)
            }
          }, 0)
          delete elementMapRef.current[id]
        }
      })
    }

    processedItems.forEach((item) => {
      const existing = item.id !== undefined ? datasetItemsRef.current!.get(item.id) : undefined
      if (!existing) {
        datasetItemsRef.current!.add(item as any)
        return
      }

      // Skip when nothing the template or vis-timeline cares about has changed.
      // RTK Query returns a fresh array on every refetch, so without this check
      // every item would re-mount on every refetch and the whole timeline would
      // flicker after editing a single activity.
      if (itemsVisuallyEqual(existing as TItem, item)) return

      // Clear cached template so it re-renders with updated data
      const mapId = item.id ?? 0
      if (elementMapRef.current[mapId]) {
        const { container, root } = elementMapRef.current[mapId]
        container.remove()
        setTimeout(() => {
          try {
            root.unmount()
          } catch (error) {
            console.error('Error unmounting root:', error)
          }
        }, 0)
        delete elementMapRef.current[mapId]
      }
      datasetItemsRef.current!.update(item as any)
    })
  }, [props.data, colors.item.background, colors.background.background])

  // Fully reinitialize the timeline when the theme changes so group/item templates
  // re-render with the correct font colors (they use inline styles, not CSS).
  useEffect(() => {
    if (!isInitializedRef.current || !timelineInstanceRef.current) return

    // Preserve the current window before destroying
    const currentWindow = timelineInstanceRef.current.getWindow()

    // Clear the cache immediately so templates don't reuse old containers,
    // then unmount the React roots async to avoid unmounting during render.
    const oldElements = elementMapRef.current
    elementMapRef.current = {}
    setTimeout(() => {
      Object.values(oldElements).forEach(({ root }) => {
        try { root.unmount() } catch { /* ignore */ }
      })
    }, 0)

    // Destroy the timeline instance so init effect rebuilds it
    timelineInstanceRef.current.destroy()
    timelineInstanceRef.current = null
    datasetItemsRef.current = null
    datasetGroupsRef.current = null
    isInitializedRef.current = false
    // Force the options-effect to apply on the next render after reinit,
    // since the new Timeline instance starts with no options applied.
    prevAppliedOptionsRef.current = null

    // Stash the current window so it can be restored after reinit
    if (currentWindow) {
      preservedWindowRef.current = { start: currentWindow.start, end: currentWindow.end }
    }

    setReinitTrigger((prev) => prev + 1)
  }, [currentThemeName])

  // Update groups when they change (after initialization)
  useEffect(() => {
    if (!isInitializedRef.current || !timelineInstanceRef.current) return

    // Don't reinitialize if we're just loading - wait for data to arrive
    if (props.isLoading) return

    // Groups removed - need to reinitialize without groups
    if (
      (!props.groups || props.groups.length === 0) &&
      datasetGroupsRef.current
    ) {
      if (timelineInstanceRef.current) {
        timelineInstanceRef.current.destroy()
        timelineInstanceRef.current = null
      }

      datasetItemsRef.current = null
      datasetGroupsRef.current = null
      isInitializedRef.current = false

      // Trigger reinit by updating state
      setReinitTrigger((prev) => prev + 1)
      return
    }

    // Groups arrived but we initialized without them - add groups to existing timeline
    if (props.groups && props.groups.length > 0 && !datasetGroupsRef.current) {
      const datasetGroups = new DataSet(props.groups)
      datasetGroupsRef.current = datasetGroups
      timelineInstanceRef.current.setGroups(datasetGroups)

      // Re-apply options to ensure they're still set correctly after adding groups
      // Exclude start/end to prevent window reset
      const { start, end, ...optionsWithoutWindow } = dynamicOptionsRef.current
      timelineInstanceRef.current.setOptions(optionsWithoutWindow)

      // Force redraw to ensure items render correctly with newly added groups
      timelineInstanceRef.current.redraw()
      return
    }

    if (!datasetGroupsRef.current || !props.groups) return

    // When the group hierarchy is structurally unchanged, skip the destructive
    // setGroups() — it rebuilds the group DOM and leaves orphaned item containers
    // from the template cache. But we still need to refresh the rendered group
    // templates when fields the template reads (e.g. `content`) have changed, so
    // an edited activity name shows up without a full refetch/rebuild. Re-render
    // into the existing cached container in place — do NOT remove the container
    // or unmount the root, since vis-timeline has already attached that DOM node
    // and won't re-invoke `groupTemplate` for an existing group id.
    const prevGroups = datasetGroupsRef.current.get() as TGroup[]
    if (groupsStructurallyEqual(prevGroups, props.groups)) {
      const prevById = new Map<string | number, TGroup>()
      prevGroups.forEach((g) => {
        if (g.id !== undefined) prevById.set(g.id, g)
      })
      const Template: TimelineTemplate<WaydDataGroup> = getDefaultTemplate(
        'group',
        propsRef.current,
      )
      // vis-timeline orders group rows by the field named by `groupOrder`
      // (default 'order'). If a consumer mutates that field on an existing
      // group, the row layout must change — include it in the equality check
      // so we don't get stuck with stale ordering.
      const groupOrderField =
        (typeof propsRef.current.options.groupOrder === 'string'
          ? propsRef.current.options.groupOrder
          : undefined) ?? 'order'
      const changedGroups: TGroup[] = []
      props.groups.forEach((next) => {
        if (next.id === undefined) return
        const prev = prevById.get(next.id)
        if (
          prev &&
          prev.content === next.content &&
          prev.className === next.className &&
          prev.style === next.style &&
          prev.treeLevel === next.treeLevel &&
          (prev as Record<string, unknown>)[groupOrderField] ===
            (next as Record<string, unknown>)[groupOrderField] &&
          prev.visible === next.visible
        ) {
          return
        }
        changedGroups.push(next)
        const cached = elementMapRef.current[next.id]
        if (cached && Template) {
          cached.root.render(
            <Template
              item={next}
              fontColor={colorsRef.current.item.font}
              foregroundColor={colorsRef.current.item.foreground}
              parentElement={cached.container.parentElement ?? undefined}
            />,
          )
        }
      })
      // Skip the DataSet.update() entirely when no visible field changed —
      // calling update() on every refetch fires change events that cause
      // vis-timeline to re-lay out group rows, producing visible flicker.
      if (changedGroups.length > 0) {
        datasetGroupsRef.current.update(changedGroups as any)
      }
      return
    }

    // Evict cached group template entries so React roots from the old hierarchy
    // aren't reused after setGroups() rebuilds the DOM and detaches those containers.
    const prevGroupIds = new Set(datasetGroupsRef.current.getIds())
    Object.keys(elementMapRef.current).forEach((key) => {
      const id = (isNaN(Number(key)) ? key : Number(key)) as string | number
      if (prevGroupIds.has(id)) {
        const { container, root } = elementMapRef.current[key]
        container.remove()
        setTimeout(() => { try { root.unmount() } catch { /* ignore */ } }, 0)
        delete elementMapRef.current[key]
      }
    })

    // Fully replace the groups DataSet to ensure vis-timeline rebuilds its
    // internal nestedInGroup hierarchy correctly when the level structure changes.
    const newDataset = new DataSet(props.groups)
    datasetGroupsRef.current = newDataset
    timelineInstanceRef.current.setGroups(newDataset)
  }, [props.groups, props.isLoading])

  // When consumer controls group column width via CSS, force a vis-timeline
  // redraw so axis/grid panel geometry is recomputed to match.
  useEffect(() => {
    if (!isInitializedRef.current || !timelineInstanceRef.current) return
    if (!props.options.groupColumnWidth) return

    timelineInstanceRef.current.redraw()
  }, [props.options.groupColumnWidth])

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      const roots = Object.values(elementMapRef.current).map(({ root }) => root)
      setTimeout(() => {
        roots.forEach((root) => {
          try {
            root.unmount()
          } catch (error) {
            console.error('Error unmounting root:', error)
          }
        })
      }, 0)
      elementMapRef.current = {}
      if (timelineInstanceRef.current) {
        timelineInstanceRef.current.destroy()
        timelineInstanceRef.current = null
      }
      datasetItemsRef.current = null
      datasetGroupsRef.current = null
      isInitializedRef.current = false
      prevWindowBoundsRef.current = null
    }
  }, [])

  const toggleFullScreen = () => {
    if (!enableFullScreenToggle) return
    setIsFullScreen(!isFullScreen)
  }

  const saveTimelineAsImage = async () => {
    if (!timelineRef.current || !timelineInstanceRef.current) return

    // Store current options
    const currentOptions = dynamicOptionsRef.current

    try {
      // Temporarily remove height constraints to capture full timeline
      const captureOptions = {
        ...currentOptions,
        maxHeight: undefined,
        height: undefined,
      }
      timelineInstanceRef.current.setOptions(captureOptions)

      // Wait for the timeline to re-render with full height
      await new Promise((resolve) => setTimeout(resolve, 300))

      const canvasOptions: Record<string, unknown> = {
        backgroundColor: token.colorBgContainer,
      }

      await saveElementAsImage(
        timelineRef.current,
        'timeline.png',
        canvasOptions,
      )
    } catch (error) {
      console.error('Failed to save timeline as image:', error)
    } finally {
      // Restore original options
      timelineInstanceRef.current.setOptions(currentOptions)
    }
  }

  const resetWindow = () => {
    if (timelineInstanceRef.current && initialWindowRef.current) {
      timelineInstanceRef.current.setWindow(
        initialWindowRef.current.start,
        initialWindowRef.current.end,
        { animation: true },
      )
    }
  }

  const isLoading = props.isLoading || isTimelineLoading
  const hasData = props.data && props.data.length > 0
  const showControls = !isLoading && hasData

  const menuItems: MenuProps['items'] = (() => {
    const items: MenuProps['items'] = []

    if (enableFullScreenToggle) {
      items.push({
        key: 'fullscreen',
        label: isFullScreen ? 'Exit Fullscreen' : 'Enter Fullscreen',
        icon: isFullScreen ? (
          <FullscreenExitOutlined />
        ) : (
          <FullscreenOutlined />
        ),
        onClick: toggleFullScreen,
      })
    }

    if (enableSaveAsImage) {
      items.push({
        key: 'save-image',
        label: 'Save as Image',
        icon: <FileImageOutlined />,
        onClick: saveTimelineAsImage,
      })
    }

    items.push({
      key: 'reset',
      label: 'Reset View',
      icon: <UndoOutlined />,
      onClick: resetWindow,
    })

    return items
  })()

  return (
    <Spin spinning={isLoading} description="Loading timeline..." size="large">
      <div
        ref={containerRef}
        className={
          props.options.groupColumnWidth
            ? 'wayd-timeline-root wayd-timeline-fixed-group-width'
            : 'wayd-timeline-root'
        }
        style={{
          ...(props.options.groupColumnWidth
            ? {
                ['--wayd-group-column-width' as any]: `${props.options.groupColumnWidth}px`,
              }
            : {}),
          position: isFullScreen ? 'fixed' : 'relative',
          top: 0,
          left: 0,
          width: isFullScreen ? '100vw' : 'auto',
          height: isFullScreen ? '100vh' : 'auto',
          zIndex: isFullScreen ? 1000 : 'auto',
          transition: 'all 0.3s ease',
          background: isFullScreen ? token.colorBgContainer : 'transparent',
          padding: isFullScreen ? 20 : 0,
          overflow: isFullScreen ? 'auto' : 'unset',
        }}
      >
        {showControls && (
          <Dropdown menu={{ items: menuItems }} trigger={['click']}>
            <Button
              type="text"
              shape="circle"
              title="Timeline Actions"
              aria-label="Timeline Actions"
              icon={<MoreOutlined />}
              size="small"
              style={{
                position: 'absolute',
                top: isFullScreen ? 25 : 5,
                right: isFullScreen ? 25 : 5,
                zIndex: 1000,
              }}
            />
          </Dropdown>
        )}
        <div ref={timelineRef} />
        {!isLoading &&
          (!props.data || props.data.length === 0) &&
          (!props.groups || props.groups.length === 0) && (
            <WaydEmpty message={props.emptyMessage ?? 'No timeline data'} />
          )}
      </div>
    </Spin>
  )
}

export default memo(WaydTimeline)
