'use client'

import { FC } from 'react'
import { AttentionTileDefinition } from './widget-registry'
import styles from './home.module.css'

export interface AttentionStripProps {
  tiles: AttentionTileDefinition[]
}

/** The row of counts across the top of the home page; absent when no shown widget has one. */
const AttentionStrip: FC<AttentionStripProps> = ({ tiles }) => {
  if (tiles.length === 0) return null

  return (
    <section className={styles.attentionStrip} aria-label="Needs attention">
      {tiles.map(({ id, Tile }) => (
        <div key={id} className={styles.attentionTile}>
          <Tile />
        </div>
      ))}
    </section>
  )
}

export default AttentionStrip
