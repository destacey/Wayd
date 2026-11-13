'use client'

import { UnlinkedEmployeeAlert, WaydEmpty } from '@/src/components/common'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { SettingOutlined } from '@ant-design/icons'
import { Button, Tooltip, Typography } from 'antd'
import { Suspense, useReducer, useState } from 'react'
import AttentionStrip from './_components/home/attention-strip'
import CustomizeHomeDrawer from './_components/home/customize-home-drawer'
import HomeGreeting from './_components/home/home-greeting'
import { HomeWidgets } from './_components/home/home-widgets'
import styles from './_components/home/home.module.css'
import { useHomeLayout } from './_components/home/use-home-layout'
import WidgetCard from './_components/home/widget-card'
import {
  recordWidgetState,
  WidgetDisplayContext,
  widgetForm,
  WidgetStateReport,
  WidgetStates,
} from './_components/home/widget-display'
import { HomeWidget, widgetAnchorId } from './_components/home/widget-registry'
import {
  emptyDisplayOf,
  WidgetSettingValues,
} from './_components/home/widget-settings'
import { WidgetSettingsContext } from './_components/home/widget-settings-form'

const { Text } = Typography

/** What an unlinked account is missing, so the page explains its gaps rather than looking broken. */
const UNLINKED_CONSEQUENCE =
  'Your projects, risks, tasks and teams, your notifications and the records you follow appear here once it is.'

/** How the page hands its widgets their settings. */
interface WidgetSettingsProps {
  /** Each widget's setting values in effect, by widget id. */
  values: Record<string, WidgetSettingValues>
  /** What the user changed, by widget id. */
  stored: Record<string, WidgetSettingValues>
  /** Undefined while the layout cannot be saved, so no card offers its gear. */
  onChange?: (id: string, stored: WidgetSettingValues) => void
}

/** What the page knows about how each widget is drawn. */
interface WidgetDisplayProps {
  states: WidgetStates
  revealed: boolean
  /** A reducer's dispatch, so it keeps its identity across renders. */
  onReport: (report: WidgetStateReport) => void
}

const WidgetColumn = ({
  widgets,
  className,
  settings,
  display,
}: {
  widgets: HomeWidget[]
  className: string
  settings: WidgetSettingsProps
  display: WidgetDisplayProps
}) => (
  <div className={className}>
    {widgets.map((widget) => {
      const { id, title, Component } = widget
      const { onChange } = settings
      const emptyDisplay = emptyDisplayOf(settings.values[id])
      const { revealed } = display
      return (
        <section
          key={id}
          id={widgetAnchorId(id)}
          className={styles.widget}
          // Hidden, not removed: the widget keeps loading and reports when it fills.
          hidden={
            widgetForm(display.states[id], emptyDisplay, revealed) === 'hidden'
          }
        >
          <WidgetDisplayContext
            value={{
              widgetId: id,
              emptyDisplay,
              revealed,
              onReport: display.onReport,
            }}
          >
            <WidgetSettingsContext
              value={
                onChange
                  ? {
                      widget,
                      stored: settings.stored[id] ?? {},
                      onChange: (stored) => onChange(id, stored),
                    }
                  : null
              }
            >
              {/* A lazily loaded widget shows its titled skeleton while its code arrives. */}
              <Suspense fallback={<WidgetCard title={title} loading />}>
                <Component title={title} settings={settings.values[id]} />
              </Suspense>
            </WidgetSettingsContext>
          </WidgetDisplayContext>
        </section>
      )
    })}
  </div>
)

