'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: 'feature' | 'pricing' | 'testimonial' | 'stat';
  hoverable?: boolean;
  children: React.ReactNode;
}

/**
 * Card Component
 *
 * Variants:
 * - feature: Icon + headline + description (for feature highlights)
 * - pricing: Plan comparison cards
 * - testimonial: Quote + author + avatar
 * - stat: Large number + label (e.g., "1000+ users")
 *
 * Props:
 * - hoverable: Adds lift effect on hover (default: true for feature/pricing)
 *
 * Usage:
 * ```tsx
 * <Card variant="feature">
 *   <CardIcon><Mic /></CardIcon>
 *   <CardTitle>Privacy First</CardTitle>
 *   <CardDescription>No cloud, no tracking, fully offline</CardDescription>
 * </Card>
 * ```
 */
export const Card = React.forwardRef<HTMLDivElement, CardProps>(
  ({ className, variant = 'feature', hoverable, children, ...props }, ref) => {
    const shouldHover = hoverable ?? (variant === 'feature' || variant === 'pricing');

    const baseStyles = 'bg-white rounded-lg border border-gray-300 transition-all duration-200';

    const variantStyles = {
      feature: 'p-6',
      pricing: 'p-8',
      testimonial: 'p-6',
      stat: 'p-6 text-center',
    };

    const hoverStyles = shouldHover
      ? 'hover:shadow-lg hover:-translate-y-1 cursor-pointer'
      : '';

    return (
      <div
        ref={ref}
        className={cn(
          baseStyles,
          variantStyles[variant],
          hoverStyles,
          className
        )}
        {...props}
      >
        {children}
      </div>
    );
  }
);

Card.displayName = 'Card';

/**
 * CardIcon - Container for icon at top of card (48x48px)
 */
export const CardIcon = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...props }, ref) => {
    return (
      <div
        ref={ref}
        className={cn('flex h-12 w-12 items-center justify-center rounded-lg bg-blue-50 text-blue-600 mb-4', className)}
        {...props}
      >
        {children}
      </div>
    );
  }
);

CardIcon.displayName = 'CardIcon';

/**
 * CardTitle - Card headline
 */
export const CardTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, children, ...props }, ref) => {
    return (
      <h3
        ref={ref}
        className={cn('text-xl font-semibold leading-relaxed text-gray-900 mb-2', className)}
        {...props}
      >
        {children}
      </h3>
    );
  }
);

CardTitle.displayName = 'CardTitle';

/**
 * CardDescription - Card body text
 */
export const CardDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, children, ...props }, ref) => {
    return (
      <p
        ref={ref}
        className={cn('text-base leading-relaxed text-gray-700', className)}
        {...props}
      >
        {children}
      </p>
    );
  }
);

CardDescription.displayName = 'CardDescription';

/**
 * CardStat - Large number for stat cards
 */
export const CardStat = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...props }, ref) => {
    return (
      <div
        ref={ref}
        className={cn('text-5xl font-bold text-blue-600 mb-2', className)}
        {...props}
      >
        {children}
      </div>
    );
  }
);

CardStat.displayName = 'CardStat';

/**
 * CardLabel - Label for stat cards
 */
export const CardLabel = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, children, ...props }, ref) => {
    return (
      <p
        ref={ref}
        className={cn('text-sm text-gray-700', className)}
        {...props}
      >
        {children}
      </p>
    );
  }
);

CardLabel.displayName = 'CardLabel';

/**
 * CardFooter - Optional footer section (for pricing cards, CTAs, etc.)
 */
export const CardFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...props }, ref) => {
    return (
      <div
        ref={ref}
        className={cn('mt-6 pt-6 border-t border-gray-200', className)}
        {...props}
      >
        {children}
      </div>
    );
  }
);

CardFooter.displayName = 'CardFooter';
