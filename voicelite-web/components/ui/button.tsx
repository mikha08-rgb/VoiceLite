'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
  children: React.ReactNode;
}

/**
 * Button Component
 *
 * Variants:
 * - primary: Main CTAs (Download, Buy Now)
 * - secondary: Lower-priority actions (Learn More, See Docs)
 * - ghost: Tertiary actions (text links with hover state)
 * - danger: Destructive actions (Delete account)
 *
 * Sizes:
 * - sm: Small (32px height)
 * - md: Medium (44px height) - Default, meets accessibility touch target
 * - lg: Large (56px height)
 *
 * States:
 * - Default, Hover (lift + shadow), Active (pressed), Disabled, Loading
 *
 * Usage:
 * ```tsx
 * <Button variant="primary" size="lg">Download Now</Button>
 * <Button variant="secondary">Learn More</Button>
 * <Button variant="ghost" size="sm">Cancel</Button>
 * <Button variant="primary" isLoading>Processing...</Button>
 * ```
 */
export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = 'primary', size = 'md', isLoading = false, disabled, children, ...props }, ref) => {
    const baseStyles = 'inline-flex items-center justify-center font-semibold rounded-lg transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed disabled:pointer-events-none';

    const variantStyles = {
      primary: 'bg-emerald-500 text-white shadow-md hover:bg-emerald-600 hover:shadow-lg hover:-translate-y-0.5 active:translate-y-0',
      secondary: 'bg-transparent border-2 border-gray-300 text-gray-700 hover:border-blue-600 hover:text-blue-600',
      ghost: 'bg-transparent text-gray-700 hover:bg-gray-100 hover:text-gray-900',
      danger: 'bg-red-500 text-white shadow-md hover:bg-red-600 hover:shadow-lg hover:-translate-y-0.5 active:translate-y-0',
    };

    const sizeStyles = {
      sm: 'h-8 px-4 py-1.5 text-sm',
      md: 'h-11 px-6 py-3 text-base', // 44px height = accessibility target
      lg: 'h-14 px-8 py-4 text-lg',
    };

    return (
      <button
        ref={ref}
        className={cn(
          baseStyles,
          variantStyles[variant],
          sizeStyles[size],
          className
        )}
        disabled={disabled || isLoading}
        {...props}
      >
        {isLoading && (
          <svg
            className="mr-2 h-4 w-4 animate-spin"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
        )}
        {children}
      </button>
    );
  }
);

Button.displayName = 'Button';