/** "A", "A and B", "A, B and C". */
const listOf = (names: string[]) =>
  names.length <= 1
    ? names.join('')
    : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`

/**
 * The line naming the widgets hidden as empty, so none disappears without a
 * trace, with the action that shows them until the page is left.
 */
const HiddenWidgetsLine = ({
  widgets,
  revealed,
  onToggle,
}: {
  widgets: HomeWidget[]
  revealed: boolean
  onToggle: () => void
}) => (
  <div className={styles.hiddenWidgets}>
    {/* Announced, since the widgets it names leave the page without a word once loaded. */}
    <Text type="secondary" role="status">
      {widgets.length === 1
        ? `1 widget has nothing to show: ${widgets[0].title}.`
        : `${widgets.length} widgets have nothing to show: ${listOf(widgets.map((w) => w.title))}.`}
    </Text>
    <Button
      type="link"
      size="small"
      className={styles.hiddenWidgetsToggle}
      onClick={onToggle}
      aria-expanded={revealed}
      aria-label={
        revealed
          ? 'Hide the widgets with nothing to show'
          : 'Show the widgets with nothing to show'
      }
    >
      {revealed ? 'Hide' : 'Show'}
    </Button>
  </div>
)

/**
 * The home page: a personal briefing built from the widget registry and the
 * user's saved layout. The frame renders at once and each widget loads, and
 * shows a skeleton, on its own.
 */
const HomePage = () => {
  useDocumentTitle('Home')
  const {
    columns,
    tiles,
    hasLinkedEmployee,
    isLayoutLoading,
    widgetSettings,
    storedSettings,
    changeSettings,
    customize,
  } = useHomeLayout()
  const settings: WidgetSettingsProps = {
    values: widgetSettings,
    stored: storedSettings,
    onChange: customize.canCustomize ? changeSettings : undefined,
  }
  const [states, reportState] = useReducer(recordWidgetState, {})
  // Page state, so leaving the page hides them again.
  const [revealed, setRevealed] = useState(false)
  const display: WidgetDisplayProps = {
    states,
    revealed,
    onReport: reportState,
  }
  // Hidden whatever `revealed` says, so the line still names them while they show.
  const isHiddenAsEmpty = (widget: HomeWidget) =>
    widgetForm(
      states[widget.id],
      emptyDisplayOf(widgetSettings[widget.id]),
      false,
    ) === 'hidden'
  const hiddenWidgets = [...columns.main, ...columns.side].filter(
    isHiddenAsEmpty,
  )
  const showsAny = (widgets: HomeWidget[]) =>
    widgets.some((w) => revealed || !isHiddenAsEmpty(w))
  const hasWidgets = columns.main.length + columns.side.length > 0
  const hasCustomizableWidgets =
    customize.layout.main.length + customize.layout.side.length > 0

  return (
    <div className="page-gutters">
      <div className={styles.header}>
        <HomeGreeting />
        {hasCustomizableWidgets && (
          <Tooltip title="Customize home">
            <Button
              type="text"
              icon={<SettingOutlined />}
              onClick={customize.open}
              disabled={!customize.canCustomize}
              aria-label="Customize home"
            />
          </Tooltip>
        )}
      </div>
      <UnlinkedEmployeeAlert consequence={UNLINKED_CONSEQUENCE} />
      {/* Waits with the columns: a tile of a widget the user hid would otherwise flash. */}
      <AttentionStrip
        tiles={isLayoutLoading ? [] : tiles}
        onRevealHiddenWidgets={() => setRevealed(true)}
      />
      {isLayoutLoading ? null : hasWidgets ? (
        <>
          {/* With nothing showing in one column, the other takes the width. */}
          <div
            className={
              showsAny(columns.main) && showsAny(columns.side)
                ? styles.columns
                : undefined
            }
          >
            <WidgetColumn
              widgets={columns.main}
              className={styles.column}
              settings={settings}
              display={display}
            />
            <WidgetColumn
              widgets={columns.side}
              className={`${styles.column} ${styles.side}`}
              settings={settings}
              display={display}
            />
          </div>
          {/* When every widget is hidden as empty, this line is the page. */}
          {hiddenWidgets.length > 0 ? (
            <HiddenWidgetsLine
              widgets={hiddenWidgets}
              revealed={revealed}
              onToggle={() => setRevealed((r) => !r)}
            />
          ) : null}
        </>
      ) : hasCustomizableWidgets ? (
        <WaydEmpty message="Every widget is switched off. Use Customize home (the gear) to bring some back." />
      ) : hasLinkedEmployee ? (
        // An unlinked account already has the banner saying what will appear.
        <WaydEmpty message="Nothing on the home page applies to your account yet. It fills as you are given access to the areas you work in." />
      ) : null}
      <CustomizeHomeDrawer
        open={customize.isOpen}
        layout={customize.layout}
        widgets={HomeWidgets}
        onChange={customize.change}
        onReset={customize.reset}
        onClose={customize.close}
      />
    </div>
  )
}

export default HomePage
