import { useEffect, type RefObject } from 'react'

/**
 * After the server rejects fields, moves focus to the first field marked `aria-invalid`, so a
 * screen reader announces that field and the error it describes (AC-02, AC-03). Pass the same
 * errors object the fields render from; a new object means a new failed submit.
 */
export function useFocusFirstInvalid(
  formRef: RefObject<HTMLFormElement | null>,
  fieldErrors: Record<string, unknown>,
) {
  useEffect(() => {
    if (Object.keys(fieldErrors).length === 0) {
      return
    }
    formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus()
  }, [formRef, fieldErrors])
}
