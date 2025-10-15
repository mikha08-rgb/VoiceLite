'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: 'info' | 'success' | 'warning' | 'danger' | 'neutral';
  size?: 'sm' | 'md';
  children: React.ReactNode;
}

/**
 * Badge/Tag Component
 *
 * Variants:
 * - info: Neutral information (blue)
 * - success: Positive status (green)
 * - warning: Caution (yellow/amber)
 * - danger: Error or critical (red)
 * - neutral: Generic labels (gray)
 *
 * Sizes:
 * - sm: Small (12px text)
 * - md: Medium (14px text)
 *
 * Usage:
 * ```tsx
 * <Badge variant="success">Active</Badge>
 * <Badge variant="warning" size="sm">Beta</Badge>
 * <Badge variant="info">v1.0.66</Badge>
 * ```
 */
export const Badge = React.forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = 'neutral', size = 'md', children, ...props }, ref) => {
    const baseStyles = 'inline-flex items-center font-medium rounded-full';

    const variantStyles = {
      info: 'bg-blue-100 text-blue-700 border border-blue-200',
      success: 'bg-green-100 text-green-700 border border-green-200',
      warning: 'bg-amber-100 text-amber-700 border border-amber-200',
      danger: 'bg-red-100 text-red-700 border border-red-200',
      neutral: 'bg-gray-100 text-gray-700 border border-gray-200',
    };

    const sizeStyles = {
      sm: 'px-2 py-0.5 text-xs',
      md: 'px-3 py-1 text-sm',
    };

    return (
      <span
        ref={ref}
        className={cn(
          baseStyles,
          variantStyles[variant],
          sizeStyles[size],
          className
        )}
        {...props}
      >
        {children}
      </span>
    );
  }
);

Badge.displayName = 'Badge';
