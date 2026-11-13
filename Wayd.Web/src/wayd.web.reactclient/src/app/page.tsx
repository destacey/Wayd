'use client'

import { UnlinkedEmployeeAlert, WaydEmpty } from '@/src/components/common'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { SettingOutlined } from '@ant-design/icons'
import { Button, Tooltip } from 'antd'
import { Suspense } from 'react'
import AttentionStrip from './_components/home/attention-strip'
import CustomizeHomeDrawer from './_components/home/customize-home-drawer'
import HomeGreeting from './_components/home/home-greeting'
import { HomeWidgets } from './_components/home/home-widgets'
import styles from './_components/home/home.module.css'
import { useHomeLayout } from './_components/home/use-home-layout'
import WidgetCard from './_components/home/widget-card'
import { HomeWidget, widgetAnchorId } from './_components/home/widget-registry'
import { WidgetSettingValues } from './_components/home/widget-settings'
import { WidgetSettingsContext } from './_components/home/widget-settings-form'

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

const WidgetColumn = ({
  widgets,
  className,
  settings,
}: {
  widgets: HomeWidget[]
  className: string
  settings: WidgetSettingsProps
}) => (
  <div className={className}>
    {widgets.map((widget) => {
      const { id, title, Component } = widget
      const { onChange } = settings
      return (
        <section key={id} id={widgetAnchorId(id)} className={styles.widget}>
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
        </section>
      )
    })}
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
      <AttentionStrip tiles={isLayoutLoading ? [] : tiles} />
      {isLayoutLoading ? null : hasWidgets ? (
        // With nothing for the side column, the main column takes the width.
        <div className={columns.side.length > 0 ? styles.columns : undefined}>
          <WidgetColumn
            widgets={columns.main}
            className={styles.column}
            settings={settings}
          />
          <WidgetColumn
            widgets={columns.side}
            className={`${styles.column} ${styles.side}`}
            settings={settings}
          />
        </div>
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
