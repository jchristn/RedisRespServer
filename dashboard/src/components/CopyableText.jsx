import { useState } from 'react';
import './CopyableText.css';

export default function CopyableText({ text, truncate = false }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  return (
    <span className={`copyable-text ${truncate ? 'truncate' : ''}`}>
      <span className="copyable-value mono">{text}</span>
      <button
        className="copy-btn"
        onClick={handleCopy}
        title={copied ? 'Copied!' : 'Copy to clipboard'}
      >
        {copied ? '✓' : '📋'}
      </button>
    </span>
  );
}
