import { useId, type InputHTMLAttributes } from 'react'

interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  label: string
  /** What the field must be; shown under the input and announced as its description. */
  errors?: string[]
}

export function TextField({ label, errors, className, ...input }: TextFieldProps) {
  const id = useId()
  const errorId = `${id}-error`
  const invalid = errors !== undefined && errors.length > 0

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-gray-800">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? errorId : undefined}
        className={`rounded-md border px-3 py-2 text-base outline-none focus:ring-2 focus:ring-blue-500 ${
          invalid ? 'border-red-600' : 'border-gray-300'
        } ${className ?? ''}`}
        {...input}
      />
      {invalid && (
        <p id={errorId} className="text-sm text-red-700">
          {errors.join(' ')}
        </p>
      )}
    </div>
  )
}
