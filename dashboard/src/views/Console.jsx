import { useState, useRef, useEffect } from 'react';
import { useApp } from '../context/AppContext';
import './Console.css';

export default function Console() {
  const { api } = useApp();
  const [command, setCommand] = useState('');
  const [history, setHistory] = useState([]);
  const [historyIndex, setHistoryIndex] = useState(-1);
  const [commandHistory, setCommandHistory] = useState([]);
  const outputRef = useRef(null);
  const inputRef = useRef(null);

  useEffect(() => {
    // Focus input on mount
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    // Scroll to bottom when history changes
    if (outputRef.current) {
      outputRef.current.scrollTop = outputRef.current.scrollHeight;
    }
  }, [history]);

  const executeCommand = async (cmd) => {
    if (!cmd.trim() || !api) return;

    const trimmedCmd = cmd.trim();

    // Add to command history
    setCommandHistory((prev) => {
      const newHistory = [...prev.filter((c) => c !== trimmedCmd), trimmedCmd];
      // Keep last 100 commands
      return newHistory.slice(-100);
    });
    setHistoryIndex(-1);

    // Add command to output
    setHistory((prev) => [
      ...prev,
      { type: 'command', content: trimmedCmd, timestamp: new Date() },
    ]);

    try {
      const result = await api.executeCommand(trimmedCmd);
      setHistory((prev) => [
        ...prev,
        { type: 'result', content: formatResult(result), timestamp: new Date() },
      ]);
    } catch (err) {
      setHistory((prev) => [
        ...prev,
        { type: 'error', content: err.message, timestamp: new Date() },
      ]);
    }

    setCommand('');
  };

  const formatResult = (result) => {
    if (result === null || result === undefined) {
      return '(nil)';
    }
    if (typeof result === 'boolean') {
      return result ? '(integer) 1' : '(integer) 0';
    }
    if (typeof result === 'number') {
      return `(integer) ${result}`;
    }
    if (typeof result === 'string') {
      return `"${result}"`;
    }
    if (Array.isArray(result)) {
      if (result.length === 0) {
        return '(empty array)';
      }
      return result
        .map((item, index) => `${index + 1}) ${formatResult(item)}`)
        .join('\n');
    }
    if (typeof result === 'object') {
      return JSON.stringify(result, null, 2);
    }
    return String(result);
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      executeCommand(command);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (commandHistory.length > 0) {
        const newIndex =
          historyIndex === -1
            ? commandHistory.length - 1
            : Math.max(0, historyIndex - 1);
        setHistoryIndex(newIndex);
        setCommand(commandHistory[newIndex]);
      }
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (historyIndex !== -1) {
        const newIndex = historyIndex + 1;
        if (newIndex >= commandHistory.length) {
          setHistoryIndex(-1);
          setCommand('');
        } else {
          setHistoryIndex(newIndex);
          setCommand(commandHistory[newIndex]);
        }
      }
    } else if (e.key === 'l' && e.ctrlKey) {
      e.preventDefault();
      setHistory([]);
    }
  };

  const clearHistory = () => {
    setHistory([]);
  };

  return (
    <div className="console-view">
      <div className="page-header">
        <h1 className="page-title">Console</h1>
        <div className="page-actions">
          <button className="btn btn-secondary btn-sm" onClick={clearHistory}>
            Clear
          </button>
        </div>
      </div>

      <div className="console-container">
        <div className="console-output" ref={outputRef}>
          <div className="console-welcome">
            <p>Welcome to Redish Console</p>
            <p className="text-muted">
              Type Redis commands below. Use ↑/↓ to navigate history. Ctrl+L to clear.
            </p>
          </div>

          {history.map((entry, index) => (
            <div key={index} className={`console-entry console-${entry.type}`}>
              {entry.type === 'command' && (
                <div className="console-command">
                  <span className="console-prompt">&gt;</span>
                  <span className="console-cmd">{entry.content}</span>
                </div>
              )}
              {entry.type === 'result' && (
                <pre className="console-result">{entry.content}</pre>
              )}
              {entry.type === 'error' && (
                <pre className="console-error">(error) {entry.content}</pre>
              )}
            </div>
          ))}
        </div>

        <div className="console-input-container">
          <span className="console-prompt">&gt;</span>
          <input
            ref={inputRef}
            type="text"
            className="console-input"
            value={command}
            onChange={(e) => setCommand(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Enter Redis command..."
            autoComplete="off"
            autoCorrect="off"
            autoCapitalize="off"
            spellCheck="false"
          />
        </div>
      </div>

      <div className="console-hints">
        <h4>Common Commands</h4>
        <div className="hints-grid">
          <HintButton command="PING" onClick={executeCommand} />
          <HintButton command="INFO" onClick={executeCommand} />
          <HintButton command="DBSIZE" onClick={executeCommand} />
          <HintButton command="KEYS *" onClick={executeCommand} />
          <HintButton command="FLUSHDB" onClick={executeCommand} />
          <HintButton command="TIME" onClick={executeCommand} />
          <HintButton command="CLIENT LIST" onClick={executeCommand} />
          <HintButton command="CONFIG GET *" onClick={executeCommand} />
        </div>
      </div>
    </div>
  );
}

function HintButton({ command, onClick }) {
  return (
    <button
      className="hint-btn"
      onClick={() => onClick(command)}
      title={`Run ${command}`}
    >
      {command}
    </button>
  );
}
