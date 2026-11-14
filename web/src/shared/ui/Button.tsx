import type { ButtonHTMLAttributes } from 'react'

export function Button({
  className,
  type = 'button',
  ...button
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type={type}
      className={`rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60 ${className ?? ''}`}
      {...button}
    />
  )
}
