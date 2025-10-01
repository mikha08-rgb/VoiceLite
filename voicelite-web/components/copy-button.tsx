'use client';

import { Copy, Check } from 'lucide-react';
import { useCopyToClipboard } from '@/hooks/use-copy-to-clipboard';

interface CopyButtonProps {
  text: string;
  onCopy?: () => void;
}

export function CopyButton({ text, onCopy }: CopyButtonProps) {
  const { isCopied, copy } = useCopyToClipboard();

  const handleCopy = async () => {
    const success = await copy(text);
    if (success && onCopy) {
      onCopy();
    }
  };

  return (
    <button
      onClick={handleCopy}
      className="group inline-flex items-center gap-2 rounded-lg px-3 py-1.5 text-xs font-semibold transition-all duration-200 hover:bg-purple-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 dark:hover:bg-purple-950/30"
      aria-label={isCopied ? 'Copied!' : 'Copy to clipboard'}
    >
      {isCopied ? (
        <>
          <Check className="h-4 w-4 text-emerald-600 dark:text-emerald-400" aria-hidden="true" />
          <span className="text-emerald-600 dark:text-emerald-400">Copied!</span>
        </>
      ) : (
        <>
          <Copy className="h-4 w-4 text-stone-600 transition-colors group-hover:text-purple-600 dark:text-stone-400 dark:group-hover:text-purple-400" aria-hidden="true" />
          <span className="text-stone-600 transition-colors group-hover:text-purple-600 dark:text-stone-400 dark:group-hover:text-purple-400">
            Copy
          </span>
        </>
      )}
    </button>
  );
}
