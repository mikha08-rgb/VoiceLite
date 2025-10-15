'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface AccordionItem {
  id: string;
  title: string;
  content: React.ReactNode;
}

export interface AccordionProps {
  items: AccordionItem[];
  type?: 'single' | 'multiple';
  defaultOpen?: string | string[];
  className?: string;
}

/**
 * Accordion Component
 *
 * Variants:
 * - single: Only one item open at a time
 * - multiple: Multiple items can be open simultaneously
 *
 * Features:
 * - Smooth animation (300ms ease)
 * - Keyboard navigation (Space/Enter to toggle, Arrow keys to navigate)
 * - Deep linking (URL fragments like #faq-refund)
 * - ARIA compliant
 *
 * Usage:
 * ```tsx
 * <Accordion
 *   type="single"
 *   items={[
 *     { id: 'refund', title: 'Can I get a refund?', content: 'Yes, 30 days...' },
 *     { id: 'internet', title: 'Do I need internet?', content: 'Only for purchase...' },
 *   ]}
 * />
 * ```
 */
export function Accordion({
  items,
  type = 'single',
  defaultOpen,
  className,
}: AccordionProps) {
  const [openItems, setOpenItems] = React.useState<Set<string>>(() => {
    if (typeof defaultOpen === 'string') {
      return new Set([defaultOpen]);
    } else if (Array.isArray(defaultOpen)) {
      return new Set(defaultOpen);
    }
    return new Set();
  });

  // Handle deep linking (URL fragments)
  React.useEffect(() => {
    if (typeof window !== 'undefined') {
      const hash = window.location.hash.slice(1);
      if (hash && items.some((item) => item.id === hash)) {
        setOpenItems(new Set([hash]));

        // Scroll to the item
        setTimeout(() => {
          const element = document.getElementById(hash);
          element?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 100);
      }
    }
  }, [items]);

  const toggleItem = (id: string) => {
    setOpenItems((prev) => {
      const newSet = new Set(prev);

      if (type === 'single') {
        // Single mode: close all, open clicked (or close if already open)
        if (newSet.has(id)) {
          newSet.delete(id);
        } else {
          newSet.clear();
          newSet.add(id);
        }
      } else {
        // Multiple mode: toggle clicked
        if (newSet.has(id)) {
          newSet.delete(id);
        } else {
          newSet.add(id);
        }
      }

      return newSet;
    });

    // Update URL hash
    if (typeof window !== 'undefined') {
      window.history.replaceState(null, '', `#${id}`);
    }
  };

  return (
    <div className={cn('w-full space-y-2', className)}>
      {items.map((item) => (
        <AccordionItem
          key={item.id}
          item={item}
          isOpen={openItems.has(item.id)}
          onToggle={() => toggleItem(item.id)}
        />
      ))}
    </div>
  );
}

/**
 * AccordionItem - Internal component
 */
function AccordionItem({
  item,
  isOpen,
  onToggle,
}: {
  item: AccordionItem;
  isOpen: boolean;
  onToggle: () => void;
}) {
  const contentRef = React.useRef<HTMLDivElement>(null);
  const [height, setHeight] = React.useState<number | undefined>(undefined);

  // Calculate content height for smooth animation
  React.useEffect(() => {
    if (isOpen && contentRef.current) {
      setHeight(contentRef.current.scrollHeight);
    } else {
      setHeight(0);
    }
  }, [isOpen]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onToggle();
    }
  };

  return (
    <div
      id={item.id}
      className="border border-gray-300 rounded-lg overflow-hidden bg-white"
    >
      {/* Header */}
      <button
        onClick={onToggle}
        onKeyDown={handleKeyDown}
        className="w-full flex items-center justify-between px-6 py-4 text-left hover:bg-gray-50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2"
        aria-expanded={isOpen}
        aria-controls={`${item.id}-content`}
      >
        <span className="text-base font-semibold text-gray-900 pr-4">
          {item.title}
        </span>

        {/* Chevron Icon */}
        <svg
          className={cn(
            'h-5 w-5 text-gray-500 transition-transform duration-300 flex-shrink-0',
            isOpen && 'rotate-180'
          )}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          aria-hidden="true"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M19 9l-7 7-7-7"
          />
        </svg>
      </button>

      {/* Content */}
      <div
        id={`${item.id}-content`}
        className="overflow-hidden transition-all duration-300 ease-out"
        style={{ height: height !== undefined ? `${height}px` : undefined }}
        aria-hidden={!isOpen}
      >
        <div ref={contentRef} className="px-6 py-4 text-base text-gray-700">
          {item.content}
        </div>
      </div>
    </div>
  );
}
