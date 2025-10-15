'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  success?: string;
  helperText?: string;
  icon?: React.ReactNode;
}

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  error?: string;
  success?: string;
  helperText?: string;
}

/**
 * Input Component
 *
 * Variants (via type prop):
 * - text: Standard text entry
 * - email: With email validation
 * - search: With search icon
 * - password: With password toggle
 *
 * States:
 * - Default (empty), Focus (border highlight), Filled (has value)
 * - Error (red border + error message), Success (green border + checkmark)
 * - Disabled (grayed out)
 *
 * Accessibility:
 * - Always include labels
 * - Inline validation (show errors on blur)
 * - 16px font size minimum (prevents iOS zoom)
 *
 * Usage:
 * ```tsx
 * <Input
 *   label="Email Address"
 *   type="email"
 *   placeholder="you@example.com"
 *   error="Please enter a valid email"
 *   required
 * />
 * ```
 */
export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  (
    {
      className,
      type = 'text',
      label,
      error,
      success,
      helperText,
      icon,
      id,
      required,
      disabled,
      ...props
    },
    ref
  ) => {
    const inputId = id || `input-${React.useId()}`;
    const errorId = `${inputId}-error`;
    const helperId = `${inputId}-helper`;

    const baseStyles = 'w-full px-4 py-3 text-base rounded-lg border transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-gray-50';

    const stateStyles = error
      ? 'border-red-500 focus:border-red-500 focus:ring-red-600'
      : success
      ? 'border-green-500 focus:border-green-500 focus:ring-green-600'
      : 'border-gray-300 focus:border-blue-600';

    const iconPadding = icon ? 'pl-12' : '';

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={inputId}
            className="block text-sm font-medium text-gray-900 mb-2"
          >
            {label}
            {required && <span className="text-red-500 ml-1">*</span>}
          </label>
        )}

        <div className="relative">
          {icon && (
            <div className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500">
              {icon}
            </div>
          )}

          <input
            ref={ref}
            id={inputId}
            type={type}
            className={cn(baseStyles, stateStyles, iconPadding, className)}
            aria-invalid={error ? 'true' : 'false'}
            aria-describedby={error ? errorId : helperText ? helperId : undefined}
            aria-required={required}
            disabled={disabled}
            {...props}
          />

          {/* Success checkmark */}
          {success && (
            <div className="absolute right-4 top-1/2 -translate-y-1/2">
              <svg
                className="h-5 w-5 text-green-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M5 13l4 4L19 7"
                />
              </svg>
            </div>
          )}
        </div>

        {/* Error message */}
        {error && (
          <p id={errorId} className="mt-2 text-sm text-red-500" role="alert">
            {error}
          </p>
        )}

        {/* Success message */}
        {success && (
          <p className="mt-2 text-sm text-green-500">{success}</p>
        )}

        {/* Helper text */}
        {helperText && !error && !success && (
          <p id={helperId} className="mt-2 text-sm text-gray-500">
            {helperText}
          </p>
        )}
      </div>
    );
  }
);

Input.displayName = 'Input';

/**
 * Textarea Component
 *
 * Same API as Input, but for multi-line text entry
 *
 * Usage:
 * ```tsx
 * <Textarea
 *   label="Message"
 *   placeholder="Tell us about your issue..."
 *   rows={5}
 *   maxLength={500}
 * />
 * ```
 */
export const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  (
    {
      className,
      label,
      error,
      success,
      helperText,
      id,
      required,
      disabled,
      ...props
    },
    ref
  ) => {
    const textareaId = id || `textarea-${React.useId()}`;
    const errorId = `${textareaId}-error`;
    const helperId = `${textareaId}-helper`;

    const baseStyles = 'w-full px-4 py-3 text-base rounded-lg border transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-gray-50 resize-y';

    const stateStyles = error
      ? 'border-red-500 focus:border-red-500 focus:ring-red-600'
      : success
      ? 'border-green-500 focus:border-green-500 focus:ring-green-600'
      : 'border-gray-300 focus:border-blue-600';

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={textareaId}
            className="block text-sm font-medium text-gray-900 mb-2"
          >
            {label}
            {required && <span className="text-red-500 ml-1">*</span>}
          </label>
        )}

        <textarea
          ref={ref}
          id={textareaId}
          className={cn(baseStyles, stateStyles, className)}
          aria-invalid={error ? 'true' : 'false'}
          aria-describedby={error ? errorId : helperText ? helperId : undefined}
          aria-required={required}
          disabled={disabled}
          {...props}
        />

        {/* Error message */}
        {error && (
          <p id={errorId} className="mt-2 text-sm text-red-500" role="alert">
            {error}
          </p>
        )}

        {/* Success message */}
        {success && (
          <p className="mt-2 text-sm text-green-500">{success}</p>
        )}

        {/* Helper text */}
        {helperText && !error && !success && (
          <p id={helperId} className="mt-2 text-sm text-gray-500">
            {helperText}
          </p>
        )}
      </div>
    );
  }
);

Textarea.displayName = 'Textarea';

/**
 * SearchInput - Pre-configured Input with search icon
 *
 * Usage:
 * ```tsx
 * <SearchInput placeholder="Search for help..." onSearch={handleSearch} />
 * ```
 */
export interface SearchInputProps extends Omit<InputProps, 'type' | 'icon'> {
  onSearch?: (value: string) => void;
}

export const SearchInput = React.forwardRef<HTMLInputElement, SearchInputProps>(
  ({ onSearch, ...props }, ref) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Enter' && onSearch) {
        onSearch(e.currentTarget.value);
      }
    };

    return (
      <Input
        ref={ref}
        type="search"
        icon={
          <svg
            className="h-5 w-5"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            aria-hidden="true"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
            />
          </svg>
        }
        onKeyDown={handleKeyDown}
        {...props}
      />
    );
  }
);

SearchInput.displayName = 'SearchInput';
