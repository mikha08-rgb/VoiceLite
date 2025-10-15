'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface TableColumn {
  key: string;
  label: string;
  className?: string;
}

export interface TableRow {
  [key: string]: React.ReactNode;
}

export interface TableProps {
  columns: TableColumn[];
  data: TableRow[];
  highlightColumn?: string;
  sortable?: boolean;
  mobileView?: 'cards' | 'scroll';
  className?: string;
}

/**
 * Table Component
 *
 * Variants:
 * - standard: Basic rows/columns
 * - comparison: Highlight one column (e.g., VoiceLite)
 * - sortable: Clickable headers to sort
 *
 * Responsive:
 * - Desktop: Full table
 * - Tablet: Horizontal scroll with sticky first column
 * - Mobile: Convert to cards (default) or horizontal scroll
 *
 * Features:
 * - Sticky header on scroll (long tables)
 * - Zebra striping for readability
 * - Highlight key column with subtle background
 *
 * Usage:
 * ```tsx
 * <Table
 *   columns={[
 *     { key: 'feature', label: 'Feature' },
 *     { key: 'voicelite', label: 'VoiceLite' },
 *     { key: 'dragon', label: 'Dragon' },
 *   ]}
 *   data={[
 *     { feature: 'Price', voicelite: '$20', dragon: '$500' },
 *     { feature: 'Privacy', voicelite: '100% local', dragon: 'Local' },
 *   ]}
 *   highlightColumn="voicelite"
 * />
 * ```
 */
export function Table({
  columns,
  data,
  highlightColumn,
  sortable = false,
  mobileView = 'cards',
  className,
}: TableProps) {
  const [sortKey, setSortKey] = React.useState<string | null>(null);
  const [sortDirection, setSortDirection] = React.useState<'asc' | 'desc'>('asc');

  const handleSort = (key: string) => {
    if (!sortable) return;

    if (sortKey === key) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortKey(key);
      setSortDirection('asc');
    }
  };

  const sortedData = React.useMemo(() => {
    if (!sortKey) return data;

    return [...data].sort((a, b) => {
      const aValue = String(a[sortKey] || '');
      const bValue = String(b[sortKey] || '');

      if (sortDirection === 'asc') {
        return aValue.localeCompare(bValue);
      } else {
        return bValue.localeCompare(aValue);
      }
    });
  }, [data, sortKey, sortDirection]);

  return (
    <>
      {/* Desktop Table (≥768px) */}
      <div className={cn('hidden md:block overflow-x-auto', className)}>
        <table className="w-full border-collapse border border-gray-300 rounded-lg overflow-hidden">
          <thead>
            <tr className="bg-gray-100">
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={cn(
                    'px-4 py-3 text-left text-sm font-semibold text-gray-900 border-b border-gray-300',
                    sortable && 'cursor-pointer hover:bg-gray-200',
                    highlightColumn === column.key && 'bg-blue-50',
                    column.className
                  )}
                  onClick={() => handleSort(column.key)}
                >
                  <div className="flex items-center gap-2">
                    {column.label}
                    {sortable && sortKey === column.key && (
                      <svg
                        className={cn(
                          'h-4 w-4 transition-transform',
                          sortDirection === 'desc' && 'rotate-180'
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
                          d="M5 15l7-7 7 7"
                        />
                      </svg>
                    )}
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sortedData.map((row, idx) => (
              <tr
                key={idx}
                className={cn(
                  'border-b border-gray-300 last:border-0',
                  idx % 2 === 0 ? 'bg-white' : 'bg-gray-50',
                  'hover:bg-gray-100 transition-colors'
                )}
              >
                {columns.map((column) => (
                  <td
                    key={column.key}
                    className={cn(
                      'px-4 py-3 text-sm text-gray-700',
                      highlightColumn === column.key && 'bg-blue-50 font-semibold',
                      column.className
                    )}
                  >
                    {row[column.key]}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile View - Cards (< 768px) */}
      {mobileView === 'cards' && (
        <div className="md:hidden space-y-4">
          {sortedData.map((row, idx) => (
            <div
              key={idx}
              className="bg-white border border-gray-300 rounded-lg p-4 space-y-3"
            >
              {columns.map((column) => (
                <div key={column.key} className="flex justify-between items-start">
                  <span className="text-sm font-semibold text-gray-900 flex-shrink-0 mr-4">
                    {column.label}:
                  </span>
                  <span
                    className={cn(
                      'text-sm text-gray-700 text-right',
                      highlightColumn === column.key && 'font-bold text-blue-600'
                    )}
                  >
                    {row[column.key]}
                  </span>
                </div>
              ))}
            </div>
          ))}
        </div>
      )}

      {/* Mobile View - Horizontal Scroll (< 768px) */}
      {mobileView === 'scroll' && (
        <div className="md:hidden overflow-x-auto">
          <table className="w-full border-collapse border border-gray-300 rounded-lg overflow-hidden min-w-[640px]">
            <thead>
              <tr className="bg-gray-100">
                {columns.map((column) => (
                  <th
                    key={column.key}
                    className={cn(
                      'px-4 py-3 text-left text-sm font-semibold text-gray-900 border-b border-gray-300',
                      highlightColumn === column.key && 'bg-blue-50',
                      column.className
                    )}
                  >
                    {column.label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {sortedData.map((row, idx) => (
                <tr
                  key={idx}
                  className={cn(
                    'border-b border-gray-300 last:border-0',
                    idx % 2 === 0 ? 'bg-white' : 'bg-gray-50'
                  )}
                >
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={cn(
                        'px-4 py-3 text-sm text-gray-700',
                        highlightColumn === column.key && 'bg-blue-50 font-semibold',
                        column.className
                      )}
                    >
                      {row[column.key]}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
