'use client'

import { useCallback, useEffect, useState } from 'react'

export interface UseLocalStorageStateOptions {
  /**
   * Optional version number for the stored data.
   * When the version changes, old data is automatically cleared and defaults are used.
   * This prevents schema conflicts when data structures evolve.
   */
  version?: number
}

/**
 * Helper function to read and initialize localStorage value
 */
function readLocalStorage<T>(
  key: string,
  versionedKey: string,
  version: number | undefined,
  defaultValue: T,
): T {
  if (typeof window === 'undefined') {
    return defaultValue
  }
  try {
    // Try to read versioned key first
    const storedValue = window.localStorage.getItem(versionedKey)
    if (storedValue !== null && storedValue !== 'undefined') {
      return JSON.parse(storedValue)
    }

    // If versioned key not found and we're using versioning
    if (version !== undefined) {
      // Try to migrate from unversioned key
      const unversionedValue = window.localStorage.getItem(key)
      let valueToReturn = defaultValue

      if (unversionedValue !== null) {
        try {
          valueToReturn = JSON.parse(unversionedValue)
          // Save migrated value to versioned key
          window.localStorage.setItem(versionedKey, unversionedValue)
        } catch {
          // If parsing fails, use default
          valueToReturn = defaultValue
        }
      }

      // Collect keys to remove (don't modify during iteration)
      const keysToRemove: string[] = []
      for (let i = 0; i < window.localStorage.length; i++) {
        const storageKey = window.localStorage.key(i)
        // Remove old versioned keys and unversioned key
        if (
          (storageKey?.startsWith(`${key}:v`) && storageKey !== versionedKey) ||
          storageKey === key
        ) {
          keysToRemove.push(storageKey)
        }
      }
      // Remove old keys
      keysToRemove.forEach((k) => window.localStorage.removeItem(k))

      return valueToReturn
    }

    return defaultValue
  } catch (error) {
    console.error(`Error reading localStorage key "${versionedKey}":`, error)
    return defaultValue
  }
}

export const useLocalStorageState = <T>(
  key: string,
  defaultValue: T,
  options?: UseLocalStorageStateOptions,
): [T, React.Dispatch<React.SetStateAction<T>>] => {
  const version = options?.version
  const versionedKey = version !== undefined ? `${key}:v${version}` : key

  const [value, setValue] = useState<T>(() =>
    readLocalStorage(key, versionedKey, version, defaultValue),
  )

  // Track the previous versionedKey in state to detect changes
  // This is the recommended React pattern for resetting state when props change
  // See: https://react.dev/learn/you-might-not-need-an-effect#adjusting-some-state-when-a-prop-changes
  const [prevVersionedKey, setPrevVersionedKey] = useState(versionedKey)

  // Re-initialize state when versionedKey changes using the "state during render" pattern
  if (prevVersionedKey !== versionedKey) {
    const newValue = readLocalStorage(key, versionedKey, version, defaultValue)
    setPrevVersionedKey(versionedKey)
    setValue(newValue)
  }

  // Update localStorage when value changes.
  useEffect(() => {
    try {
      if (value === undefined) {
        window.localStorage.removeItem(versionedKey)
        return
      }
      window.localStorage.setItem(versionedKey, JSON.stringify(value))
    } catch (error) {
      console.error(`Error writing localStorage key "${versionedKey}":`, error)
    }
  }, [versionedKey, value])

  // Listen for changes to localStorage from other tabs/windows.
  useEffect(() => {
    const handleStorageChange = (event: StorageEvent) => {
      if (event.key === versionedKey) {
        try {
          const newValue = event.newValue
            ? JSON.parse(event.newValue)
            : defaultValue
          setValue(newValue)
        } catch (error) {
          console.error(
            `Error parsing localStorage key "${versionedKey}" on storage event:`,
            error,
          )
        }
      }
    }

    window.addEventListener('storage', handleStorageChange, { passive: true })
    return () => {
      window.removeEventListener('storage', handleStorageChange)
    }
  }, [versionedKey, defaultValue])

  const setLocalStorageValue = useCallback(
    (newValue: React.SetStateAction<T>) => {
      setValue((prevValue) => {
        const valueToStore =
          typeof newValue === 'function'
            ? (newValue as (prevState: T) => T)(prevValue)
            : newValue

        // Only update if value actually changed.
        if (JSON.stringify(prevValue) === JSON.stringify(valueToStore)) {
          return prevValue
        }

        return valueToStore
      })
    },
    [],
  )

  return [value, setLocalStorageValue]
}
