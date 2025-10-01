'use client';

import { useState, useRef, useEffect } from 'react';

interface TooltipProps {
  content: string;
  children: React.ReactNode;
  className?: string;
}

export function Tooltip({ content, children, className = '' }: TooltipProps) {
  const [isVisible, setIsVisible] = useState(false);
  const [position, setPosition] = useState<'top' | 'bottom'>('top');
  const triggerRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    if (isVisible && triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect();
      const spaceAbove = rect.top;
      const spaceBelow = window.innerHeight - rect.bottom;

      // Show tooltip below if not enough space above
      setPosition(spaceAbove < 60 ? 'bottom' : 'top');
    }
  }, [isVisible]);

  return (
    <span className={`relative inline-block ${className}`}>
      <span
        ref={triggerRef}
        onMouseEnter={() => setIsVisible(true)}
        onMouseLeave={() => setIsVisible(false)}
        onFocus={() => setIsVisible(true)}
        onBlur={() => setIsVisible(false)}
        className="cursor-help border-b border-dotted border-purple-400 dark:border-purple-500"
        tabIndex={0}
        role="button"
        aria-label={content}
      >
        {children}
      </span>
      {isVisible && (
        <span
          className={`absolute left-1/2 z-50 -translate-x-1/2 whitespace-nowrap rounded-lg bg-stone-900 px-3 py-2 text-xs font-medium text-white shadow-lg animate-in fade-in zoom-in-95 duration-200 dark:bg-stone-100 dark:text-stone-900 ${
            position === 'top' ? 'bottom-full mb-2' : 'top-full mt-2'
          }`}
          role="tooltip"
        >
          {content}
          <span
            className={`absolute left-1/2 h-2 w-2 -translate-x-1/2 rotate-45 bg-stone-900 dark:bg-stone-100 ${
              position === 'top' ? '-bottom-1' : '-top-1'
            }`}
          />
        </span>
      )}
    </span>
  );
}
